namespace StarResonanceDps.PluginSdk;

/// <summary>
/// Changes how the host splits encounters and runs the combat clock. The host implements it.
///
/// <para>
/// Methods can be called from any thread. The host runs <see cref="SplitAsync"/> and <see cref="SetRulesAsync"/> on the thread that
/// processes the game's messages, after the messages that arrived before the call, in the order of the calls; while the host is not
/// capturing, it runs them on the UI thread. The tasks complete asynchronously, so the plugin's continuation does not run on the host's thread.
/// <see cref="RulesChanged"/> is raised on the UI thread.
/// </para>
///
/// <para>
/// The rules are kept only while the plugin runs. The host does not save them; register them again in <see cref="IStarResonancePlugin.Initialize"/>.
/// When the host stops the plugin, it removes the plugin's rules. After that (including during <see cref="IStarResonancePlugin.Shutdown"/>),
/// the methods do nothing: <see cref="SplitAsync"/> ends with <see cref="PluginEncounterSplitOutcome.Unavailable"/> and
/// <see cref="SetRulesAsync"/> with <see cref="PluginEncounterRulesStatus.Unavailable"/>. After the app has started to exit, they also end that way.
/// The arguments of <see cref="SetRulesAsync"/> are still checked first.
/// </para>
/// </summary>
public interface IPluginEncounterControlService
{
    /// <summary>
    /// Closes the current encounter and creates a new one, like a manual reset in the Aggregation tab.
    /// During a benchmark (also while it waits for its first event), the host does nothing and the task ends with
    /// <see cref="PluginEncounterSplitOutcome.SkippedBenchmark"/>.
    /// The new encounter comes with <see cref="IPluginGameDataService.EncounterStarted"/>, and the closed encounter with
    /// <see cref="IPluginGameDataService.EncounterClosed"/> when it had records.
    /// </summary>
    /// <param name="keepPreviousInMeter">
    /// <c>true</c> to let the meters keep showing the previous encounter until the new one has records, when the general setting
    /// "Hold Results Until Next Event" is on (like a split by progress). <c>false</c> to show the new encounter at once (like a manual reset).
    /// </param>
    /// <param name="cancellationToken">Cancels the request before the host runs it; the task then ends as canceled.</param>
    /// <returns>How the request ended.</returns>
    Task<PluginEncounterSplitResult> SplitAsync(bool keepPreviousInMeter = false, CancellationToken cancellationToken = default);

    /// <summary>
    /// Registers this plugin's rules, replacing the rules it registered before; <c>null</c> removes them.
    /// The rules apply from the next message the host processes, and only to encounters the host creates after that,
    /// except <see cref="PluginEncounterRules.IdleSplitSeconds"/> and <see cref="PluginEncounterRules.IntervalSplitSeconds"/>, which also apply to the current encounter.
    ///
    /// <para>
    /// Each item of <see cref="PluginEncounterRuleItem"/> other than its default value is held by one plugin at a time: the first plugin that registered it.
    /// Registering a different value for an item another plugin holds does not fail: the item waits in <see cref="PendingItems"/>
    /// and applies when that plugin removes its rules or stops. The same value as the holder's is applied at once.
    /// The conditions (<see cref="PluginEncounterRules.IdleSplitSeconds"/> and <see cref="PluginEncounterRules.IntervalSplitSeconds"/>) of all plugins apply together.
    /// </para>
    /// </summary>
    /// <param name="rules">The rules, or <c>null</c> to remove this plugin's rules.</param>
    /// <param name="cancellationToken">Cancels the request before the host runs it; the task then ends as canceled.</param>
    /// <returns>How the request ended.</returns>
    /// <exception cref="ArgumentOutOfRangeException">
    /// A number of seconds is neither 0 nor from 1 to 86400, or an enumeration value is not defined.
    /// </exception>
    /// <exception cref="ArgumentException">
    /// <see cref="PluginEncounterRules.DungeonClock"/> is not <see cref="PluginDungeonClock.None"/> while
    /// <see cref="PluginEncounterRules.DungeonStartSplit"/> is <see cref="PluginAutoSplitMode.DoNotSplit"/>, or
    /// <see cref="PluginEncounterRules.BossRoomSplit"/> or <see cref="PluginEncounterRules.WipeSplit"/> is <see cref="PluginAutoSplitMode.Split"/>.
    /// </exception>
    Task<PluginEncounterRulesResult> SetRulesAsync(PluginEncounterRules? rules, CancellationToken cancellationToken = default);

    /// <summary>The rules this plugin registered last (applied or waiting), or <c>null</c> when it has none.</summary>
    PluginEncounterRules? Rules { get; }

    /// <summary>
    /// The items of <see cref="Rules"/> that wait because another plugin holds them with a different value. Empty when all apply.
    /// </summary>
    IReadOnlyList<PluginEncounterRuleItem> PendingItems { get; }

    /// <summary>The overrides the host applies now, combined from the rules of all plugins.</summary>
    PluginEncounterOverrides AppliedOverrides { get; }

    /// <summary>
    /// Raised on the UI thread after <see cref="PendingItems"/> or <see cref="AppliedOverrides"/> has changed,
    /// including when another plugin registered or removed its rules or stopped.
    /// </summary>
    event EventHandler? RulesChanged;
}
