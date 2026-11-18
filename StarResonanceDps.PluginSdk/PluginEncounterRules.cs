namespace StarResonanceDps.PluginSdk;

/// <summary>
/// Rules that the host applies while it processes the game's messages (<see cref="IPluginEncounterControlService.SetRulesAsync"/>).
/// The default value of each property changes nothing.
/// </summary>
public sealed record PluginEncounterRules
{
    /// <summary>
    /// Splits the current encounter when no combat event has arrived for this many seconds (1 to 86400); 0 does not split.
    /// The host checks it before each message it processes and every second, so the first combat event after the idle time belongs to the new encounter.
    /// Not checked in a benchmark encounter, in a closed encounter, before the combat clock starts, and in an encounter with a dungeon clock.
    /// </summary>
    public int IdleSplitSeconds { get; init; }

    /// <summary>
    /// Splits the current encounter when this many seconds (1 to 86400) have passed since its first combat event, in real time
    /// (paused intervals are not excluded); 0 does not split. Checked like <see cref="IdleSplitSeconds"/>.
    /// </summary>
    public int IntervalSplitSeconds { get; init; }

    /// <summary>
    /// For a split by <see cref="IdleSplitSeconds"/> or <see cref="IntervalSplitSeconds"/>: <c>true</c> to let the meters keep showing the previous
    /// encounter until the new one has records, when the general setting "Hold Results Until Next Event" is on.
    /// </summary>
    public bool KeepPreviousInMeterOnConditionSplit { get; init; }

    /// <summary>Whether the host splits the encounter when a dungeon starts.</summary>
    public PluginAutoSplitMode DungeonStartSplit { get; init; }

    /// <summary>Whether the host splits the encounter when the party enters a boss room.</summary>
    public PluginAutoSplitMode BossRoomSplit { get; init; }

    /// <summary>Whether the host splits the encounter at a party wipe.</summary>
    public PluginAutoSplitMode WipeSplit { get; init; }

    /// <summary>
    /// Runs the combat clock of the encounters the host creates at the start of a dungeon from the start of the dungeon, for the whole dungeon.
    /// The host splits at the start of the dungeon (as with <see cref="PluginAutoSplitMode.Split"/>), does not split at boss rooms, wipes,
    /// <see cref="IdleSplitSeconds"/> and <see cref="IntervalSplitSeconds"/> inside that encounter, does not use "Auto Pause" in it,
    /// and excludes the intervals in which the game's dungeon timer stopped (see <see cref="PluginSessionState.DungeonTimer"/>).
    /// Encounters created by a manual reset, <see cref="IPluginEncounterControlService.SplitAsync"/> or the end of a benchmark during the dungeon
    /// also start from the start of the dungeon. The encounter shows the origin in <see cref="PluginEncounterSnapshot.ClockOrigin"/>.
    /// </summary>
    public PluginDungeonClock DungeonClock { get; init; }
}

/// <summary>Whether the host splits the encounter at an event of the dungeon's progress.</summary>
public enum PluginAutoSplitMode
{
    /// <summary>The general setting "Auto Reset by Progress" decides (<see cref="PluginAggregationSettings.SplitOnProgress"/>).</summary>
    HostSetting = 0,

    /// <summary>Splits, whatever the general setting is.</summary>
    Split,

    /// <summary>Does not split, whatever the general setting is.</summary>
    DoNotSplit
}

/// <summary>The origin of the dungeon clock (<see cref="PluginEncounterRules.DungeonClock"/>).</summary>
public enum PluginDungeonClock
{
    /// <summary>No dungeon clock: the combat clock starts at the first combat event.</summary>
    None = 0,

    /// <summary>The time the host received the start of the dungeon.</summary>
    DungeonStartArrival,

    /// <summary>
    /// The start time the game's dungeon timer sent (<see cref="PluginDungeonTimer.StartUtc"/>), which is the origin of the time the game shows.
    /// The timer arrives shortly after the start of the dungeon, so the host starts with <see cref="DungeonStartArrival"/> and changes the origin
    /// when the timer arrives, only while the encounter has no records. When the dungeon sends no timer, the host has not received the server's time yet,
    /// or the encounter already had records, the origin stays <see cref="PluginClockOrigin.DungeonStartArrival"/>.
    /// </summary>
    DungeonServerStart
}

/// <summary>
/// The overrides the host applies, combined from the rules of all plugins (<see cref="IPluginEncounterControlService.AppliedOverrides"/>).
/// </summary>
/// <param name="DungeonStartSplit">The split at the start of a dungeon.</param>
/// <param name="BossRoomSplit">The split when the party enters a boss room.</param>
/// <param name="WipeSplit">The split at a party wipe.</param>
/// <param name="DungeonClock">The dungeon clock.</param>
public sealed record PluginEncounterOverrides(
    PluginAutoSplitMode DungeonStartSplit,
    PluginAutoSplitMode BossRoomSplit,
    PluginAutoSplitMode WipeSplit,
    PluginDungeonClock DungeonClock);

/// <summary>How <see cref="IPluginEncounterControlService.SetRulesAsync"/> ended.</summary>
/// <param name="Status">The result.</param>
/// <param name="PendingItems">The items that wait because another plugin holds them (the same as <see cref="IPluginEncounterControlService.PendingItems"/>).</param>
public sealed record PluginEncounterRulesResult(
    PluginEncounterRulesStatus Status,
    IReadOnlyList<PluginEncounterRuleItem> PendingItems);

/// <summary>The result of <see cref="IPluginEncounterControlService.SetRulesAsync"/>.</summary>
public enum PluginEncounterRulesStatus
{
    /// <summary>All items apply.</summary>
    Applied = 0,

    /// <summary>The rules were registered, but some items wait because another plugin holds them (<see cref="PluginEncounterRulesResult.PendingItems"/>).</summary>
    Pending,

    /// <summary>Nothing was done, because the host has stopped the plugin or the app has started to exit.</summary>
    Unavailable
}

/// <summary>An item of <see cref="PluginEncounterRules"/> that one plugin holds at a time.</summary>
public enum PluginEncounterRuleItem
{
    /// <summary><see cref="PluginEncounterRules.DungeonStartSplit"/>.</summary>
    DungeonStartSplit = 0,

    /// <summary><see cref="PluginEncounterRules.BossRoomSplit"/>.</summary>
    BossRoomSplit,

    /// <summary><see cref="PluginEncounterRules.WipeSplit"/>.</summary>
    WipeSplit,

    /// <summary><see cref="PluginEncounterRules.DungeonClock"/>. Holding it also holds <see cref="DungeonStartSplit"/> as <see cref="PluginAutoSplitMode.Split"/>.</summary>
    DungeonClock
}
