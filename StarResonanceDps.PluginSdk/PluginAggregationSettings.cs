namespace StarResonanceDps.PluginSdk;

/// <summary>
/// The host's aggregation settings: the "Aggregation" section of the general settings (<see cref="IPluginGameDataService.AggregationSettings"/>).
/// The values are the user's settings.
/// </summary>
/// <param name="StopInNonCombatAreas">
/// "Stop Aggregation in Non-Combat Areas": in the non-combat areas, the host records nothing and does not start the combat clock
/// (<see cref="PluginEncounterSnapshot.IsAggregationStopped"/>).
/// </param>
/// <param name="SplitOnProgress">
/// "Auto Reset by Progress": the host creates a new encounter at the start of a dungeon, on entering a boss room and at a party wipe.
/// This value stays the user's setting; plugins can override the three splits (<see cref="IPluginEncounterControlService.AppliedOverrides"/>).
/// </param>
/// <param name="KeepResultUntilNextEvent">
/// "Hold Results Until Next Event": after a split by progress or a map change, the meters keep showing the previous encounter
/// until the new encounter has records.
/// </param>
/// <param name="ReturnToLiveOnNextEvent">
/// "Auto-exit History View": while a history encounter is open, the host returns to the live encounter at the next live event,
/// except while a plugin holds <see cref="IPluginHistoryService.KeepHistoryOpen"/>.
/// </param>
/// <param name="AutoPauseSeconds">
/// "Auto Pause" in seconds; 0 when the combat clock does not pause. It applies to the encounters the host creates after the change;
/// the value of an encounter is <see cref="PluginEncounterInfo.AutoPauseSeconds"/>.
/// </param>
/// <param name="BenchmarkSeconds">
/// "Benchmark Duration" in seconds. It applies from the next benchmark; the value of a benchmark encounter is <see cref="PluginEncounterInfo.BenchmarkSeconds"/>.
/// </param>
/// <param name="BenchmarkFirstTargetOnly">
/// "Measure Only the First Enemy Attacked". It applies from the next benchmark;
/// the value of a benchmark encounter is <see cref="PluginEncounterInfo.BenchmarkFirstTargetOnly"/>.
/// </param>
/// <param name="MaxSavedEncounters">
/// "Max History Entries"; 0 when there is no limit. When there are more saved encounters, the oldest are deleted with the plugins' data.
/// </param>
public sealed record PluginAggregationSettings(
    bool StopInNonCombatAreas,
    bool SplitOnProgress,
    bool KeepResultUntilNextEvent,
    bool ReturnToLiveOnNextEvent,
    int AutoPauseSeconds,
    int BenchmarkSeconds,
    bool BenchmarkFirstTargetOnly,
    int MaxSavedEncounters);
