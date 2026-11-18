namespace StarResonanceDps.PluginSdk;

/// <summary>
/// An encounter the host has saved to its history (<see cref="IPluginGameDataService.EncounterSaved"/>).
/// </summary>
/// <param name="EncounterKey">
/// The encounter's <see cref="PluginEncounterSnapshot.EncounterKey"/>: the same as in the result of <see cref="IPluginGameDataService.EncounterClosed"/>
/// and in the objects of the live encounter the plugin read before.
/// </param>
/// <param name="HistoryId">The number the encounter was saved under. Use it with <see cref="IPluginHistoryService"/>.</param>
public sealed record PluginEncounterSaved(long EncounterKey, ulong HistoryId);
