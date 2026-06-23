namespace StarResonanceDps.PluginSdk;

public interface IPluginSettingsStore
{
    T Load<T>() where T : class, new();

    void Save<T>(T value) where T : class;
}
