namespace StarResonanceDps.PluginSdk;

public interface IPluginContext
{
    string PluginId { get; }

    IPluginSettingsStore Settings { get; }

    IPluginMessageService Messages { get; }

    IPluginLocalizationService Localization { get; }

    IPluginLogger Logger { get; }

    IPluginGameDataService GameData { get; }

    IPluginNameService Names { get; }

    IPluginPacketService Packets { get; }

    /// <summary>Sends the notifications defined with <see cref="IPluginNotificationProvider"/>.</summary>
    IPluginNotificationService Notifications { get; }

    /// <summary>
    /// Canceled when the host stops this plugin (the app exits, or the plugin threw an exception the host caught).
    /// Pass it to the plugin's own background work.
    /// </summary>
    CancellationToken Stopping { get; }
}
