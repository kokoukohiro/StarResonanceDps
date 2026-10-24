using Serilog;
using StarResonanceDps.App.Config;
using StarResonanceDps.App.Localization;
using StarResonanceDps.App.Services;
using StarResonanceDps.Core.CombatRuntime;

namespace StarResonanceDps.App.Models.Notifications;

/// <summary>
/// 通知「マッチング成立」。マッチングが成立して承諾待ちになるたびに出す(重なりは消さない)。
/// ウィジェットを開いていなくても、通知がオンなら <see cref="NotificationMonitor"/> が呼ぶ。
///
/// <para>
/// {Content} はマッチング先のコンテンツ名。引けなかった(表に無い番号・種類が分からない)ときは「不明」にして出し、ログに残す。
/// </para>
/// </summary>
public static class MatchFoundNotifier
{
    public static void Notify(Zproto.EMatchType matchType, long matchTypeUuid)
    {
        var content = CombatDataCatalog.GetMatchTargetName(matchType, matchTypeUuid);
        if (string.IsNullOrEmpty(content))
        {
            Log.Warning("No content name for the match target: {MatchType} {MatchTypeUuid}", matchType, matchTypeUuid);
            content = LocalizationManager.Instance.GetString("PlayerInfo_Unknown");
        }

        NotificationService.Instance.Notify(
            NotificationKind.MatchFound,
            NotificationTextFormatter.FormatMatchFound(NotificationTextFormatter.ResolveFormat(NotificationKind.MatchFound), content));
    }
}
