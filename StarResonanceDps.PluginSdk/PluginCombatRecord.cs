namespace StarResonanceDps.PluginSdk;

/// <summary>
/// One skill activation of a player. The local player's activations are recorded when the game accepts the skill,
/// other players' when they start using it.
/// </summary>
/// <param name="SkillId">
/// The skill the player used. <see cref="IPluginNameService.GetSkillActivationName"/> returns the name the host's graphs show.
/// </param>
/// <param name="Frame">The frame the host's graphs draw behind the skill's icon.</param>
/// <param name="ArrivalTimeUtc">When the activation was received (UTC).</param>
public sealed record PluginSkillActivation(int SkillId, PluginSkillFrame Frame, DateTime ArrivalTimeUtc);

/// <summary>
/// The frame the host's graphs draw behind a skill's icon, decided by the skill the player used and the host's own data.
/// </summary>
public enum PluginSkillFrame
{
    /// <summary>Other skills (also skills the host has no data for).</summary>
    Standard = 1,

    /// <summary>Imagines and the skills derived from them.</summary>
    Imagine = 2,

    /// <summary>Ultimate skills and the skills derived from them.</summary>
    Ultimate = 3
}

/// <summary>
/// Damage a player took (a record of the host's Damage Taken Log).
/// </summary>
/// <param name="Sequence">The order in the encounter, shared with <see cref="PluginCombatLogRecord.Sequence"/>.</param>
/// <param name="ArrivalTimeUtc">When the damage was received (UTC).</param>
/// <param name="CharacterId">The player who took the damage.</param>
/// <param name="Attacker">
/// The entity that dealt the damage. For a summoned entity or an object, the monster or player that summoned or placed it, when the host knows it.
/// </param>
/// <param name="Source">What the damage came from.</param>
/// <param name="SourceId">The ID of what the damage came from: a skill, projectile or buff ID, depending on <paramref name="Source"/>.</param>
/// <param name="BuffSourceSkillId">For damage from a buff, the skill that applied the buff, or 0.</param>
/// <param name="SummonSourceSkillId">For damage from a summoned entity, the skill that summoned it, or 0.</param>
/// <param name="Value">The damage.</param>
/// <param name="Element">The element, or <c>null</c> in records saved before the host saved elements.</param>
/// <param name="Mode">Physical, magical or neither.</param>
/// <param name="IsLethal">The damage incapacitated the player.</param>
public sealed record PluginTakenHit(
    long Sequence,
    DateTime ArrivalTimeUtc,
    long CharacterId,
    PluginCombatParty Attacker,
    PluginDamageSource Source,
    int SourceId,
    int BuffSourceSkillId,
    int SummonSourceSkillId,
    long Value,
    PluginDamageElement? Element,
    PluginDamageMode Mode,
    bool IsLethal);

/// <summary>
/// One record of the host's Timeline; check the derived type.
/// </summary>
/// <param name="Sequence">The order in the encounter, shared with <see cref="PluginTakenHit.Sequence"/>.</param>
/// <param name="ArrivalTimeUtc">When it was received (UTC). For the end of a boss skill telegraph, when the telegraph bar ended.</param>
public abstract record PluginCombatLogRecord(long Sequence, DateTime ArrivalTimeUtc);

/// <summary>
/// An enemy started a skill that has a cast time or shows a warning on the combat screen.
/// </summary>
/// <param name="Sequence">The order in the encounter (<see cref="PluginCombatLogRecord.Sequence"/>).</param>
/// <param name="ArrivalTimeUtc">When the cast started (UTC).</param>
/// <param name="Caster">The enemy. A summoned entity without a name of its own is recorded as the monster that summoned it.</param>
/// <param name="SkillId">The skill.</param>
/// <param name="IsWarning">A skill that shows a warning on the combat screen.</param>
public sealed record PluginCastRecord(
    long Sequence,
    DateTime ArrivalTimeUtc,
    PluginCombatParty Caster,
    int SkillId,
    bool IsWarning) : PluginCombatLogRecord(Sequence, ArrivalTimeUtc);

/// <summary>A boss skill telegraph, or the end of its bar.</summary>
/// <param name="Sequence">The order in the encounter (<see cref="PluginCombatLogRecord.Sequence"/>).</param>
/// <param name="ArrivalTimeUtc">When the telegraph arrived (UTC), or when its bar ended.</param>
/// <param name="SkillId">The telegraphed skill.</param>
/// <param name="OwnerMonsterId">The monster ID of the boss around the player when the telegraph arrived, or <c>null</c>.</param>
/// <param name="IsBarEnd"><c>true</c> for the end of the telegraph bar.</param>
public sealed record PluginAnnouncementRecord(
    long Sequence,
    DateTime ArrivalTimeUtc,
    int SkillId,
    int? OwnerMonsterId,
    bool IsBarEnd) : PluginCombatLogRecord(Sequence, ArrivalTimeUtc);

/// <summary>A player was incapacitated without a damage record (for example by a mechanic).</summary>
/// <param name="Sequence">The order in the encounter (<see cref="PluginCombatLogRecord.Sequence"/>).</param>
/// <param name="ArrivalTimeUtc">When it was received (UTC).</param>
/// <param name="CharacterId">The player.</param>
public sealed record PluginPlayerDeathRecord(
    long Sequence,
    DateTime ArrivalTimeUtc,
    long CharacterId) : PluginCombatLogRecord(Sequence, ArrivalTimeUtc);

/// <summary>An enemy healed an enemy or a player.</summary>
/// <param name="Sequence">The order in the encounter (<see cref="PluginCombatLogRecord.Sequence"/>).</param>
/// <param name="ArrivalTimeUtc">When it was received (UTC).</param>
/// <param name="Healer">The enemy that healed.</param>
/// <param name="Target">The healed entity.</param>
/// <param name="Source">What the heal came from.</param>
/// <param name="SourceId">The ID of what the heal came from.</param>
/// <param name="BuffSourceSkillId">For a heal from a buff, the skill that applied the buff, or 0.</param>
/// <param name="SummonSourceSkillId">For a heal from a summoned entity, the skill that summoned it, or 0.</param>
/// <param name="Value">The HP actually restored.</param>
/// <param name="CastSequence">The <see cref="PluginCombatLogRecord.Sequence"/> of the cast that caused it, or 0.</param>
public sealed record PluginEnemyHealRecord(
    long Sequence,
    DateTime ArrivalTimeUtc,
    PluginCombatParty Healer,
    PluginCombatParty Target,
    PluginDamageSource Source,
    int SourceId,
    int BuffSourceSkillId,
    int SummonSourceSkillId,
    long Value,
    long CastSequence) : PluginCombatLogRecord(Sequence, ArrivalTimeUtc);

/// <summary>An enemy dealt damage to an enemy.</summary>
/// <param name="Sequence">The order in the encounter (<see cref="PluginCombatLogRecord.Sequence"/>).</param>
/// <param name="ArrivalTimeUtc">When it was received (UTC).</param>
/// <param name="Attacker">The enemy that dealt the damage.</param>
/// <param name="Target">The enemy that took it.</param>
/// <param name="Source">What the damage came from.</param>
/// <param name="SourceId">The ID of what the damage came from.</param>
/// <param name="BuffSourceSkillId">For damage from a buff, the skill that applied the buff, or 0.</param>
/// <param name="SummonSourceSkillId">For damage from a summoned entity, the skill that summoned it, or 0.</param>
/// <param name="Value">The damage.</param>
/// <param name="CastSequence">The <see cref="PluginCombatLogRecord.Sequence"/> of the cast that caused it, or 0.</param>
public sealed record PluginEnemyDamageRecord(
    long Sequence,
    DateTime ArrivalTimeUtc,
    PluginCombatParty Attacker,
    PluginCombatParty Target,
    PluginDamageSource Source,
    int SourceId,
    int BuffSourceSkillId,
    int SummonSourceSkillId,
    long Value,
    long CastSequence) : PluginCombatLogRecord(Sequence, ArrivalTimeUtc);

/// <summary>An enemy applied a buff to an entity.</summary>
/// <param name="Sequence">The order in the encounter (<see cref="PluginCombatLogRecord.Sequence"/>).</param>
/// <param name="ArrivalTimeUtc">When it was received (UTC).</param>
/// <param name="Caster">The enemy that applied it.</param>
/// <param name="Target">The entity it was applied to.</param>
/// <param name="BuffId">The buff.</param>
/// <param name="DurationMilliseconds">The duration. 0 or less means no duration.</param>
/// <param name="Source">What applied the buff.</param>
/// <param name="SourceId">The ID of what applied the buff.</param>
/// <param name="BuffSourceSkillId">For a buff applied by another buff, the skill that applied that buff, or 0.</param>
/// <param name="SummonSourceSkillId">For a buff from a summoned entity, the skill that summoned it, or 0.</param>
/// <param name="CastSequence">The <see cref="PluginCombatLogRecord.Sequence"/> of the cast that caused it, or 0.</param>
public sealed record PluginEnemyBuffRecord(
    long Sequence,
    DateTime ArrivalTimeUtc,
    PluginCombatParty Caster,
    PluginCombatParty Target,
    int BuffId,
    int DurationMilliseconds,
    PluginDamageSource Source,
    int SourceId,
    int BuffSourceSkillId,
    int SummonSourceSkillId,
    long CastSequence) : PluginCombatLogRecord(Sequence, ArrivalTimeUtc);

/// <summary>An entity in a record.</summary>
/// <param name="EntityUuid">The entity UUID.</param>
/// <param name="Kind">What kind of entity it is.</param>
/// <param name="CharacterId">The character ID when it is a player.</param>
/// <param name="MonsterId">The monster ID when it is a monster and the ID has been received.</param>
public sealed record PluginCombatParty(long EntityUuid, PluginCombatPartyKind Kind, long? CharacterId, int? MonsterId);

/// <summary>The kind of entity in a record.</summary>
public enum PluginCombatPartyKind
{
    /// <summary>A player.</summary>
    Player,

    /// <summary>A monster.</summary>
    Monster,

    /// <summary>A game mechanic that is neither a player nor a monster (the Timeline shows it as the system).</summary>
    System,

    /// <summary>Any other entity.</summary>
    Other
}

/// <summary>A player's damage or healing per second of the encounter's combat time.</summary>
/// <param name="Elapsed">The combat time when it was read (paused intervals excluded).</param>
/// <param name="IsClosed">The encounter has ended.</param>
/// <param name="Totals">
/// <c>Totals[i]</c> is the total from i seconds to i + 1 seconds (inclusive) of combat time.
/// A live encounter has only complete seconds; a closed encounter also has the last partial second.
/// </param>
public sealed record PluginPerSecondTotals(TimeSpan Elapsed, bool IsClosed, IReadOnlyList<ulong> Totals);
