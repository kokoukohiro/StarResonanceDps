using System.Text.RegularExpressions;
using StarResonanceDps.App.Config;
using StarResonanceDps.App.Localization;

namespace StarResonanceDps.App.Models.Notifications;

/// <summary>
/// 通知タブの項目の文章。通知タブのプレビューと通知が同じ組み立てを使う。
///
/// <para>
/// 保存した値が null なら表示言語の既定の文(空欄は保存の時点で null にしている)。
/// 差し込める項目は、バフ・デバフ効果切れが {Name}(対象の名前)と {BuffName}(カードのバフ名)と {Caster}(付与者プレイヤー名)、
/// 薬剤・料理効果切れが {Name} と {BuffName}、
/// 予兆技が {Name}(タイムラインの行と同じ名前)と {SkillName}、マッチング成立が {Content}(マッチング先のコンテンツ名)、
/// HP低下とプレイヤー出現が {Name}(プレイヤーリストの行と同じ名前)、エンティティ出現が {Name}(内部IDの注記なしのエンティティ名)。
/// </para>
/// </summary>
public static partial class NotificationTextFormatter
{
    /// <summary>項目の既定の文の鍵。</summary>
    public static string GetDefaultKey(NotificationKind kind)
    {
        return kind switch
        {
            NotificationKind.MatchFound => "PlayerListNotification_MatchFound_Default",
            NotificationKind.HealthLow => "PlayerListNotification_HealthLow_Default",
            NotificationKind.PlayerAppeared => "PlayerAppearedNotification_Default",
            NotificationKind.EntityAppeared => "EntityAppearedNotification_Default",
            NotificationKind.BuffExpired => "BuffCardNotification_Expired_Default",
            NotificationKind.CuisinePotionLow => "BuffCardNotification_CuisinePotionLow_Default",
            NotificationKind.TelegraphedSkill => "TakenDamageLogNotification_TelegraphedSkill_Default",
            _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, "No default text for the notification.")
        };
    }

    /// <summary>今の設定で使う書式。保存した値が無ければ表示言語の既定の文。</summary>
    public static string ResolveFormat(NotificationKind kind)
    {
        return ConfigManager.Instance.GetNotificationItem(kind).FormatString
            ?? LocalizationManager.Instance.GetString(GetDefaultKey(kind));
    }

    /// <summary>
    /// バフの通知の文。付与者プレイヤー名は付与者がプレイヤーでなければ空で、空になって残った <c>()</c> <c>[]</c> はバフ・デバフカードと同じく消す
    /// (<see cref="Widgets.BuffInfoFormatFormatter"/>)。通知の文は改行を持つので、空白は詰めない。
    /// </summary>
    public static string FormatBuff(string format, string name, string buffName, string casterName)
    {
        var result = GetBuffFieldRegex().Replace(
            format,
            match => match.Groups[1].Value.ToUpperInvariant() switch
            {
                "NAME" => name,
                "CASTER" => casterName,
                _ => buffName
            });
        result = GetEmptyParenthesisRegex().Replace(result, string.Empty);
        return GetEmptyBracketRegex().Replace(result, string.Empty);
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
        return GetNameFieldRegex().Replace(format, _ => name);
    }

    /// <summary>プレイヤー出現・エンティティ出現の文。差し込めるのは {Name}(出現した相手の名前)だけ。</summary>
    public static string FormatAppeared(string format, string name)
    {
        return GetNameFieldRegex().Replace(format, _ => name);
    }

    /// <summary>通知タブの見本。値は実データではなく、書式の形を見せるためのもの。</summary>
    public static string FormatPreview(NotificationKind kind, string format)
    {
        var localization = LocalizationManager.Instance;
        return kind switch
        {
            NotificationKind.MatchFound => FormatMatchFound(
                format,
                localization.GetString("Settings_PlayerListNotification_PreviewContent")),

            // 名前はプレイヤー名の書式の見本と同じ。
            NotificationKind.HealthLow => FormatHealthLow(format, localization.GetString("Settings_PlayerInfo_PreviewName")),
            NotificationKind.PlayerAppeared => FormatAppeared(format, localization.GetString("Settings_PlayerInfo_PreviewName")),
            NotificationKind.EntityAppeared => FormatAppeared(format, localization.GetString("Settings_EntityInfo_PreviewName")),
            NotificationKind.BuffExpired => FormatBuff(
                format,
                localization.GetString("Settings_BuffInfo_PreviewName"),
                localization.GetString("Settings_BuffInfo_PreviewBuffName"),
                localization.GetString("Settings_BuffInfo_PreviewCaster")),

            // バフ名は料理・薬剤のまとまりの名前になるので「料理」を見せる。
            NotificationKind.CuisinePotionLow => FormatBuff(
                format,
                localization.GetString("Settings_BuffInfo_PreviewName"),
                localization.GetString("Widget_BuffDebuffCard_Group_Cuisine"),
                localization.GetString("Settings_BuffInfo_PreviewCaster")),
            NotificationKind.TelegraphedSkill => FormatTelegraphedSkill(
                format,
                localization.GetString("Settings_EntityInfo_PreviewName"),
                localization.GetString("Settings_SkillInfo_PreviewName")),
            _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, "No preview for the notification.")
        };
    }

    [GeneratedRegex(@"\{(Name|BuffName|Caster)\}", RegexOptions.IgnoreCase)]
    private static partial Regex GetBuffFieldRegex();

    [GeneratedRegex(@"\(\s*\)")]
    private static partial Regex GetEmptyParenthesisRegex();

    [GeneratedRegex(@"\[\s*\]")]
    private static partial Regex GetEmptyBracketRegex();

    [GeneratedRegex(@"\{(Name|SkillName)\}", RegexOptions.IgnoreCase)]
    private static partial Regex GetTelegraphedSkillFieldRegex();

    [GeneratedRegex(@"\{Content\}", RegexOptions.IgnoreCase)]
    private static partial Regex GetMatchFoundFieldRegex();

    [GeneratedRegex(@"\{Name\}", RegexOptions.IgnoreCase)]
    private static partial Regex GetNameFieldRegex();
}
