namespace StarResonanceDps.PluginSdk;

public interface IPluginContext
{
    string PluginId { get; }

    string PluginDataDirectory { get; }

    IPluginSettingsStore Settings { get; }

    IPluginLogger Logger { get; }
}
