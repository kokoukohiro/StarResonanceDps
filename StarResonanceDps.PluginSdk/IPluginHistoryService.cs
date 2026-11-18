namespace StarResonanceDps.PluginSdk;

/// <summary>
/// The encounters the host saved to its history (the history of the Aggregation tab): listing and reading them without opening them,
/// opening one in the host, and storing the plugin's own data with an encounter so that it can be read again from the history.
/// The host implements it.
///
/// <para>
/// Methods can be called from any thread. <see cref="GetEntriesAsync"/>, <see cref="LoadAsync"/> and <see cref="SetSavedEncounterDataAsync"/>
/// run on a background thread, one call at a time for each plugin: a call waits until the plugin's earlier calls of these three have finished.
/// <see cref="OpenFailedChanged"/> is raised on the UI thread.
/// </para>
///
/// <para>
/// After the host has stopped the plugin (including during <see cref="IStarResonancePlugin.Shutdown"/>), the methods do nothing:
/// the tasks end with <see cref="PluginHistoryStatus.Unavailable"/> or <see cref="PluginHistoryOpenResult.Unavailable"/>,
/// <see cref="ReturnToLiveAsync"/> ends without switching, <see cref="TrySetEncounterData"/> returns <c>false</c>,
/// and <see cref="KeepHistoryOpen"/> returns an object that keeps nothing.
/// After the app has started to exit, the tasks also end that way.
/// The arguments of <see cref="TrySetEncounterData"/> and <see cref="SetSavedEncounterDataAsync"/> are still checked first,
/// so an invalid key or too long data throws <see cref="ArgumentException"/> even then.
/// </para>
///
/// <para>
/// The plugin's data is bytes stored under keys. Keys follow the ID rules (1 to 128 ASCII letters, digits, <c>.</c>, <c>-</c> or <c>_</c>)
/// and are matched without case. A plugin reads and writes only its own data (<see cref="IPluginEncounterRecord.GetPluginData"/>).
/// For one plugin and one encounter, the data is at most <see cref="PluginEncounterDataLimits.MaxKeys"/> keys and
/// <see cref="PluginEncounterDataLimits.MaxTotalBytes"/> bytes in total.
/// The data is deleted with its encounter (the general setting "Max History Entries"); until then it is kept, also when the plugin is uninstalled.
/// </para>
/// </summary>
public interface IPluginHistoryService
{
    /// <summary>
    /// Lists the saved encounters, newest first (the order of the Aggregation tab's history), without opening them.
    /// The host deletes the oldest encounters over the general setting "Max History Entries" when it saves an encounter and when the app exits.
    /// The task ends with <see cref="PluginHistoryStatus.Failed"/> when the history could not be read (the host logs the reason).
    /// </summary>
    /// <param name="cancellationToken">
    /// Checked before and after reading the history; reading itself is not interrupted.
    /// When it is canceled, the task ends with <see cref="OperationCanceledException"/>.
    /// </param>
    Task<PluginHistoryListResult> GetEntriesAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Reads one saved encounter without opening it: the host's meters, widgets and the Aggregation tab do not change.
    /// Read the result with <see cref="PluginHistoryLoadResult.Encounter"/>; its <see cref="IPluginEncounterRecord.IsHistory"/> is <c>true</c>.
    /// The task ends with <see cref="PluginHistoryStatus.NotFound"/> when no saved encounter has the number
    /// (for example, it was deleted by "Max History Entries"), and with <see cref="PluginHistoryStatus.Failed"/> when it could not be read.
    /// The encounter does not change after it is read: data written later with <see cref="SetSavedEncounterDataAsync"/> is not in it.
    /// </summary>
    /// <param name="historyId">
    /// The number in the saved history (<see cref="PluginHistoryEntry.HistoryId"/>, <see cref="PluginEncounterSaved.HistoryId"/>).
    /// </param>
    /// <param name="cancellationToken">The same as in <see cref="GetEntriesAsync"/>.</param>
    Task<PluginHistoryLoadResult> LoadAsync(ulong historyId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Opens a saved encounter in the host, the same as choosing it in the Aggregation tab's history:
    /// the meters, the other widgets and <see cref="IPluginGameDataService.GetEncounter"/> show it.
    /// The encounter is read on a background thread, and the task ends with <see cref="PluginHistoryOpenResult.Opened"/>
    /// when the host has queued showing it; <see cref="IPluginGameDataService.ShownEncounterChanged"/> follows when it is shown.
    /// When another encounter or the live encounter is chosen while it is read, the task ends with <see cref="PluginHistoryOpenResult.Superseded"/>;
    /// when one is chosen after <see cref="PluginHistoryOpenResult.Opened"/> but before it is shown, the encounter is not shown.
    /// When the host stops the plugin before the encounter is shown and nothing else has been chosen since, the host withdraws the request,
    /// and the encounter is not shown.
    /// Check what is shown with <see cref="IPluginGameDataService.GetShownEncounter"/>.
    /// With the general setting "Auto-exit History View" on, the host returns to the live encounter at the next live event
    /// unless a plugin holds <see cref="KeepHistoryOpen"/>.
    /// </summary>
    /// <param name="historyId">The number in the saved history.</param>
    Task<PluginHistoryOpenResult> OpenAsync(ulong historyId);

    /// <summary>
    /// Returns the host to the live encounter, the same as the Aggregation tab's button.
    /// The task ends after the host has switched; when the host already shows the live encounter, nothing changes.
    /// When switching failed, the task ends with the host's exception (the host also logs it).
    /// </summary>
    Task ReturnToLiveAsync();

    /// <summary>
    /// Keeps the opened history encounter until the returned object is disposed: even with the general setting "Auto-exit History View" on,
    /// the host does not return to the live encounter at live events. Returning with <see cref="ReturnToLiveAsync"/>,
    /// with the Aggregation tab, and at logout still happens. Each call returns a new object; disposing it a second time does nothing.
    /// After the objects of all plugins are disposed, the host returns at the next live event, as the setting says.
    /// The host disposes the plugin's objects when it stops the plugin.
    /// </summary>
    IDisposable KeepHistoryOpen();

    /// <summary>
    /// The host could not read the encounter of the latest request to open one (by the user or with <see cref="OpenAsync"/>):
    /// an exception, or no such saved encounter. The shown encounter stays as it was.
    /// It becomes <c>false</c> when an encounter is opened or the host returns to the live encounter.
    /// <see cref="LoadAsync"/> does not change it.
    /// </summary>
    bool HasOpenFailed { get; }

    /// <summary>
    /// Raised on the UI thread after <see cref="HasOpenFailed"/> has changed. Changes in a short time are combined into one call.
    /// </summary>
    event EventHandler? OpenFailedChanged;

    /// <summary>
    /// Stores data with an encounter the host has not saved yet: the current live encounter, or the encounter the host is closing.
    /// The host saves the data with the encounter, in the same database transaction. Empty data removes the key. The data is copied.
    /// Returns <c>false</c> when the encounter is neither, for example after the host has saved it;
    /// use <see cref="SetSavedEncounterDataAsync"/> after <see cref="IPluginGameDataService.EncounterSaved"/>.
    /// An encounter without records is not saved, and its data is discarded with it.
    /// When the app exits, the host saves the last encounter before it stops the plugins, so a call in
    /// <see cref="IStarResonancePlugin.Shutdown"/> returns <c>false</c>.
    /// Throws <see cref="ArgumentException"/> when <paramref name="key"/> does not follow the key rules or <paramref name="data"/>
    /// is longer than <see cref="PluginEncounterDataLimits.MaxTotalBytes"/>, and <see cref="InvalidOperationException"/>
    /// when the plugin's data for the encounter would exceed <see cref="PluginEncounterDataLimits.MaxKeys"/> or
    /// <see cref="PluginEncounterDataLimits.MaxTotalBytes"/>.
    /// </summary>
    /// <param name="encounterKey">
    /// The encounter's <see cref="PluginEncounterSnapshot.EncounterKey"/> (for example, of <see cref="IPluginGameDataService.GetLiveEncounter"/>).
    /// </param>
    /// <param name="key">The key.</param>
    /// <param name="data">The data to store under the key.</param>
    bool TrySetEncounterData(long encounterKey, string key, ReadOnlyMemory<byte> data);

    /// <summary>
    /// Stores data with a saved encounter, replacing the data under the key. Empty data removes the key. The data is copied.
    /// The task ends with <see cref="PluginHistoryStatus.NotFound"/> when no saved encounter has the number,
    /// and with <see cref="PluginHistoryStatus.Failed"/> when the database could not be written (the host logs the reason).
    /// The host's opened history encounter and the previous encounter the meters keep showing get the new data too;
    /// encounters read earlier with <see cref="LoadAsync"/>, and objects of live encounters the host no longer shows, do not.
    /// Throws <see cref="ArgumentException"/> when <paramref name="key"/> does not follow the key rules or <paramref name="data"/>
    /// is longer than <see cref="PluginEncounterDataLimits.MaxTotalBytes"/>.
    /// The task ends with <see cref="InvalidOperationException"/> when the plugin's data for the encounter would exceed the limits.
    /// </summary>
    /// <param name="historyId">The number in the saved history.</param>
    /// <param name="key">The key.</param>
    /// <param name="data">The data to store under the key.</param>
    /// <param name="cancellationToken">Checked before writing. When it is canceled, the task ends with <see cref="OperationCanceledException"/>.</param>
    Task<PluginHistoryStatus> SetSavedEncounterDataAsync(
        ulong historyId,
        string key,
        ReadOnlyMemory<byte> data,
        CancellationToken cancellationToken = default);
}
