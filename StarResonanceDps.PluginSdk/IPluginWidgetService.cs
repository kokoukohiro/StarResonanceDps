namespace StarResonanceDps.PluginSdk;

/// <summary>
/// Opens widgets from plugin code, for example from a button in the plugin's own widget or window.
///
/// <para>
/// The methods can be called from any thread. The host queues the request to the UI thread and completes the task after it has run,
/// also when called on the UI thread, so a widget can be opened from inside <see cref="IPluginWidgetProvider.CreateWidget"/>.
/// The host activates the window only while a window of this app is active (the user is using the app);
/// otherwise it shows the window without taking the focus from the game.
/// </para>
/// <para>
/// The task results in <c>true</c> when the window is open after the call (opened, or already open and brought to the front),
/// and in <c>false</c> when the widget card does not exist, the plugin's widget content could not be created, or the enemy is not around the player.
/// After the host has stopped the plugin the methods do nothing and result in <c>false</c>.
/// </para>
/// </summary>
public interface IPluginWidgetService
{
    /// <summary>Opens a single-window widget of this plugin (<see cref="IPluginWidgetProvider"/>).</summary>
    /// <param name="widgetId">The <see cref="PluginWidgetDefinition.Id"/>.</param>
    /// <exception cref="ArgumentException">
    /// <paramref name="widgetId"/> is not a widget of <see cref="IPluginWidgetProvider"/> of this plugin,
    /// or the host has not read the definitions yet (called in <see cref="IStarResonancePlugin.Initialize"/>).
    /// </exception>
    Task<bool> OpenAsync(string widgetId);

    /// <summary>Opens the window of a widget that opens a window per target (<see cref="IPluginMultiWidgetProvider"/>), for one target.</summary>
    /// <param name="widgetId">The <see cref="PluginWidgetDefinition.Id"/>.</param>
    /// <param name="targetId">
    /// The <see cref="PluginWidgetTarget.Id"/>. A target that is not in the current list is opened with the widget name only as the title.
    /// </param>
    /// <exception cref="ArgumentException">
    /// <paramref name="widgetId"/> is not a widget of <see cref="IPluginMultiWidgetProvider"/> of this plugin, <paramref name="targetId"/> does not follow the ID rules,
    /// or the host has not read the definitions yet.
    /// </exception>
    Task<bool> OpenAsync(string widgetId, string targetId);

    /// <summary>Opens a host widget that has one window.</summary>
    /// <exception cref="ArgumentException">
    /// <paramref name="widget"/> is not one of <see cref="PluginHostWidgetKind.DpsMeter"/>, <see cref="PluginHostWidgetKind.HpsMeter"/>,
    /// <see cref="PluginHostWidgetKind.HateMeter"/>, <see cref="PluginHostWidgetKind.BreakMeter"/>, <see cref="PluginHostWidgetKind.Timeline"/>,
    /// <see cref="PluginHostWidgetKind.PlayerList"/>, <see cref="PluginHostWidgetKind.EntityList"/>, <see cref="PluginHostWidgetKind.ReviveList"/>,
    /// <see cref="PluginHostWidgetKind.DamageTakenLog"/>, <see cref="PluginHostWidgetKind.Map"/>, <see cref="PluginHostWidgetKind.TargetInfo"/>,
    /// <see cref="PluginHostWidgetKind.PlayerStatus"/> and <see cref="PluginHostWidgetKind.BuffDebuffGroup"/>.
    /// </exception>
    Task<bool> OpenHostAsync(PluginHostWidgetKind widget);

    /// <summary>Opens a host widget that shows one player, for the player.</summary>
    /// <param name="widget">
    /// A widget that shows one player: <see cref="PluginHostWidgetKind.PlayerInfo"/>, <see cref="PluginHostWidgetKind.PlayerEquipment"/>,
    /// <see cref="PluginHostWidgetKind.PlayerTalent"/>, <see cref="PluginHostWidgetKind.PlayerSeasonTalent"/>, <see cref="PluginHostWidgetKind.PlayerModule"/>,
    /// <see cref="PluginHostWidgetKind.BuffList"/>, <see cref="PluginHostWidgetKind.DebuffList"/>, the skill details, graphs and summaries,
    /// and <see cref="PluginHostWidgetKind.BuffUptime"/>.
    /// </param>
    /// <param name="characterId">The character ID of the player.</param>
    /// <exception cref="ArgumentException"><paramref name="widget"/> does not show one player.</exception>
    Task<bool> OpenHostForPlayerAsync(PluginHostWidgetKind widget, long characterId);

    /// <summary>Opens a host widget that shows one enemy, for an enemy that is around the player now.</summary>
    /// <param name="widget"><see cref="PluginHostWidgetKind.TargetInfo"/>, <see cref="PluginHostWidgetKind.BuffList"/> or <see cref="PluginHostWidgetKind.DebuffList"/>.</param>
    /// <param name="entityUuid">The UUID of the enemy (<see cref="PluginEntity.EntityUuid"/>).</param>
    /// <exception cref="ArgumentException"><paramref name="widget"/> does not show one enemy.</exception>
    Task<bool> OpenHostForEntityAsync(PluginHostWidgetKind widget, long entityUuid);
}
