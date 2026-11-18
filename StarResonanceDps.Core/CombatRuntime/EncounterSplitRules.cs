// This file is part of StarResonanceDps. See LICENSING.md for the license, additional permissions and additional terms.

namespace StarResonanceDps.Core.CombatRuntime;

/// <summary>
/// プラグインが預けた、回の区切りと戦闘の時計の規則(全部のプラグインを合わせた1組)。合わせるのは App のプラグインの規則の一覧で、ここは当てるだけ。
///
/// <para>
/// 書き換え(<see cref="Apply"/>)も読み(区切りの判定・回を作るときの時計)も、パケットのスレッドでメッセージとメッセージのあいだに行う。
/// キャプチャが動いていない間は画面のスレッドで書く(手動のリセットと設定の書き込みと同じスレッド)。
/// </para>
/// </summary>
public static class EncounterSplitRules
{
    private static EncounterSplitRuleSet _rules = EncounterSplitRuleSet.Empty;

    private static readonly object TimerGate = new();
    private static Timer? _conditionTimer;

    /// <summary>今当てている規則。</summary>
    public static EncounterSplitRuleSet Rules => Volatile.Read(ref _rules);

    /// <summary>規則を当てる。条件(無操作・経過の秒)が1つでもある間だけ、1秒ごとの判定を動かす。</summary>
    public static void Apply(EncounterSplitRuleSet rules)
    {
        ArgumentNullException.ThrowIfNull(rules);
        Volatile.Write(ref _rules, rules);

        lock (TimerGate)
        {
            if (rules.Conditions.Count == 0)
            {
                _conditionTimer?.Dispose();
                _conditionTimer = null;
            }
            else
            {
                _conditionTimer ??= new Timer(_ => EnqueueConditionCheck(), null, TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(1));
            }
        }
    }

    /// <summary>
    /// ダンジョンの進行で区切るか。上書きが無ければ全体設定「進行で自動リセット」。
    /// ダンジョンの時計があれば開始では必ず区切り、ダンジョンの時計の回の中ではボス部屋・全滅で区切らない。
    /// </summary>
    public static bool ShouldSplit(EncounterSplitTrigger trigger)
    {
        var rules = Rules;
        return trigger switch
        {
            EncounterSplitTrigger.DungeonStart => rules.DungeonClock != EncounterClockOrigin.FirstCombatEvent || Resolve(rules.DungeonStartSplit),
            EncounterSplitTrigger.BossRoom => !EncounterManager.Current.HasDungeonClock && Resolve(rules.BossRoomSplit),
            EncounterSplitTrigger.Wipe => !EncounterManager.Current.HasDungeonClock && Resolve(rules.WipeSplit),
            _ => throw new ArgumentOutOfRangeException(nameof(trigger), trigger, null),
        };
    }

    /// <summary>
    /// パケットの中身を処理する直前(<c>NetCap.BeforeParsePacket</c>)。条件の期限が到着時刻までに来ていれば区切り、
    /// 予告のバーの終わりは期限までを前の回へ、残りを新しい回へ残す。
    /// </summary>
    public static void BeforeParsePacket(DateTime arrivalUtc)
    {
        SplitIfDue(arrivalUtc);
        EncounterManager.RecordEndedAnnouncementBars(arrivalUtc);
    }

    private static bool Resolve(EncounterSplitOverride mode)
    {
        return mode switch
        {
            EncounterSplitOverride.HostSetting => CombatRuntimeSettings.SplitEncountersOnNewPhases,
            EncounterSplitOverride.Split => true,
            EncounterSplitOverride.DoNotSplit => false,
            _ => throw new ArgumentOutOfRangeException(nameof(mode), mode, null),
        };
    }

    /// <summary>1秒ごとの判定。判定の時刻は積んだ時刻(その後に届くパケットより前)。キャプチャが動いていなければ積まない。</summary>
    private static void EnqueueConditionCheck()
    {
        var queuedAt = DateTime.UtcNow;
        MessageManager.netCap?.TryEnqueueCommand(() => SplitIfDue(queuedAt));
    }

    private static void SplitIfDue(DateTime utc)
    {
        var rules = Rules;
        if (rules.Conditions.Count == 0 || !TryGetDue(rules, out var deadline, out _) || deadline > utc)
        {
            return;
        }

        // 期限までに終わった予告のバーは前の回のもの。バーの終わりも戦闘の出来事なので、期限が延びることがある。
        EncounterManager.RecordEndedAnnouncementBars(deadline);
        if (!TryGetDue(rules, out deadline, out var keepPastEncounterInMeter) || deadline > utc)
        {
            return;
        }

        EncounterManager.SplitOnCondition(keepPastEncounterInMeter);
    }

    /// <summary>
    /// 今の回で一番早く来る条件の期限。判定しない回(計測の回・閉じた回・時計がまだ動いていない回・ダンジョンの時計の回)は false。
    /// 無操作は最後の戦闘の出来事 + 秒、経過は最初の戦闘の出来事 + 秒(実時間)。
    /// </summary>
    private static bool TryGetDue(EncounterSplitRuleSet rules, out DateTime deadline, out bool keepPastEncounterInMeter)
    {
        deadline = DateTime.MaxValue;
        keepPastEncounterInMeter = false;
        var encounter = EncounterManager.Current;
        if (EncounterManager.IsBenchmarkActive
            || encounter.EndTime != DateTime.MinValue
            || encounter.HasDungeonClock
            || encounter.ExData.FirstDamageTimeStamp is not { } first)
        {
            return false;
        }

        var last = encounter.ReadLastCombatEventUtc();
        var found = false;
        foreach (var condition in rules.Conditions)
        {
            if (condition.IdleSeconds > 0 && last is { } lastEvent)
            {
                Consider(lastEvent.AddSeconds(condition.IdleSeconds), condition.KeepPastEncounterInMeter, ref deadline, ref keepPastEncounterInMeter, ref found);
            }

            if (condition.IntervalSeconds > 0)
            {
                Consider(first.AddSeconds(condition.IntervalSeconds), condition.KeepPastEncounterInMeter, ref deadline, ref keepPastEncounterInMeter, ref found);
            }
        }

        return found;
    }

    private static void Consider(DateTime candidate, bool keep, ref DateTime deadline, ref bool keepPastEncounterInMeter, ref bool found)
    {
        if (!found || candidate < deadline)
        {
            deadline = candidate;
            keepPastEncounterInMeter = keep;
            found = true;
        }
    }
}

/// <summary>全部のプラグインを合わせた規則。</summary>
/// <param name="Conditions">プラグインごとの無操作・経過の秒。</param>
/// <param name="DungeonStartSplit">ダンジョンの開始の区切り。</param>
/// <param name="BossRoomSplit">ボス部屋の入場の区切り。</param>
/// <param name="WipeSplit">全滅の区切り。</param>
/// <param name="DungeonClock">ダンジョンの時計の起点。<see cref="EncounterClockOrigin.FirstCombatEvent"/> は使わない。</param>
public sealed record EncounterSplitRuleSet(
    IReadOnlyList<EncounterSplitCondition> Conditions,
    EncounterSplitOverride DungeonStartSplit,
    EncounterSplitOverride BossRoomSplit,
    EncounterSplitOverride WipeSplit,
    EncounterClockOrigin DungeonClock)
{
    public static EncounterSplitRuleSet Empty { get; } = new(
        [],
        EncounterSplitOverride.HostSetting,
        EncounterSplitOverride.HostSetting,
        EncounterSplitOverride.HostSetting,
        EncounterClockOrigin.FirstCombatEvent);
}

/// <summary>1つのプラグインの無操作・経過の秒(0 は使わない)と、区切った後に前の回をメーターに残すか。</summary>
public readonly record struct EncounterSplitCondition(int IdleSeconds, int IntervalSeconds, bool KeepPastEncounterInMeter);

/// <summary>ダンジョンの進行で区切るか。</summary>
public enum EncounterSplitOverride
{
    /// <summary>全体設定「進行で自動リセット」のまま。</summary>
    HostSetting = 0,

    Split,

    DoNotSplit,
}

/// <summary>回を作るきっかけ(ダンジョンの時計を付けるかの判定に使う)。</summary>
public enum EncounterSplitTrigger
{
    /// <summary>上のどれでもない(マップ移動・ログアウト・計測の開始と停止など)。</summary>
    None = 0,

    DungeonStart,

    BossRoom,

    Wipe,

    /// <summary>無操作・経過の秒の条件。</summary>
    Condition,

    /// <summary>手動のリセットとプラグインの区切りの要求。</summary>
    Request,
}

/// <summary>回の戦闘の時計の起点の種類(回と一緒に保存する)。</summary>
public enum EncounterClockOrigin
{
    /// <summary>最初の戦闘の出来事(ダンジョンの時計なし)。</summary>
    FirstCombatEvent = 0,

    /// <summary>ダンジョンの開始を受信した時刻。</summary>
    DungeonStartArrival,

    /// <summary>ゲームのダンジョンの時計の開始時刻(この端末の時計に直したもの)。</summary>
    DungeonServerStart,
}
