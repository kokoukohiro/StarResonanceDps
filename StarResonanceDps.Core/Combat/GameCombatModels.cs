namespace StarResonanceDps.Core.Combat;

public enum GameCombatEventKind
{
    Damage,
    Healing
}

public enum GameDungeonEntryPhase
{
    Idle,
    Active,
    Completed
}

public sealed record GameSkillStatistics(
    int SkillId,
    long DamageDone,
    long HealingDone,
    long DamageTaken,
    int HitCount,
    int CriticalHitCount,
    int LuckyHitCount,
    int MissCount);

public sealed record GameCombatantSnapshot(
    long EntityUuid,
    long CharacterId,
    int EntityType,
    string Name,
    int ProfessionId,
    long DamageDone,
    long HealingDone,
    long DamageTaken,
    long HealingTaken,
    long EffectiveDamageDone,
    long EffectiveHealingDone,
    long EffectiveDamageTaken,
    long ServerDungeonDamageTotal,
    int HitCount,
    int CriticalHitCount,
    int LuckyHitCount,
    int MissCount,
    IReadOnlyList<GameSkillStatistics> Skills);

public sealed record GameCombatEvent(
    DateTimeOffset OccurredAtUtc,
    GameCombatEventKind Kind,
    long AttackerUuid,
    long TargetUuid,
    int SkillId,
    int SkillLevel,
    long Value,
    long HpLessenValue,
    long ShieldLessenValue,
    int DamageType,
    int DamageProperty,
    bool IsCritical,
    bool IsLucky,
    bool IsMiss,
    bool IsDead);

public sealed record GameCombatSnapshot(
    Guid DungeonEntryId,
    GameDungeonEntryPhase Phase,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset? StartedAtUtc,
    DateTimeOffset? CompletedAtUtc,
    uint LevelMapId,
    string MapName,
    int DungeonState,
    long TotalDamage,
    long TotalHealing,
    long TotalTakenDamage,
    IReadOnlyList<GameCombatantSnapshot> Combatants,
    IReadOnlyList<GameCombatEvent> RecentEvents,
    IReadOnlyDictionary<long, long> DungeonDamageTotals)
{
    public TimeSpan Elapsed => StartedAtUtc is null
        ? TimeSpan.Zero
        : (CompletedAtUtc ?? DateTimeOffset.UtcNow) - StartedAtUtc.Value;
}

public sealed class GameCombatSnapshotChangedEventArgs(GameCombatSnapshot snapshot) : EventArgs
{
    public GameCombatSnapshot Snapshot { get; } = snapshot;
}
