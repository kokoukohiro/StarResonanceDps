namespace StarResonanceDps.PluginSdk;

/// <summary>
/// The totals of one encounter for every player, with the same numbers as the host's meters.
/// </summary>
/// <param name="IsHistory">
/// The encounter was read from the saved history. In <see cref="IPluginGameDataService.GetEncounter"/>, the history encounter shown in the host
/// (opened by the user, or by a plugin with <see cref="IPluginHistoryService.OpenAsync"/>);
/// from <see cref="IPluginEncounterRecord.GetTotals"/>, also an encounter read with <see cref="IPluginHistoryService.LoadAsync"/>.
/// </param>
/// <param name="StartUtc">
/// When the combat clock started (UTC), or <c>null</c> before it starts. Usually the first combat event (<see cref="FirstCombatEventUtc"/>);
/// in an encounter with a dungeon clock (<see cref="ClockOrigin"/>), the start of the dungeon, which can be before
/// <see cref="PluginEncounterInfo.CreatedUtc"/> and before the first combat event.
/// </param>
/// <param name="Elapsed">
/// The combat time: the time since <see cref="StartUtc"/> minus the paused intervals (the general setting "Auto Pause",
/// and in an encounter with a dungeon clock, the intervals in which the game's dungeon timer stopped).
/// </param>
/// <param name="IsClosed">The encounter has ended.</param>
/// <param name="Players">Players with any total above 0, in no particular order. Not filtered by party.</param>
public sealed record PluginEncounterSnapshot(
    bool IsHistory,
    DateTime? StartUtc,
    TimeSpan Elapsed,
    bool IsClosed,
    IReadOnlyList<PluginEncounterPlayer> Players)
{
    /// <summary>
    /// A number that identifies the encounter in this run of the app, starting from 1; 0 when the host has no encounter yet.
    /// A live encounter keeps its number after it is saved. An encounter read from the saved history gets a new number each time it is read,
    /// so identify a saved encounter by <see cref="HistoryId"/>. The numbers are not saved and start again when the app restarts.
    /// </summary>
    public long EncounterKey { get; init; }

    /// <summary>
    /// The number of the encounter in the saved history (<see cref="IPluginHistoryService"/>), or <c>null</c> while it has not been saved:
    /// a live encounter before the host saves it, and an encounter without records, which is never saved.
    /// In <see cref="PluginEncounterResult.Totals"/> it is always <c>null</c>; the number comes with <see cref="IPluginGameDataService.EncounterSaved"/>.
    /// </summary>
    public ulong? HistoryId { get; init; }

    /// <summary>
    /// When the encounter's last combat event arrived (UTC), or <c>null</c> before the first one.
    /// The combat clock pauses <see cref="PluginEncounterInfo.AutoPauseSeconds"/> after it (when not 0).
    /// </summary>
    public DateTime? LastCombatEventUtc { get; init; }

    /// <summary>
    /// The host records nothing in this encounter and does not start its combat clock,
    /// because its scene is a non-combat area and <see cref="PluginAggregationSettings.StopInNonCombatAreas"/> is on.
    /// Only the encounter the host is recording now can have it: <c>false</c> for an encounter that has closed
    /// (including the previous encounter the meters keep showing) and for an encounter read from the saved history.
    /// </summary>
    public bool IsAggregationStopped { get; init; }

    /// <summary>
    /// In a live benchmark encounter with <see cref="PluginEncounterInfo.BenchmarkFirstTargetOnly"/>, the UUID of the enemy the local player damaged first;
    /// only damage, hate and break against it are counted. <c>null</c> before it is decided, in other encounters,
    /// and for an encounter read from the saved history (the host does not save it).
    /// </summary>
    public long? BenchmarkFirstTargetEntityUuid { get; init; }

    /// <summary>
    /// What the combat clock starts from. <see cref="PluginClockOrigin.FirstCombatEvent"/> unless a plugin registered a dungeon clock
    /// (<see cref="PluginEncounterRules.DungeonClock"/>) before the host created the encounter. Saved with the encounter.
    /// </summary>
    public PluginClockOrigin ClockOrigin { get; init; }

    /// <summary>
    /// When the encounter's first combat event arrived (UTC), or <c>null</c> before it. The host records healing, hate and break only from this time.
    /// The same as <see cref="StartUtc"/> unless the encounter has a dungeon clock.
    /// </summary>
    public DateTime? FirstCombatEventUtc { get; init; }
}

/// <summary>What the combat clock of an encounter starts from (<see cref="PluginEncounterSnapshot.ClockOrigin"/>).</summary>
public enum PluginClockOrigin
{
    /// <summary>The first combat event of the encounter.</summary>
    FirstCombatEvent = 0,

    /// <summary>
    /// The time the host received the start of the dungeon (<see cref="PluginDungeonClock.DungeonStartArrival"/>,
    /// or <see cref="PluginDungeonClock.DungeonServerStart"/> when the server's start time could not be used).
    /// </summary>
    DungeonStartArrival,

    /// <summary>The start time of the game's dungeon timer (<see cref="PluginDungeonClock.DungeonServerStart"/>).</summary>
    DungeonServerStart
}

/// <summary>
/// One player's totals in an encounter.
/// </summary>
/// <param name="CharacterId">The character ID (the same as <see cref="PluginPlayer.CharacterId"/>).</param>
/// <param name="Damage">Damage dealt.</param>
/// <param name="Healing">Healing done.</param>
/// <param name="DamageTaken">Damage taken.</param>
public sealed record PluginEncounterPlayer(
    long CharacterId,
    PluginCombatTotals Damage,
    PluginCombatTotals Healing,
    PluginCombatTotals DamageTaken);

/// <summary>
/// Totals of damage, healing or damage taken.
/// </summary>
/// <param name="Total">The total. Damage and healing equal the host's meter rows.</param>
/// <param name="PerSecond">The total per second of the encounter's combat time (the host's meters).</param>
/// <param name="ActivePerSecond">The total per second of the player's active time (the host's skill detail).</param>
/// <param name="Hits">The number of hits.</param>
/// <param name="CritTotal">The total of critical hits.</param>
/// <param name="CritCount">The number of critical hits.</param>
/// <param name="LuckyTotal">The total of lucky hits.</param>
/// <param name="LuckyCount">The number of lucky hits.</param>
public sealed record PluginCombatTotals(
    ulong Total,
    double PerSecond,
    double ActivePerSecond,
    ulong Hits,
    ulong CritTotal,
    uint CritCount,
    ulong LuckyTotal,
    uint LuckyCount);

/// <summary>
/// Which totals to read.
/// </summary>
public enum PluginCombatKind
{
    /// <summary>Damage dealt.</summary>
    Damage = 0,

    /// <summary>Healing done.</summary>
    Healing
}

/// <summary>
/// One row of a player's skill breakdown (the same rows as the host's Skill Detail widget).
/// </summary>
/// <param name="RowKey">The row key, <c>"id:branch"</c>.</param>
/// <param name="Name">
/// The row name in the current language, without internal names or IDs, or <c>null</c> when the row has no name.
/// The name depends on how the host grouped the row, so it cannot be looked up from the key alone.
/// </param>
/// <param name="Total">The total.</param>
/// <param name="PerSecond">The total per second of the encounter's combat time (the host's meters).</param>
/// <param name="ActivePerSecond">The total per second of the player's active time (the host's skill detail).</param>
/// <param name="Hits">The number of hits.</param>
/// <param name="CritRate">The critical rate, in percent.</param>
/// <param name="Share">The share of the player's total, in percent.</param>
public sealed record PluginSkillTotals(
    string RowKey,
    string? Name,
    ulong Total,
    double PerSecond,
    double ActivePerSecond,
    ulong Hits,
    double CritRate,
    double Share);

/// <summary>
/// The result of an encounter that has just closed: the totals and every player's skill breakdown.
/// </summary>
/// <param name="Totals">
/// The totals of the closed encounter (<see cref="PluginEncounterSnapshot.IsHistory"/> is <c>false</c> and <see cref="PluginEncounterSnapshot.HistoryId"/> is <c>null</c>;
/// the number in the saved history comes with <see cref="IPluginGameDataService.EncounterSaved"/>).
/// </param>
/// <param name="Skills">Character ID to the player's skill breakdown, for every player in <see cref="PluginEncounterSnapshot.Players"/>.</param>
public sealed record PluginEncounterResult(
    PluginEncounterSnapshot Totals,
    IReadOnlyDictionary<long, PluginPlayerSkills> Skills);

/// <summary>
/// One player's skill breakdown.
/// </summary>
/// <param name="Damage">The damage rows, largest first.</param>
/// <param name="Healing">The healing rows, largest first.</param>
public sealed record PluginPlayerSkills(
    IReadOnlyList<PluginSkillTotals> Damage,
    IReadOnlyList<PluginSkillTotals> Healing);
