using StarResonanceDps.App.Config;
using StarResonanceDps.App.Localization;
using StarResonanceDps.App.Services;
using StarResonanceDps.Core.CombatRuntime;
using StarResonanceDps.Core.Services;

namespace StarResonanceDps.App.Models.Widgets;

/// <summary>
/// 被ダメログの通知(予兆技)の判定。被ダメログの窓の更新ごとに <see cref="Poll"/> を呼ぶ(起動中のウィジェットだけが通知する)。
///
/// <para>
/// <b>予告</b>はゲームの予告のバーが消える時刻(<see cref="BossDbmBarStore"/>)に、<b>警告</b>は警告の技の開始が届いたとき
/// (<see cref="WarningSkillCastStore"/>)に出す。同じ技に予告と警告の両方があっても、それぞれの時機に別々に出す。
/// どちらもライブの戦闘で決め、被ダメログが履歴を表示していても、計測の窓の外で記録が止まっていても止まらない。
/// 窓を開いた時点で既に過ぎたものは出さない。
/// </para>
///
/// <para>
/// 名前とスキル名はログの行と同じ文字(名前の無い技は行と同じ「何か」)。内部ID注記は付けない。
/// </para>
/// </summary>
public sealed class TelegraphedSkillNotificationTracker
{
    private readonly Func<TakenDamageLogWidgetSettingsConfig> _getSettings;
    private readonly Func<TakenDamageLogParty, string> _getDisplayName;

    private DateTime _lastBarCheckUtc = DateTime.UtcNow;

    // 窓を開いた時点までに控えた警告は出さない。
    private long _lastWarningSequence = WarningSkillCastStore.Instance.LastSequence;

    /// <param name="getSettings">被ダメログの設定(保存前プレビューを含む)。</param>
    /// <param name="getDisplayName">ログの行の名前の出し方(被ダメログの窓と同じ)。</param>
    public TelegraphedSkillNotificationTracker(
        Func<TakenDamageLogWidgetSettingsConfig> getSettings,
        Func<TakenDamageLogParty, string> getDisplayName)
    {
        _getSettings = getSettings;
        _getDisplayName = getDisplayName;
    }

    public void Poll()
    {
        var now = DateTime.UtcNow;
        foreach (var bar in BossDbmBarStore.Instance.GetEndedBetween(_lastBarCheckUtc, now))
        {
            Notify(
                _getDisplayName(MeterSnapshotProvider.CreateTakenDamageLogAnnouncementParty(bar.OwnerMonsterId)),
                MeterSnapshotProvider.GetTakenDamageLogSkillName(bar.SkillId));
        }

        _lastBarCheckUtc = now;

        foreach (var cast in WarningSkillCastStore.Instance.GetAfter(_lastWarningSequence))
        {
            _lastWarningSequence = cast.Sequence;
            Notify(_getDisplayName(cast.Caster), MeterSnapshotProvider.GetTakenDamageLogSkillName(cast.SkillId));
        }
    }

    private void Notify(string name, string skillName)
    {
        var format = WidgetNotificationTextFormatter.ResolveFormat(
            _getSettings().TelegraphedSkillNotificationFormatString,
            WidgetNotificationTextFormatter.TelegraphedSkillDefaultKey);
        if (WidgetNotificationTextFormatter.IsOff(format))
        {
            return;
        }

        if (string.IsNullOrEmpty(skillName))
        {
            skillName = LocalizationManager.Instance.GetString("TakenDamageLog_UnnamedCastSkill");
        }

        NotificationService.Instance.Notify(WidgetNotificationTextFormatter.FormatTelegraphedSkill(format, name, skillName));
    }
}
