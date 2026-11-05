using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using StarResonanceDps.PluginSdk;

namespace StarResonanceDps.App.Services;

/// <summary>
/// プラグインが渡したウィジェットの定義を確かめ、本体が持つ写しを作る。
/// 決まりは SDK の <see cref="PluginWidgetDefinition"/> と <see cref="PluginWidgetSettingSection"/> の説明と同じ。
/// 決まりに合わなければ、理由(ログに書く英語)を返し、本体はそのプラグインを止める。
/// </summary>
internal static class PluginWidgetValidator
{
    /// <summary>節のアイコンに使える形の鍵(SDK の公開の一覧のうち、描き方の2つを除いたもの)。</summary>
    private static readonly HashSet<string> IconKeys = typeof(PluginResourceKeys.Icons)
        .GetFields(BindingFlags.Public | BindingFlags.Static)
        .Where(field => field.IsLiteral && field.FieldType == typeof(string))
        .Select(field => (string)field.GetRawConstantValue()!)
        .Where(key => key is not PluginResourceKeys.Icons.StrokeStyle and not PluginResourceKeys.Icons.FillStyle)
        .ToHashSet(StringComparer.Ordinal);

    public static bool TryValidate(
        IReadOnlyList<PluginWidgetDefinition>? definitions,
        [NotNullWhen(true)] out IReadOnlyList<PluginWidgetDefinition>? copies,
        [NotNullWhen(false)] out string? error)
    {
        copies = null;
        if (definitions is null)
        {
            error = "GetWidgetDefinitions returned null.";
            return false;
        }

        var result = new List<PluginWidgetDefinition>(definitions.Count);
        var ids = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var definition in definitions)
        {
            if (definition is null)
            {
                error = "A widget definition is null.";
                return false;
            }

            if (!PluginIdRules.IsValid(definition.Id))
            {
                error = $"The widget ID '{definition.Id}' is missing or contains unsupported characters.";
                return false;
            }

            var id = definition.Id.Trim();
            if (!ids.Add(id))
            {
                error = $"The widget ID '{id}' is defined more than once.";
                return false;
            }

            if (!PluginSettingValidator.TryCopyRequiredTexts(definition.Names, out var names))
            {
                error = $"The widget '{id}' needs at least one name, and each culture only once.";
                return false;
            }

            if (!IsFinitePositive(definition.DefaultWidth) || !IsFinitePositive(definition.DefaultHeight))
            {
                error = $"The widget '{id}' needs a finite default size above 0.";
                return false;
            }

            if (!TryCopySections(definition.SettingSections, out var sections, out var sectionError))
            {
                error = $"The widget '{id}': {sectionError}";
                return false;
            }

            result.Add(new PluginWidgetDefinition
            {
                Id = id,
                Names = names,
                DefaultWidth = definition.DefaultWidth,
                DefaultHeight = definition.DefaultHeight,
                HasFooter = definition.HasFooter,
                ScrollsVertically = definition.ScrollsVertically,
                SettingSections = sections
            });
        }

        copies = result;
        error = null;
        return true;
    }

    private static bool TryCopySections(
        IReadOnlyList<PluginWidgetSettingSection>? sections,
        out IReadOnlyList<PluginWidgetSettingSection> copies,
        [NotNullWhen(false)] out string? error)
    {
        var result = new List<PluginWidgetSettingSection>();
        copies = result;
        var settingIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var section in sections ?? [])
        {
            if (section is null)
            {
                error = "a setting section is null.";
                return false;
            }

            if (!PluginSettingValidator.TryCopyRequiredTexts(section.Names, out var names))
            {
                error = "a setting section needs at least one name, and each culture only once.";
                return false;
            }

            if (section.Icon is null || !IconKeys.Contains(section.Icon))
            {
                error = $"the setting section icon '{section.Icon}' is not one of PluginResourceKeys.Icons.";
                return false;
            }

            if (section.IconStyle is not PluginResourceKeys.Icons.StrokeStyle and not PluginResourceKeys.Icons.FillStyle)
            {
                error = $"the setting section icon style '{section.IconStyle}' must be PluginResourceKeys.Icons.StrokeStyle or FillStyle.";
                return false;
            }

            if (!PluginSettingValidator.TryCopySettings(section.Settings, allowsColor: true, settingIds, out var settings, out var settingError))
            {
                error = settingError;
                return false;
            }

            if (settings.Count == 0)
            {
                error = "a setting section needs at least one setting.";
                return false;
            }

            result.Add(new PluginWidgetSettingSection
            {
                Names = names,
                Icon = section.Icon,
                IconStyle = section.IconStyle,
                Settings = settings
            });
        }

        error = null;
        return true;
    }

    private static bool IsFinitePositive(double value)
    {
        return double.IsFinite(value) && value > 0;
    }
}
