using StarResonanceDps.PluginSdk;

namespace StarResonanceDps.App.Services;

internal sealed class PluginHostContext : IPluginContext
{
    public PluginHostContext(
        string pluginId,
        string pluginDataDirectory,
        IPluginLogger logger)
    {
        PluginId = pluginId;
        PluginDataDirectory = pluginDataDirectory;
        Logger = logger;
    }

    public string PluginId { get; }

    public string PluginDataDirectory { get; }

    public IPluginLogger Logger { get; }
}
