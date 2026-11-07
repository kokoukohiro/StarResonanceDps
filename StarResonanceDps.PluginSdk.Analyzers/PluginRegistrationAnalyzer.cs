using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

namespace StarResonanceDps.PluginSdk.Analyzers;

/// <summary>
/// プラグインの属性を、本体が読み込むときと同じ決まりで確かめる(<see cref="PluginDiagnostics"/>)。
/// SDK を参照していても、プラグインの入口のクラス(<c>IStarResonancePlugin</c> を実装した、abstract でないクラス)も
/// プラグインの属性も無いアセンブリ(プラグインが分けた部品の DLL)は確かめない。
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class PluginRegistrationAnalyzer : DiagnosticAnalyzer
{
    private const string DisplayNameAttributeName = "PluginDisplayName";
    private const string GenreAttributeName = "PluginGenre";

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get; } = ImmutableArray.Create(
        PluginDiagnostics.MissingRegistration,
        PluginDiagnostics.ApiVersionMismatch,
        PluginDiagnostics.InvalidEntryType,
        PluginDiagnostics.MissingDisplayName,
        PluginDiagnostics.DuplicateCulture,
        PluginDiagnostics.MissingAuthor,
        PluginDiagnostics.MissingGenre,
        PluginDiagnostics.UnusedGenre,
        PluginDiagnostics.EmptyLocalizedText,
        PluginDiagnostics.EmptyIconPath,
        PluginDiagnostics.InvalidVersion);

    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterCompilationAction(AnalyzeCompilation);
    }

    private static void AnalyzeCompilation(CompilationAnalysisContext context)
    {
        if (SdkSymbols.Find(context.Compilation) is not { } sdk)
        {
            return;
        }

        var assembly = context.Compilation.Assembly;
        var attributes = assembly.GetAttributes();
        var registration = FindAttribute(attributes, sdk.Registration);
        var displayNames = FindAttributes(attributes, sdk.DisplayName);
        var genres = FindAttributes(attributes, sdk.Genre);
        var author = FindAttribute(attributes, sdk.Author);
        var version = FindAttribute(attributes, sdk.Version);
        var icon = FindAttribute(attributes, sdk.Icon);

        var hasPluginAttribute = registration is not null
            || displayNames.Length > 0
            || genres.Length > 0
            || author is not null
            || version is not null
            || icon is not null;
        if (!hasPluginAttribute && !DefinesPluginClass(assembly.GlobalNamespace, sdk.Plugin))
        {
            return;
        }

        // 足りない属性の検査は、場所の分かる登録の属性に付ける(無ければ場所なし)。
        var pluginLocation = registration is null ? Location.None : GetLocation(registration);
        var entryType = AnalyzeRegistration(context, sdk, registration);

        if (CountValidLocalizedTexts(context, displayNames, DisplayNameAttributeName) == 0)
        {
            Report(context, PluginDiagnostics.MissingDisplayName, pluginLocation);
        }

        if (author is null || GetStringArgument(author, 0).Trim().Length == 0)
        {
            Report(context, PluginDiagnostics.MissingAuthor, author is null ? pluginLocation : GetLocation(author));
        }

        if (version is null || !PluginVersionRules.IsValid(GetStringArgument(version, 0)))
        {
            Report(context, PluginDiagnostics.InvalidVersion, version is null ? pluginLocation : GetLocation(version));
        }

        // 窓の口は入口の型で決まる。入口の型が無ければ、ジャンルの決まりは確かめない(登録の属性のエラーが先)。
        if (entryType is not null)
        {
            if (Implements(entryType, sdk.WindowProvider))
            {
                if (CountValidLocalizedTexts(context, genres, GenreAttributeName) == 0)
                {
                    Report(context, PluginDiagnostics.MissingGenre, pluginLocation);
                }
            }
            else if (genres.Length > 0)
            {
                Report(context, PluginDiagnostics.UnusedGenre, GetLocation(genres[0]));
            }
        }

        if (icon is not null && GetStringArgument(icon, 0).Trim().Length == 0)
        {
            Report(context, PluginDiagnostics.EmptyIconPath, GetLocation(icon));
        }
    }

    /// <summary>登録の属性(入口の型・API 版)を確かめ、入口の型を返す(型が無ければ null)。</summary>
    private static INamedTypeSymbol? AnalyzeRegistration(CompilationAnalysisContext context, SdkSymbols sdk, AttributeData? registration)
    {
        if (registration is null)
        {
            Report(context, PluginDiagnostics.MissingRegistration, Location.None);
            return null;
        }

        // 引数の数が違う呼び出しは、コンパイラーがエラーにする。
        var arguments = registration.ConstructorArguments;
        if (arguments.Length != 2)
        {
            return null;
        }

        var location = GetLocation(registration);
        if (arguments[1].Value is int apiVersion && apiVersion != sdk.CurrentApiVersion)
        {
            Report(context, PluginDiagnostics.ApiVersionMismatch, location, apiVersion, sdk.CurrentApiVersion);
        }

        if (arguments[0].Value is not INamedTypeSymbol entryType)
        {
            Report(context, PluginDiagnostics.InvalidEntryType, location, "null", "must be a class declared in this assembly");
            return null;
        }

        if (GetEntryTypeProblem(context.Compilation, sdk, entryType) is { } problem)
        {
            Report(context, PluginDiagnostics.InvalidEntryType, location, entryType.ToDisplayString(), problem);
        }

        return entryType;
    }

    private static string? GetEntryTypeProblem(Compilation compilation, SdkSymbols sdk, INamedTypeSymbol entryType)
    {
        if (!SymbolEqualityComparer.Default.Equals(entryType.ContainingAssembly, compilation.Assembly))
        {
            return "must be declared in this assembly";
        }

        if (entryType.TypeKind != TypeKind.Class
            || entryType.ContainingType is not null
            || entryType.DeclaredAccessibility != Accessibility.Public)
        {
            return "must be a public top-level class";
        }

        if (entryType.IsAbstract)
        {
            return "must not be abstract or static";
        }

        if (entryType.IsUnboundGenericType)
        {
            return "must not be an open generic type";
        }

        if (!Implements(entryType, sdk.Plugin))
        {
            return "must implement IStarResonancePlugin";
        }

        if (!entryType.InstanceConstructors.Any(constructor =>
                constructor.Parameters.Length == 0 && constructor.DeclaredAccessibility == Accessibility.Public))
        {
            return "must have a public parameterless constructor";
        }

        return null;
    }

    /// <summary>
    /// 言語ごとの文字の属性を確かめ、使われる(言語名と文字が空でない・言語が重ならない)属性の数を返す。
    /// 本体の読み込みと同じく、言語名と文字は前後の空白を除いて見て、言語名は大文字と小文字を区別しない。
    /// </summary>
    private static int CountValidLocalizedTexts(CompilationAnalysisContext context, ImmutableArray<AttributeData> attributes, string attributeName)
    {
        var cultures = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var validCount = 0;
        foreach (var attribute in attributes)
        {
            var culture = GetStringArgument(attribute, 0).Trim();
            var text = GetStringArgument(attribute, 1).Trim();
            if (culture.Length == 0 || text.Length == 0)
            {
                Report(context, PluginDiagnostics.EmptyLocalizedText, GetLocation(attribute), attributeName);
                continue;
            }

            if (!cultures.Add(culture))
            {
                Report(context, PluginDiagnostics.DuplicateCulture, GetLocation(attribute), attributeName, culture);
                continue;
            }

            validCount++;
        }

        return validCount;
    }

    private static bool DefinesPluginClass(INamespaceOrTypeSymbol container, INamedTypeSymbol pluginInterface)
    {
        foreach (var member in container.GetMembers())
        {
            switch (member)
            {
                case INamespaceSymbol childNamespace when DefinesPluginClass(childNamespace, pluginInterface):
                    return true;
                case INamedTypeSymbol type:
                    if ((type.TypeKind == TypeKind.Class && !type.IsAbstract && Implements(type, pluginInterface))
                        || DefinesPluginClass(type, pluginInterface))
                    {
                        return true;
                    }

                    break;
            }
        }

        return false;
    }

    private static bool Implements(INamedTypeSymbol type, INamedTypeSymbol interfaceType)
    {
        return type.AllInterfaces.Any(implemented => SymbolEqualityComparer.Default.Equals(implemented, interfaceType));
    }

    private static AttributeData? FindAttribute(ImmutableArray<AttributeData> attributes, INamedTypeSymbol attributeType)
    {
        return attributes.FirstOrDefault(attribute => SymbolEqualityComparer.Default.Equals(attribute.AttributeClass, attributeType));
    }

    private static ImmutableArray<AttributeData> FindAttributes(ImmutableArray<AttributeData> attributes, INamedTypeSymbol attributeType)
    {
        return attributes.Where(attribute => SymbolEqualityComparer.Default.Equals(attribute.AttributeClass, attributeType)).ToImmutableArray();
    }

    private static string GetStringArgument(AttributeData attribute, int index)
    {
        return index < attribute.ConstructorArguments.Length
            ? attribute.ConstructorArguments[index].Value as string ?? string.Empty
            : string.Empty;
    }

    private static Location GetLocation(AttributeData attribute)
    {
        return attribute.ApplicationSyntaxReference is { } syntaxReference
            ? Location.Create(syntaxReference.SyntaxTree, syntaxReference.Span)
            : Location.None;
    }

    private static void Report(CompilationAnalysisContext context, DiagnosticDescriptor descriptor, Location location, params object[] arguments)
    {
        context.ReportDiagnostic(Diagnostic.Create(descriptor, location, arguments));
    }

    /// <summary>コンパイルが参照している SDK の型。SDK を参照していなければ作らない(確かめるものが無い)。</summary>
    private sealed class SdkSymbols
    {
        private const string SdkNamespace = "StarResonanceDps.PluginSdk.";

        private SdkSymbols(
            INamedTypeSymbol registration,
            INamedTypeSymbol displayName,
            INamedTypeSymbol genre,
            INamedTypeSymbol author,
            INamedTypeSymbol version,
            INamedTypeSymbol icon,
            INamedTypeSymbol plugin,
            INamedTypeSymbol windowProvider,
            int currentApiVersion)
        {
            Registration = registration;
            DisplayName = displayName;
            Genre = genre;
            Author = author;
            Version = version;
            Icon = icon;
            Plugin = plugin;
            WindowProvider = windowProvider;
            CurrentApiVersion = currentApiVersion;
        }

        public INamedTypeSymbol Registration { get; }

        public INamedTypeSymbol DisplayName { get; }

        public INamedTypeSymbol Genre { get; }

        public INamedTypeSymbol Author { get; }

        public INamedTypeSymbol Version { get; }

        public INamedTypeSymbol Icon { get; }

        public INamedTypeSymbol Plugin { get; }

        public INamedTypeSymbol WindowProvider { get; }

        public int CurrentApiVersion { get; }

        public static SdkSymbols? Find(Compilation compilation)
        {
            var registration = compilation.GetTypeByMetadataName(SdkNamespace + "PluginRegistrationAttribute");
            var displayName = compilation.GetTypeByMetadataName(SdkNamespace + "PluginDisplayNameAttribute");
            var genre = compilation.GetTypeByMetadataName(SdkNamespace + "PluginGenreAttribute");
            var author = compilation.GetTypeByMetadataName(SdkNamespace + "PluginAuthorAttribute");
            var version = compilation.GetTypeByMetadataName(SdkNamespace + "PluginVersionAttribute");
            var icon = compilation.GetTypeByMetadataName(SdkNamespace + "PluginIconAttribute");
            var plugin = compilation.GetTypeByMetadataName(SdkNamespace + "IStarResonancePlugin");
            var windowProvider = compilation.GetTypeByMetadataName(SdkNamespace + "IPluginWindowProvider");
            var currentApiVersion = compilation.GetTypeByMetadataName(SdkNamespace + "PluginSdkVersion")?
                .GetMembers("Current")
                .OfType<IFieldSymbol>()
                .FirstOrDefault()?
                .ConstantValue as int?;

            return registration is null
                || displayName is null
                || genre is null
                || author is null
                || version is null
                || icon is null
                || plugin is null
                || windowProvider is null
                || currentApiVersion is null
                ? null
                : new SdkSymbols(registration, displayName, genre, author, version, icon, plugin, windowProvider, currentApiVersion.Value);
        }
    }
}
