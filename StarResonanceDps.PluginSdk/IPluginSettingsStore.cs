namespace StarResonanceDps.PluginSdk;

/// <summary>
/// Provides JSON settings files stored in the plugin's own folder (<c>Plugins\&lt;assembly name&gt;\</c>).
/// Each plugin explicitly chooses the file name it owns.
/// </summary>
public interface IPluginSettingsStore
{
    string SettingsDirectory { get; }

    string GetFilePath(string fileName);

    T Load<T>(string fileName) where T : class, new();

    void Save<T>(string fileName, T value) where T : class;
}
