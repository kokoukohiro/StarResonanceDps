using Serilog;
using StarResonanceDps.App.Config;
using StarResonanceDps.App.Localization;
using StarResonanceDps.App.Services;
using StarResonanceDps.Core.CombatRuntime;

namespace StarResonanceDps.App.Models.Widgets;

/// <summary>
/// プレイヤーリストの通知「マッチング成立」。マッチングが成立して承諾待ちになるたびに出す(重なりは消さない)。
/// プレイヤーリストを開いている間だけ呼ぶ(起動中のウィジェットだけが通知する)。
///
/// <para>
/// {Content} はマッチング先のコンテンツ名。引けなかった(表に無い番号・種類が分からない)ときは「不明」にして出し、ログに残す。
/// </para>
/// </summary>
public sealed class MatchFoundNotifier
{
    private readonly Func<MeterWidgetSettingsConfig> _getSettings;

    public MatchFoundNotifier(Func<MeterWidgetSettingsConfig> getSettings)
    {
        _getSettings = getSettings;
    }

    public void Notify(Zproto.EMatchType matchType, long matchTypeUuid)
    {
        var format = WidgetNotificationTextFormatter.ResolveFormat(
            _getSettings().MatchFoundNotificationFormatString,
            WidgetNotificationTextFormatter.MatchFoundDefaultKey);
        if (WidgetNotificationTextFormatter.IsOff(format))
        {
            return;
        }

        var content = CombatDataCatalog.GetMatchTargetName(matchType, matchTypeUuid);
        if (string.IsNullOrEmpty(content))
        {
            Log.Warning("No content name for the match target: {MatchType} {MatchTypeUuid}", matchType, matchTypeUuid);
            content = LocalizationManager.Instance.GetString("PlayerInfo_Unknown");
        }

        NotificationService.Instance.Notify(WidgetNotificationTextFormatter.FormatMatchFound(format, content));
    }
}
