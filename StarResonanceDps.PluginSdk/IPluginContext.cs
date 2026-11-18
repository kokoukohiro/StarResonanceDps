namespace StarResonanceDps.PluginSdk;

/// <summary>
/// What the host gives a plugin in <see cref="IStarResonancePlugin.Initialize"/>. Keep it for the plugin's lifetime.
/// </summary>
public interface IPluginContext
{
    /// <summary>JSON settings files in the plugin's own folder.</summary>
    IPluginSettingsStore Settings { get; }

    /// <summary>Message windows with the host's look.</summary>
    IPluginMessageService Messages { get; }

    /// <summary>The host's UI culture and its change event.</summary>
    IPluginLocalizationService Localization { get; }

    /// <summary>The host's log.</summary>
    IPluginLogger Logger { get; }

    /// <summary>The game state the host receives from the game connection.</summary>
    IPluginGameDataService GameData { get; }

    /// <summary>The names the host shows for game IDs.</summary>
    IPluginNameService Names { get; }

    /// <summary>The game messages the host captures.</summary>
    IPluginPacketService Packets { get; }

    /// <summary>The combat events the host processes.</summary>
    IPluginCombatEventService CombatEvents { get; }

    /// <summary>Sends the notifications defined with <see cref="IPluginNotificationProvider"/>.</summary>
    IPluginNotificationService Notifications { get; }

    /// <summary>
    /// The encounters saved to the host's history: listing and reading them, opening one in the host,
    /// and storing the plugin's own data with an encounter.
    /// </summary>
    IPluginHistoryService History { get; }

    /// <summary>
    /// Changes how the host splits encounters and runs the combat clock: splitting the current encounter,
    /// and rules that the host applies while it processes the game's messages.
    /// </summary>
    IPluginEncounterControlService EncounterControl { get; }

    /// <summary>Opens the plugin's own widgets and the host's widgets from plugin code.</summary>
    IPluginWidgetService Widgets { get; }

    /// <summary>
    /// Canceled when the host stops this plugin, before <see cref="IStarResonancePlugin.Shutdown"/>: when the app exits,
    /// when the plugin threw an exception the host caught, when its widget or notification definitions are invalid,
    /// when it did not read its game messages or combat events in time (<see cref="IPluginPacketService"/>, <see cref="IPluginCombatEventService"/>),
    /// and when the user uninstalls it.
    /// Pass it to the plugin's own background work.
    /// </summary>
    CancellationToken Stopping { get; }
}
