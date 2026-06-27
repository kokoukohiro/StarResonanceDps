using StarResonanceDps.PluginSdk;

namespace StarResonanceDps.App.Services;

internal sealed class PluginHostContext : IPluginContext
{
    public PluginHostContext(
        string pluginId,
        IPluginSettingsStore settings,
        IPluginMessageService messages,
        IPluginLocalizationService localization,
        IPluginLogger logger)
    {
        PluginId = pluginId;
        Settings = settings ?? throw new ArgumentNullException(nameof(settings));
        Messages = messages ?? throw new ArgumentNullException(nameof(messages));
        Localization = localization ?? throw new ArgumentNullException(nameof(localization));
        Logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public string PluginId { get; }

    public IPluginSettingsStore Settings { get; }

    public IPluginMessageService Messages { get; }

    public IPluginLocalizationService Localization { get; }

    public IPluginLogger Logger { get; }
}
