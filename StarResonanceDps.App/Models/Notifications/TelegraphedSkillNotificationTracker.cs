using StarResonanceDps.App.Config;
using StarResonanceDps.App.Localization;
using StarResonanceDps.App.Models.Widgets;
using StarResonanceDps.App.Services;
using StarResonanceDps.Core.CombatRuntime;
using StarResonanceDps.Core.Services;

namespace StarResonanceDps.App.Models.Notifications;

/// <summary>
/// 通知「予兆技」の判定。ウィジェットを開いていなくても、通知がオンの間 <see cref="NotificationMonitor"/> が一定の間隔で <see cref="Poll"/> を呼ぶ。
///
/// <para>
/// <b>予告</b>はゲームの予告のバーが消える時刻(<see cref="BossDbmBarStore"/>)に、<b>警告</b>は警告の技の開始が届いたとき
/// (<see cref="WarningSkillCastStore"/>)に出す。同じ技に予告と警告の両方があっても、それぞれの時機に別々に出す。
/// どちらもライブの戦闘で決め、タイムラインが履歴を表示していても、計測の窓の外で記録が止まっていても止まらない。
/// 判定を始めた時点(通知をオンにした・アプリを起動した)より前のものは出さない。
/// </para>
///
/// <para>
/// 名前とスキル名はタイムラインの行と同じ文字(名前の無い技は行と同じ「何か」)。内部ID注記は付けない。
/// </para>
/// </summary>
public sealed class TelegraphedSkillNotificationTracker
{
    private DateTime _lastBarCheckUtc = DateTime.UtcNow;

    // 判定を始めた時点までに控えた警告は出さない。
    private long _lastWarningSequence = WarningSkillCastStore.Instance.LastSequence;

    public void Poll()
    {
        var now = DateTime.UtcNow;
        foreach (var bar in BossDbmBarStore.Instance.GetEndedBetween(_lastBarCheckUtc, now))
        {
            Notify(
                PlayerInfoFormatFormatter.GetPartyDisplayName(MeterSnapshotProvider.CreateTakenDamageLogAnnouncementParty(bar.OwnerMonsterId)),
                MeterSnapshotProvider.GetTakenDamageLogSkillName(bar.SkillId));
        }

        _lastBarCheckUtc = now;

        foreach (var cast in WarningSkillCastStore.Instance.GetAfter(_lastWarningSequence))
        {
            _lastWarningSequence = cast.Sequence;
            Notify(PlayerInfoFormatFormatter.GetPartyDisplayName(cast.Caster), MeterSnapshotProvider.GetTakenDamageLogSkillName(cast.SkillId));
        }
    }

    private static void Notify(string name, string skillName)
    {
        if (string.IsNullOrEmpty(skillName))
        {
            skillName = LocalizationManager.Instance.GetString("TakenDamageLog_UnnamedCastSkill");
        }

        NotificationService.Instance.Notify(
            NotificationKind.TelegraphedSkill,
            NotificationTextFormatter.FormatTelegraphedSkill(
                NotificationTextFormatter.ResolveFormat(NotificationKind.TelegraphedSkill),
                name,
                skillName));
    }
}
