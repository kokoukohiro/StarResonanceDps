// This file is part of StarResonanceDps. See LICENSING.md for the license, additional permissions and additional terms.

using Zproto;

namespace StarResonanceDps.Core.CombatRuntime;

/// <summary>
/// 戦闘のできごとの種類。値は SDK の <c>PluginCombatEventKinds</c> と同じビット。
/// </summary>
[Flags]
public enum CombatObservationKinds
{
    None = 0,
    Damage = 1 << 0,
    SkillActivation = 1 << 1,
    TakenHit = 1 << 2,
    CombatLog = 1 << 3,
    PlayerBuff = 1 << 4,
    MonsterBuff = 1 << 5,
    MonsterSkill = 1 << 6,
    MonsterHpZero = 1 << 7
}

/// <summary>メーターに足した与ダメ1件。<see cref="SkillKey"/> はメーターの行の鍵に使う畳んだ技の番号。</summary>
public readonly record struct DamageObservation(
    long EncounterKey,
    long AttackerUuid,
    long SourceEntityUuid,
    long TargetUuid,
    int? TargetMonsterId,
    long SkillKey,
    EDamageSource Source,
    int SourceId,
    long Value,
    long HpLessen,
    long ShieldBreak,
    EDamageProperty Element,
    EDamageMode Mode,
    bool IsCrit,
    bool IsLucky,
    bool IsMiss,
    bool IsImmune,
    bool IsKill,
    DateTime ArrivalUtc);

/// <summary>回に足したプレイヤーのスキルの記録。<see cref="RecordIndex"/> はその人の記録の中の番号。</summary>
public readonly record struct SkillActivationObservation(long EncounterKey, long PlayerUuid, int RecordIndex, SkillActivation Activation);

/// <summary>回に足した被弾の記録と、解いた加害者。</summary>
public readonly record struct TakenHitObservation(long EncounterKey, int RecordIndex, TakenHitRecord Record, CombatEventLogParty Attacker);

/// <summary>
/// 回に足したタイムラインの記録と、解いた登場人物。<see cref="Actor"/> は詠唱した敵・回復した敵・与ダメした敵・バフを付けた敵、
/// <see cref="Target"/> は敵の回復・与ダメ・バフの相手。予告と戦闘不能は持たない。
/// </summary>
public readonly record struct CombatLogObservation(
    long EncounterKey,
    int RecordIndex,
    CombatEventLogRecord Record,
    CombatEventLogParty? Actor,
    CombatEventLogParty? Target);

public enum BuffObservationKind
{
    Added,
    Changed,
    Removed,

    /// <summary>その時点で付いているバフの一覧の1件(出現時の一覧と、自分のバフの付け直し)。</summary>
    Present,

    /// <summary>一覧の終わり。</summary>
    ListEnd
}

/// <summary>
/// 受信したバフの付与・変化・除去、または一覧の1件。変化と除去の番号・付与者・付与元は、付与のときに控えた値(無ければ 0 と null)。
/// </summary>
public readonly record struct BuffObservation(
    BuffObservationKind Kind,
    long HolderUuid,
    long BuffUuid,
    int BuffId,
    int? Level,
    int? Layer,
    int? DurationMilliseconds,
    DateTime? ServerCreateUtc,
    long CasterUuid,
    int? FightSourceType,
    int SourceConfigId,
    bool IsRecorded,
    DateTime ArrivalUtc);

/// <summary>受信したモンスターの今のスキル(0 は使い終わり)。</summary>
public readonly record struct MonsterSkillObservation(
    long EntityUuid,
    int? MonsterId,
    int PreviousSkillId,
    int SkillId,
    int SkillLevel,
    bool IsRecorded,
    DateTime ArrivalUtc);

/// <summary>モンスターの HP が 0 になった、または 0 から戻った。</summary>
public readonly record struct MonsterHpZeroObservation(
    long EntityUuid,
    int? MonsterId,
    bool IsZero,
    long? PreviousHp,
    long MaxHp,
    bool IsOnAppear,
    bool IsRecorded,
    DateTime ArrivalUtc);

/// <summary>
/// 戦闘のできごとを App へ渡す口(パケット処理のスレッドで上がる)。値は持たない。
///
/// <para>
/// 受け手のいる種類は App が <see cref="ActiveKinds"/> に書く。記録する所は <see cref="IsActive"/> で見て、
/// 受け手のいない種類は引数を作らない(登場人物を解くなどの手間もかけない)。
/// </para>
/// </summary>
public static class CombatObservationFeed
{
    private static int _activeKinds;

    /// <summary>受け手のいる種類。App が受け手の表を作り直すたびに書く。</summary>
    public static CombatObservationKinds ActiveKinds
    {
        get => (CombatObservationKinds)Volatile.Read(ref _activeKinds);
        set => Volatile.Write(ref _activeKinds, (int)value);
    }

    public static bool IsActive(CombatObservationKinds kind) => (ActiveKinds & kind) != 0;

    public static event Action<DamageObservation>? DamageObserved;

    public static event Action<SkillActivationObservation>? SkillActivationObserved;

    public static event Action<TakenHitObservation>? TakenHitObserved;

    public static event Action<CombatLogObservation>? CombatLogObserved;

    public static event Action<BuffObservation>? BuffObserved;

    public static event Action<MonsterSkillObservation>? MonsterSkillObserved;

    public static event Action<MonsterHpZeroObservation>? MonsterHpZeroObserved;

    internal static void Publish(in DamageObservation observation) => DamageObserved?.Invoke(observation);

    internal static void Publish(in SkillActivationObservation observation) => SkillActivationObserved?.Invoke(observation);

    internal static void Publish(in TakenHitObservation observation) => TakenHitObserved?.Invoke(observation);

    internal static void Publish(in CombatLogObservation observation) => CombatLogObserved?.Invoke(observation);

    internal static void Publish(in BuffObservation observation) => BuffObserved?.Invoke(observation);

    internal static void Publish(in MonsterSkillObservation observation) => MonsterSkillObserved?.Invoke(observation);

    internal static void Publish(in MonsterHpZeroObservation observation) => MonsterHpZeroObserved?.Invoke(observation);
}
