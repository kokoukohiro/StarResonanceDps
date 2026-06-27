namespace StarResonanceDps.PluginSdk;

public interface IPluginContext
{
    string PluginId { get; }

    IPluginSettingsStore Settings { get; }

    IPluginMessageService Messages { get; }

    IPluginLocalizationService Localization { get; }

    IPluginLogger Logger { get; }
}
