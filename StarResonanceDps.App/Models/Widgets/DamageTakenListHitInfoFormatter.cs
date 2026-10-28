using System.Text.RegularExpressions;
using StarResonanceDps.App.Localization;

namespace StarResonanceDps.App.Models.Widgets;

/// <summary>
/// 被ダメリストの右の書式(設定「スキル名」)。差し込める項目は スキル名 / エンティティ名 / タイプ。
/// </summary>
public static partial class DamageTakenListHitInfoFormatter
{
    /// <summary>設定の差し込む項目の一覧。項目名は既存の文言(スキル詳細の列の見出し・エンティティリストの名前)を使う。</summary>
    public static IReadOnlyList<(string Key, string LabelResourceKey, string Placeholder)> FieldDefinitions { get; } =
    [
        ("SkillName", "Metric_SkillName", "{SkillName}"),
        ("EntityName", "Settings_EntityInfo_Field_Name", "{EntityName}"),
        ("Type", "Metric_DamageMode", "{Type}")
    ];

    /// <summary>
    /// 書式に値を差し込む。項目の名前は大文字と小文字を区別しない(スキル詳細の書式と同じ)。
    /// 1回の置き換えで全部の項目を埋めるので、値の中の <c>{Type}</c> などは置き換えない。
    /// </summary>
    public static string Format(string? formatString, string skillName, string entityName, string typeText)
    {
        if (string.IsNullOrEmpty(formatString))
        {
            return string.Empty;
        }

        return GetFieldRegex().Replace(formatString, match => match.Groups[1].Value.ToUpperInvariant() switch
        {
            "SKILLNAME" => skillName,
            "ENTITYNAME" => entityName,
            _ => typeText
        });
    }

    /// <summary>設定画面の見本。値は実データではなく、書式の形を見せるためのもの(スキル詳細・エンティティリストの見本と同じ)。</summary>
    public static string FormatPreview(string? formatString)
    {
        var localization = LocalizationManager.Instance;
        return Format(
            formatString,
            localization.GetString("Settings_SkillInfo_PreviewName"),
            localization.GetString("Settings_EntityInfo_PreviewName"),
            localization.GetString("Metric_DamageMode_Physical"));
    }

    [GeneratedRegex(@"\{(SkillName|EntityName|Type)\}", RegexOptions.IgnoreCase)]
    private static partial Regex GetFieldRegex();
}
