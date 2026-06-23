using System.IO;
using System.Text.Json;
using StarResonanceDps.PluginSdk;

namespace StarResonanceDps.App.Services;

internal sealed class PluginSettingsStore : IPluginSettingsStore
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true
    };

    private readonly string _settingsPath;
    private readonly IPluginLogger _logger;

    public PluginSettingsStore(string pluginDataDirectory, IPluginLogger logger)
    {
        _settingsPath = Path.Combine(pluginDataDirectory, "settings.json");
        _logger = logger;
    }

    public T Load<T>() where T : class, new()
    {
        if (!File.Exists(_settingsPath))
        {
            return new T();
        }

        try
        {
            var json = File.ReadAllText(_settingsPath);
            return JsonSerializer.Deserialize<T>(json, JsonOptions) ?? new T();
        }
        catch (Exception exception)
        {
            _logger.Warning($"Failed to read plugin settings. Defaults will be used. {exception.Message}");
            return new T();
        }
    }

    public void Save<T>(T value) where T : class
    {
        ArgumentNullException.ThrowIfNull(value);

        var temporaryPath = $"{_settingsPath}.tmp";
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(_settingsPath)!);
            var json = JsonSerializer.Serialize(value, JsonOptions);
            File.WriteAllText(temporaryPath, json);
            File.Move(temporaryPath, _settingsPath, overwrite: true);
        }
        catch (Exception exception)
        {
            TryDeleteTemporaryFile(temporaryPath);
            _logger.Error("Failed to save plugin settings.", exception);
        }
    }

    private static void TryDeleteTemporaryFile(string path)
    {
        try
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
        catch
        {
            // A failed cleanup must not hide the original settings-write failure.
        }
    }
}
