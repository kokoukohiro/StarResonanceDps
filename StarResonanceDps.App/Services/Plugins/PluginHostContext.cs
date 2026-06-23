using StarResonanceDps.PluginSdk;

namespace StarResonanceDps.App.Services;

internal sealed class PluginHostContext : IPluginContext
{
    public PluginHostContext(
        string pluginId,
        string pluginDataDirectory,
        IPluginSettingsStore settings,
        IPluginLogger logger)
    {
        PluginId = pluginId;
        PluginDataDirectory = pluginDataDirectory;
        Settings = settings;
        Logger = logger;
    }

    public string PluginId { get; }

    public string PluginDataDirectory { get; }

    public IPluginSettingsStore Settings { get; }

    public IPluginLogger Logger { get; }
}
