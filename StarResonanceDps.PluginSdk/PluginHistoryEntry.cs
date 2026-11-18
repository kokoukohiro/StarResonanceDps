namespace StarResonanceDps.PluginSdk;

/// <summary>
/// One saved encounter in the history (<see cref="IPluginHistoryService.GetEntriesAsync"/>).
/// </summary>
/// <param name="HistoryId">
/// The number in the saved history. Pass it to <see cref="IPluginHistoryService.LoadAsync"/> and <see cref="IPluginHistoryService.OpenAsync"/>.
/// </param>
/// <param name="Info">
/// The scene, server, benchmark and other values of the encounter. <see cref="PluginEncounterInfo.BenchmarkFirstTargetOnly"/> is <c>null</c>.
/// </param>
/// <param name="StartUtc">When the combat clock started (UTC), or <c>null</c> when it did not start.</param>
/// <param name="Elapsed">The combat time (paused intervals excluded).</param>
public sealed record PluginHistoryEntry(ulong HistoryId, PluginEncounterInfo Info, DateTime? StartUtc, TimeSpan Elapsed);

/// <summary>
/// The result of <see cref="IPluginHistoryService.GetEntriesAsync"/>.
/// </summary>
/// <param name="Status">Whether the history could be read.</param>
/// <param name="Entries">The saved encounters, newest first. Empty unless <paramref name="Status"/> is <see cref="PluginHistoryStatus.Succeeded"/>.</param>
public sealed record PluginHistoryListResult(PluginHistoryStatus Status, IReadOnlyList<PluginHistoryEntry> Entries);

/// <summary>
/// The result of <see cref="IPluginHistoryService.LoadAsync"/>.
/// </summary>
/// <param name="Status">Whether the encounter could be read.</param>
/// <param name="Encounter">The encounter when <paramref name="Status"/> is <see cref="PluginHistoryStatus.Succeeded"/>; otherwise <c>null</c>.</param>
public sealed record PluginHistoryLoadResult(PluginHistoryStatus Status, IPluginEncounterRecord? Encounter);

/// <summary>
/// How reading or writing the saved history ended.
/// </summary>
public enum PluginHistoryStatus
{
    /// <summary>It succeeded.</summary>
    Succeeded = 0,

    /// <summary>No saved encounter has the number (for example, it was deleted by the general setting "Max History Entries").</summary>
    NotFound,

    /// <summary>The database could not be read or written. The host logs the reason.</summary>
    Failed,

    /// <summary>Nothing was done, because the host has stopped the plugin or the app has started to exit.</summary>
    Unavailable
}

/// <summary>
/// How <see cref="IPluginHistoryService.OpenAsync"/> ended.
/// </summary>
public enum PluginHistoryOpenResult
{
    /// <summary>The encounter was read, and the host has queued showing it.</summary>
    Opened = 0,

    /// <summary>No saved encounter has the number. What the host shows does not change.</summary>
    NotFound,

    /// <summary>Another encounter or the live encounter was chosen while the encounter was read. It is not shown.</summary>
    Superseded,

    /// <summary>The encounter could not be read (the host logs the reason). What the host shows does not change.</summary>
    Failed,

    /// <summary>Nothing was done, because the host has stopped the plugin or the app has started to exit.</summary>
    Unavailable
}
