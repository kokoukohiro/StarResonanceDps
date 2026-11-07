using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Operations;

namespace StarResonanceDps.PluginSdk.Analyzers;

/// <summary>
/// プラグインのウィジェットと通知の定義のうち、コードを見て決まりに合わないと分かるものをビルドで止める(<see cref="PluginDiagnostics"/>)。
/// 関数を通した値など、コードから中身が見えないものは確かめない(本体が起動時に確かめて止める)。
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class PluginDefinitionAnalyzer : DiagnosticAnalyzer
{
    private const string SdkNamespace = "StarResonanceDps.PluginSdk.";

    /// <summary>少なくとも1つの文字が要る、言語ごとの文字。本体が定義を確かめる決まりと同じ。</summary>
    private static readonly (string TypeName, string PropertyName)[] RequiredTexts =
    [
        ("PluginWidgetDefinition", "Names"),
        ("PluginWidgetSettingSection", "Names"),
        ("PluginWidgetTarget", "Names"),
        ("PluginNotificationDefinition", "Names"),
        ("PluginNotificationDefinition", "DefaultTexts"),
        ("PluginNotificationField", "Names"),
        ("PluginSettingDefinition", "Names"),
        ("PluginChoiceOption", "Names")
    ];

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get; } = ImmutableArray.Create(
        PluginDiagnostics.EmptyWidgetTargets,
        PluginDiagnostics.EmptyRequiredText);

    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterCompilationStartAction(start =>
        {
            var requiredTexts = FindRequiredTexts(start.Compilation);
            if (requiredTexts.Count > 0)
            {
                start.RegisterOperationAction(
                    operation => AnalyzeAssignment(operation, requiredTexts),
                    OperationKind.SimpleAssignment);
            }

            var multiProvider = start.Compilation.GetTypeByMetadataName(SdkNamespace + "IPluginMultiWidgetProvider");
            var getTargets = multiProvider?.GetMembers("GetWidgetTargets").OfType<IMethodSymbol>().FirstOrDefault();
            if (multiProvider is not null && getTargets is not null)
            {
                start.RegisterOperationAction(
                    operation => AnalyzeMethodBody(operation, multiProvider, getTargets),
                    OperationKind.MethodBody);
            }
        });
    }

    private static HashSet<IPropertySymbol> FindRequiredTexts(Compilation compilation)
    {
        var properties = new HashSet<IPropertySymbol>(SymbolEqualityComparer.Default);
        foreach (var (typeName, propertyName) in RequiredTexts)
        {
            var property = compilation.GetTypeByMetadataName(SdkNamespace + typeName)?
                .GetMembers(propertyName)
                .OfType<IPropertySymbol>()
                .FirstOrDefault();
            if (property is not null)
            {
                properties.Add(property);
            }
        }

        return properties;
    }

    /// <summary>定義の名前などへの代入。値が null・空・空白の文字だけと見えれば止める。</summary>
    private static void AnalyzeAssignment(OperationAnalysisContext context, HashSet<IPropertySymbol> requiredTexts)
    {
        var assignment = (ISimpleAssignmentOperation)context.Operation;
        if (assignment.Target is IPropertyReferenceOperation target
            && requiredTexts.Contains(target.Property.OriginalDefinition)
            && IsBlankTexts(assignment.Value))
        {
            context.ReportDiagnostic(Diagnostic.Create(
                PluginDiagnostics.EmptyRequiredText,
                assignment.Value.Syntax.GetLocation(),
                $"{target.Property.ContainingType.Name}.{target.Property.Name}"));
        }
    }

    private static bool IsBlankTexts(IOperation value)
    {
        var operation = Unwrap(value);
        if (IsNull(operation))
        {
            return true;
        }

        return operation switch
        {
            ICollectionExpressionOperation collection => collection.Elements.Length == 0,
            IObjectCreationOperation creation => creation.Arguments.Length == 0
                && (creation.Initializer is null || creation.Initializer.Initializers.All(IsBlankEntry)),
            _ => false
        };
    }

    /// <summary>辞書の初期化子の1項目。言語名か文字が空白だと本体が捨てる。値が定数でなければ中身が分からないので、空白とは見ない。</summary>
    private static bool IsBlankEntry(IOperation entry)
    {
        return entry switch
        {
            ISimpleAssignmentOperation { Target: IPropertyReferenceOperation { Arguments.Length: 1 } indexer } assignment
                => IsBlankText(indexer.Arguments[0].Value) || IsBlankText(assignment.Value),
            IInvocationOperation { Arguments.Length: 2 } add
                => IsBlankText(add.Arguments[0].Value) || IsBlankText(add.Arguments[1].Value),
            _ => false
        };
    }

    private static bool IsBlankText(IOperation value)
    {
        var constant = Unwrap(value).ConstantValue;
        return constant.HasValue && (constant.Value is null || (constant.Value is string text && string.IsNullOrWhiteSpace(text)));
    }

    /// <summary>
    /// <c>GetWidgetTargets</c> の実装の return が全部、空の一覧と見えれば止める。
    /// 途中の <c>if (…) return [];</c> は、ほかに中身を返す return があれば止めない。
    /// </summary>
    private static void AnalyzeMethodBody(OperationAnalysisContext context, INamedTypeSymbol multiProvider, IMethodSymbol getTargets)
    {
        if (context.ContainingSymbol is not IMethodSymbol method
            || !method.ContainingType.AllInterfaces.Any(implemented => SymbolEqualityComparer.Default.Equals(implemented, multiProvider))
            || !SymbolEqualityComparer.Default.Equals(method.ContainingType.FindImplementationForInterfaceMember(getTargets), method))
        {
            return;
        }

        var returns = new List<IReturnOperation>();
        CollectReturns(context.Operation, returns);
        if (returns.Count > 0 && returns.All(returned => returned.ReturnedValue is { } value && IsEmptyList(value)))
        {
            context.ReportDiagnostic(Diagnostic.Create(PluginDiagnostics.EmptyWidgetTargets, method.Locations[0]));
        }
    }

    /// <summary>そのメソッド自身の return を集める(中のラムダとローカル関数の return は別の処理の値)。</summary>
    private static void CollectReturns(IOperation operation, List<IReturnOperation> returns)
    {
        foreach (var child in operation.ChildOperations)
        {
            if (child is IAnonymousFunctionOperation or ILocalFunctionOperation)
            {
                continue;
            }

            if (child is IReturnOperation returned)
            {
                returns.Add(returned);
            }

            CollectReturns(child, returns);
        }
    }

    private static bool IsEmptyList(IOperation value)
    {
        var operation = Unwrap(value);
        if (IsNull(operation))
        {
            return true;
        }

        return operation switch
        {
            ICollectionExpressionOperation collection => collection.Elements.Length == 0,
            IArrayCreationOperation array => array.Initializer is { ElementValues.Length: 0 }
                || (array.Initializer is null && array.DimensionSizes.All(size => size.ConstantValue is { HasValue: true, Value: 0 })),
            IObjectCreationOperation creation => creation.Arguments.Length == 0
                && (creation.Initializer is null || creation.Initializer.Initializers.Length == 0),
            IInvocationOperation invocation => invocation.TargetMethod.Name == "Empty"
                && invocation.Arguments.Length == 0
                && invocation.TargetMethod.ContainingType.Name is "Array" or "Enumerable",
            _ => false
        };
    }

    private static bool IsNull(IOperation operation)
    {
        return operation is IDefaultValueOperation
            || operation.ConstantValue is { HasValue: true, Value: null };
    }

    private static IOperation Unwrap(IOperation operation)
    {
        while (operation is IConversionOperation conversion)
        {
            operation = conversion.Operand;
        }

        return operation;
    }
}
