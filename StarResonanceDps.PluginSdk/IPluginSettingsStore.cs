namespace StarResonanceDps.PluginSdk;

/// <summary>
/// Provides JSON settings files stored directly in the runtime Plugins directory.
/// Each plugin explicitly chooses the file name it owns.
/// </summary>
public interface IPluginSettingsStore
{
    string SettingsDirectory { get; }

    string GetFilePath(string fileName);

    T Load<T>(string fileName) where T : class, new();

    void Save<T>(string fileName, T value) where T : class;
}
