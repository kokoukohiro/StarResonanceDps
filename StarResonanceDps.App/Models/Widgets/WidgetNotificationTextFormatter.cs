using System.Text.RegularExpressions;
using StarResonanceDps.App.Localization;

namespace StarResonanceDps.App.Models.Widgets;

/// <summary>
/// ウィジェットの通知の文章(バフ・デバフカードの通知テキスト、被ダメログの予兆技、プレイヤーリストの通知テキスト)。設定画面のプレビューと通知が同じ組み立てを使う。
///
/// <para>
/// 保存した値が null なら表示言語の既定の文、空(空白だけを含む)なら通知しない。
/// 差し込める項目は、カードが {Name}(対象の名前)と {BuffName}(カードのバフ名)、予兆技が {Name}(ログの行と同じ名前)と {SkillName}、
/// マッチング成立が {Content}(マッチング先のコンテンツ名)、HP低下が {Name}(プレイヤーリストの行と同じ名前)。
/// </para>
/// </summary>
public static partial class WidgetNotificationTextFormatter
{
    public const string BuffCardExpiredDefaultKey = "BuffCardNotification_Expired_Default";
    public const string BuffCardCuisinePotionLowDefaultKey = "BuffCardNotification_CuisinePotionLow_Default";
    public const string TelegraphedSkillDefaultKey = "TakenDamageLogNotification_TelegraphedSkill_Default";
    public const string MatchFoundDefaultKey = "PlayerListNotification_MatchFound_Default";
    public const string HealthLowDefaultKey = "PlayerListNotification_HealthLow_Default";

    /// <summary>保存した値から使う書式を決める。null なら表示言語の既定の文。</summary>
    public static string ResolveFormat(string? savedValue, string defaultKey)
    {
        return savedValue ?? LocalizationManager.Instance.GetString(defaultKey);
    }

    /// <summary>通知しない書式(空欄)か。</summary>
    public static bool IsOff(string format)
    {
        return string.IsNullOrWhiteSpace(format);
    }

    public static string FormatBuffCard(string format, string name, string buffName)
    {
        return GetBuffCardFieldRegex().Replace(
            format,
            match => string.Equals(match.Groups[1].Value, "Name", StringComparison.OrdinalIgnoreCase) ? name : buffName);
    }

    public static string FormatTelegraphedSkill(string format, string name, string skillName)
    {
        return GetTelegraphedSkillFieldRegex().Replace(
            format,
            match => string.Equals(match.Groups[1].Value, "Name", StringComparison.OrdinalIgnoreCase) ? name : skillName);
    }

    public static string FormatMatchFound(string format, string content)
    {
        return GetMatchFoundFieldRegex().Replace(format, _ => content);
    }

    public static string FormatHealthLow(string format, string name)
    {
        return GetHealthLowFieldRegex().Replace(format, _ => name);
    }

    /// <summary>設定画面の見本(効果時間切れ)。値は実データではなく、書式の形を見せるためのもの。</summary>
    public static string FormatBuffCardExpiredPreview(string format)
    {
        var localization = LocalizationManager.Instance;
        return FormatBuffCard(
            format,
            localization.GetString("Settings_BuffInfo_PreviewName"),
            localization.GetString("Settings_BuffInfo_PreviewBuffName"));
    }

    /// <summary>設定画面の見本(薬剤・料理バフ2分以下)。バフ名は料理・薬剤のまとまりの名前になるので「料理」を見せる。</summary>
    public static string FormatBuffCardCuisinePotionLowPreview(string format)
    {
        var localization = LocalizationManager.Instance;
        return FormatBuffCard(
            format,
            localization.GetString("Settings_BuffInfo_PreviewName"),
            localization.GetString("Widget_BuffDebuffCard_Group_Cuisine"));
    }

    /// <summary>設定画面の見本(予兆技)。値は実データではなく、書式の形を見せるためのもの。</summary>
    public static string FormatTelegraphedSkillPreview(string format)
    {
        var localization = LocalizationManager.Instance;
        return FormatTelegraphedSkill(
            format,
            localization.GetString("Settings_EntityInfo_PreviewName"),
            localization.GetString("Settings_SkillInfo_PreviewName"));
    }

    /// <summary>設定画面の見本(マッチング成立)。値は実データではなく、書式の形を見せるためのもの。</summary>
    public static string FormatMatchFoundPreview(string format)
    {
        return FormatMatchFound(format, LocalizationManager.Instance.GetString("Settings_PlayerListNotification_PreviewContent"));
    }

    /// <summary>設定画面の見本(HP低下)。名前はプレイヤー名の書式の見本と同じ。</summary>
    public static string FormatHealthLowPreview(string format)
    {
        return FormatHealthLow(format, LocalizationManager.Instance.GetString("Settings_PlayerInfo_PreviewName"));
    }

    [GeneratedRegex(@"\{(Name|BuffName)\}", RegexOptions.IgnoreCase)]
    private static partial Regex GetBuffCardFieldRegex();

    [GeneratedRegex(@"\{(Name|SkillName)\}", RegexOptions.IgnoreCase)]
    private static partial Regex GetTelegraphedSkillFieldRegex();

    [GeneratedRegex(@"\{Content\}", RegexOptions.IgnoreCase)]
    private static partial Regex GetMatchFoundFieldRegex();

    [GeneratedRegex(@"\{Name\}", RegexOptions.IgnoreCase)]
    private static partial Regex GetHealthLowFieldRegex();
}
