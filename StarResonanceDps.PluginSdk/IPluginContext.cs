namespace StarResonanceDps.PluginSdk;

public interface IPluginContext
{
    string PluginId { get; }

    string PluginDataDirectory { get; }

    IPluginLogger Logger { get; }
}
