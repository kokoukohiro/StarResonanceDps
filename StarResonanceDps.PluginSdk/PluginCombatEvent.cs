namespace StarResonanceDps.PluginSdk;

/// <summary>
/// One combat event from <see cref="IPluginCombatEventService.ReadAsync"/>; check the derived type.
/// The same object is passed to every plugin that reads its kind.
/// </summary>
/// <param name="SequenceNumber">
/// The order the host processed it, numbered together with <see cref="PluginPacketMessage.SequenceNumber"/>.
/// An event caused by a game message has a later number than the message. A plugin sees gaps for the events and messages it does not read.
/// </param>
/// <param name="ArrivalTimeUtc">
/// When the game message that carried the event arrived (UTC). For the end of a boss skill telegraph, when the telegraph bar ended.
/// </param>
public abstract record PluginCombatEvent(long SequenceNumber, DateTime ArrivalTimeUtc);

/// <summary>
/// Damage that a player or the player's summon dealt to an enemy, when the host added it to the meters.
/// Only damage the host records comes (see <see cref="PluginEncounterSnapshot.IsAggregationStopped"/> and the benchmark of <see cref="PluginEncounterInfo"/>).
/// </summary>
/// <param name="SequenceNumber">The order the host processed it (<see cref="PluginCombatEvent.SequenceNumber"/>).</param>
/// <param name="ArrivalTimeUtc">When the damage arrived (UTC).</param>
/// <param name="EncounterKey">The encounter the damage was added to (<see cref="IPluginEncounterRecord.EncounterKey"/>).</param>
/// <param name="AttackerCharacterId">The player the damage is counted for. Damage by a summon or a projectile is counted for the player who summoned it.</param>
/// <param name="SourceEntityUuid">The entity that dealt the damage in the game message (for a summon's damage, the summon).</param>
/// <param name="TargetEntityUuid">The enemy.</param>
/// <param name="TargetMonsterId">The enemy's monster ID, or <c>null</c> when it has not been received.</param>
/// <param name="RowKey">The row of the player's skill breakdown the damage is counted in (the same as <see cref="PluginSkillTotals.RowKey"/>).</param>
/// <param name="Source">What the damage came from.</param>
/// <param name="SourceId">The ID of what the damage came from: a skill, projectile or buff ID, depending on <paramref name="Source"/>.</param>
/// <param name="Value">The damage. The meters add it unless <paramref name="IsImmune"/> is <c>true</c>.</param>
/// <param name="HpLessen">The HP the enemy lost.</param>
/// <param name="ShieldBreak">The shield the damage broke.</param>
/// <param name="Element">The element.</param>
/// <param name="Mode">Physical, magical or neither.</param>
/// <param name="IsCrit">A critical hit.</param>
/// <param name="IsLucky">A lucky hit.</param>
/// <param name="IsMiss">A miss.</param>
/// <param name="IsImmune">The enemy was immune; the meters do not add it.</param>
/// <param name="IsKill">The hit defeated the enemy.</param>
public sealed record PluginDamageEvent(
    long SequenceNumber,
    DateTime ArrivalTimeUtc,
    long EncounterKey,
    long AttackerCharacterId,
    long SourceEntityUuid,
    long TargetEntityUuid,
    int? TargetMonsterId,
    string RowKey,
    PluginDamageSource Source,
    int SourceId,
    long Value,
    long HpLessen,
    long ShieldBreak,
    PluginDamageElement Element,
    PluginDamageMode Mode,
    bool IsCrit,
    bool IsLucky,
    bool IsMiss,
    bool IsImmune,
    bool IsKill) : PluginCombatEvent(SequenceNumber, ArrivalTimeUtc);

/// <summary>A player's skill activation the host recorded, the same record as <see cref="IPluginEncounterRecord.GetSkillActivations"/>.</summary>
/// <param name="SequenceNumber">The order the host processed it (<see cref="PluginCombatEvent.SequenceNumber"/>).</param>
/// <param name="ArrivalTimeUtc">When the activation was received (UTC), the same as the record's.</param>
/// <param name="EncounterKey">The encounter the record was added to (<see cref="IPluginEncounterRecord.EncounterKey"/>).</param>
/// <param name="CharacterId">The player.</param>
/// <param name="RecordIndex">
/// The index of the record among the player's activations in the encounter.
/// Pass <c>RecordIndex + 1</c> as <c>startIndex</c> to read the activations after it.
/// </param>
/// <param name="Record">The record.</param>
public sealed record PluginSkillActivationEvent(
    long SequenceNumber,
    DateTime ArrivalTimeUtc,
    long EncounterKey,
    long CharacterId,
    int RecordIndex,
    PluginSkillActivation Record) : PluginCombatEvent(SequenceNumber, ArrivalTimeUtc);

/// <summary>Damage a player took that the host recorded, the same record as <see cref="IPluginEncounterRecord.GetTakenHits"/>.</summary>
/// <param name="SequenceNumber">The order the host processed it (<see cref="PluginCombatEvent.SequenceNumber"/>).</param>
/// <param name="ArrivalTimeUtc">When the damage was received (UTC), the same as the record's.</param>
/// <param name="EncounterKey">The encounter the record was added to.</param>
/// <param name="RecordIndex">The index of the record. Pass <c>RecordIndex + 1</c> as <c>startIndex</c> to read the records after it.</param>
/// <param name="Record">The record.</param>
public sealed record PluginTakenHitEvent(
    long SequenceNumber,
    DateTime ArrivalTimeUtc,
    long EncounterKey,
    int RecordIndex,
    PluginTakenHit Record) : PluginCombatEvent(SequenceNumber, ArrivalTimeUtc);

/// <summary>A Timeline record the host added, the same record as <see cref="IPluginEncounterRecord.GetCombatLog"/>.</summary>
/// <param name="SequenceNumber">The order the host processed it (<see cref="PluginCombatEvent.SequenceNumber"/>).</param>
/// <param name="ArrivalTimeUtc">The record's time (UTC); for the end of a boss skill telegraph, when the telegraph bar ended.</param>
/// <param name="EncounterKey">The encounter the record was added to.</param>
/// <param name="RecordIndex">The index of the record. Pass <c>RecordIndex + 1</c> as <c>startIndex</c> to read the records after it.</param>
/// <param name="Record">The record.</param>
public sealed record PluginCombatLogEvent(
    long SequenceNumber,
    DateTime ArrivalTimeUtc,
    long EncounterKey,
    int RecordIndex,
    PluginCombatLogRecord Record) : PluginCombatEvent(SequenceNumber, ArrivalTimeUtc);

/// <summary>What happened to a buff in a <see cref="PluginBuffEvent"/>.</summary>
public enum PluginBuffEventKind
{
    /// <summary>The buff was applied.</summary>
    Added,

    /// <summary>The buff's stacks or duration changed.</summary>
    Changed,

    /// <summary>The buff was removed.</summary>
    Removed,

    /// <summary>
    /// The buff was on at that moment, in a full list of the entity's buffs: when another entity comes around,
    /// and when the game applies all of the local player's buffs again after a map change. <see cref="AppearListEnd"/> follows the list.
    /// </summary>
    Present,

    /// <summary>
    /// The end of the full list of the entity's buffs (also for an empty list). A buff that was on before and is not in the list
    /// was removed while the host could not see it. No list and no end come when the game sends no list.
    /// </summary>
    AppearListEnd
}

/// <summary>A buff on a player or a monster was applied, changed or removed, or listed in a full list of the entity's buffs.</summary>
/// <param name="SequenceNumber">The order the host processed it (<see cref="PluginCombatEvent.SequenceNumber"/>).</param>
/// <param name="ArrivalTimeUtc">When the buff message arrived (UTC).</param>
/// <param name="Kind">What happened.</param>
/// <param name="EntityUuid">The entity the buff is on.</param>
/// <param name="CharacterId">The character ID when the entity is a player.</param>
/// <param name="BuffUuid">The buff instance UUID (the same as <see cref="PluginBuff.BuffUuid"/>). 0 for <see cref="PluginBuffEventKind.AppearListEnd"/>.</param>
/// <param name="BuffId">
/// The buff ID. For <see cref="PluginBuffEventKind.Changed"/> and <see cref="PluginBuffEventKind.Removed"/>, the ID received when the buff was applied,
/// or 0 when the host did not receive it (for example a buff applied before the player entered the map). 0 for <see cref="PluginBuffEventKind.AppearListEnd"/>.
/// </param>
/// <param name="Level">The buff level, or <c>null</c> when the message did not carry it.</param>
/// <param name="Layer">The stacks, or <c>null</c> when the message did not carry them.</param>
/// <param name="DurationMilliseconds">The total duration, or <c>null</c> when the message did not carry it. 0 or less means no duration.</param>
/// <param name="ServerCreateTimeUtc">When the server created the buff, or <c>null</c> when the message did not carry it.</param>
/// <param name="CasterEntityUuid">The entity that applied the buff, as received when it was applied; 0 when not received.</param>
/// <param name="CasterCharacterId">The character ID when <paramref name="CasterEntityUuid"/> is a player.</param>
/// <param name="SourceType">What applied the buff, as received when it was applied, or <c>null</c> when not received.</param>
/// <param name="SourceConfigId">The ID of what applied the buff (for example a talent or factor), as received when it was applied; 0 when not received.</param>
/// <param name="IsRecorded">
/// The host's current encounter records events at that moment (the recording rules: aggregation stop and benchmark).
/// For a buff on a player, a benchmark records only the local player's buffs.
/// </param>
public sealed record PluginBuffEvent(
    long SequenceNumber,
    DateTime ArrivalTimeUtc,
    PluginBuffEventKind Kind,
    long EntityUuid,
    long? CharacterId,
    long BuffUuid,
    int BuffId,
    int? Level,
    int? Layer,
    int? DurationMilliseconds,
    DateTime? ServerCreateTimeUtc,
    long CasterEntityUuid,
    long? CasterCharacterId,
    PluginFightSourceType? SourceType,
    int SourceConfigId,
    bool IsRecorded) : PluginCombatEvent(SequenceNumber, ArrivalTimeUtc);

/// <summary>
/// A monster's current skill was received: when it starts using a skill, and with <see cref="SkillId"/> 0 when it stops.
/// It also comes when the game sends the same skill again. A skill the monster was using when it came around does not come.
/// </summary>
/// <param name="SequenceNumber">The order the host processed it (<see cref="PluginCombatEvent.SequenceNumber"/>).</param>
/// <param name="ArrivalTimeUtc">When the skill arrived (UTC).</param>
/// <param name="EntityUuid">The monster.</param>
/// <param name="MonsterId">The monster ID, or <c>null</c> when it has not been received.</param>
/// <param name="PreviousSkillId">The skill before the message, or 0.</param>
/// <param name="SkillId">The skill after the message; 0 when the monster stopped using a skill.</param>
/// <param name="SkillLevel">The skill level received with it, or 0.</param>
/// <param name="IsRecorded">The host's current encounter records events at that moment.</param>
public sealed record PluginMonsterSkillEvent(
    long SequenceNumber,
    DateTime ArrivalTimeUtc,
    long EntityUuid,
    int? MonsterId,
    int PreviousSkillId,
    int SkillId,
    int SkillLevel,
    bool IsRecorded) : PluginCombatEvent(SequenceNumber, ArrivalTimeUtc);

/// <summary>
/// A monster's HP reached 0 or came back from 0. It also comes for entities whose maximum HP is 0 (for example objects without an HP bar).
/// </summary>
/// <param name="SequenceNumber">The order the host processed it (<see cref="PluginCombatEvent.SequenceNumber"/>).</param>
/// <param name="ArrivalTimeUtc">When the HP arrived (UTC).</param>
/// <param name="EntityUuid">The monster.</param>
/// <param name="MonsterId">The monster ID, or <c>null</c> when it has not been received.</param>
/// <param name="IsZero"><c>true</c> when the HP reached 0, <c>false</c> when it came back from 0.</param>
/// <param name="PreviousHp">The HP before the message, or <c>null</c> when it had not been received and when <paramref name="IsOnAppear"/> is <c>true</c>.</param>
/// <param name="MaxHp">The maximum HP, or 0 when it has not been received.</param>
/// <param name="IsOnAppear">The monster had 0 HP when it came around.</param>
/// <param name="IsRecorded">The host's current encounter records events at that moment.</param>
public sealed record PluginMonsterHpZeroEvent(
    long SequenceNumber,
    DateTime ArrivalTimeUtc,
    long EntityUuid,
    int? MonsterId,
    bool IsZero,
    long? PreviousHp,
    long MaxHp,
    bool IsOnAppear,
    bool IsRecorded) : PluginCombatEvent(SequenceNumber, ArrivalTimeUtc);
