using System.IO;
using System.Text.Json;
using StarResonanceDps.PluginSdk;

namespace StarResonanceDps.App.Services;

/// <summary>
/// Shared JSON settings storage for plugins.
/// The host fixes the directory to the runtime Plugins directory; plugins supply only
/// the file name they own, so no plugin-specific Data subdirectories are created.
/// </summary>
internal sealed class PluginSettingsStore : IPluginSettingsStore
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true
    };

    private readonly IPluginLogger _logger;

    public PluginSettingsStore(string settingsDirectory, IPluginLogger logger)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(settingsDirectory);
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        SettingsDirectory = Path.GetFullPath(settingsDirectory);
    }

    public string SettingsDirectory { get; }

    public string GetFilePath(string fileName)
    {
        return Path.Combine(SettingsDirectory, ValidateFileName(fileName));
    }

    public T Load<T>(string fileName) where T : class, new()
    {
        var settingsPath = GetFilePath(fileName);
        if (!File.Exists(settingsPath))
        {
            return new T();
        }

        try
        {
            var json = File.ReadAllText(settingsPath);
            return JsonSerializer.Deserialize<T>(json, JsonOptions) ?? new T();
        }
        catch (Exception exception)
        {
            _logger.Warning($"Failed to read plugin settings file '{Path.GetFileName(settingsPath)}'. {exception.Message}");
            throw new InvalidDataException(
                $"プラグイン設定ファイルを読み込めません。{Path.GetFileName(settingsPath)}",
                exception);
        }
    }

    public void Save<T>(string fileName, T value) where T : class
    {
        ArgumentNullException.ThrowIfNull(value);

        var settingsPath = GetFilePath(fileName);
        var temporaryPath = $"{settingsPath}.tmp";
        try
        {
            Directory.CreateDirectory(SettingsDirectory);
            var json = JsonSerializer.Serialize(value, JsonOptions);
            File.WriteAllText(temporaryPath, json);
            File.Move(temporaryPath, settingsPath, overwrite: true);
        }
        catch (Exception exception)
        {
            TryDeleteTemporaryFile(temporaryPath);
            _logger.Error($"Failed to save plugin settings file '{Path.GetFileName(settingsPath)}'.", exception);
            throw;
        }
    }

    private static string ValidateFileName(string fileName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(fileName);

        var trimmedFileName = fileName.Trim();
        if (trimmedFileName is "." or ".."
            || Path.IsPathRooted(trimmedFileName)
            || trimmedFileName.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0
            || trimmedFileName.Contains(Path.DirectorySeparatorChar)
            || trimmedFileName.Contains(Path.AltDirectorySeparatorChar)
            || trimmedFileName.Contains('\\')
            || trimmedFileName.Contains('/'))
        {
            throw new ArgumentException("Plugin settings file names must be plain file names.", nameof(fileName));
        }

        return trimmedFileName;
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
