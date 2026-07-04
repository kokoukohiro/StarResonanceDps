using System.IO;
using System.Text.Json;
using StarResonanceDps.Core.Models;
using StarResonanceDps.Core.Services;

namespace StarResonanceDps.App.Config;

public sealed class ConfigManager
{
    private static readonly Lazy<ConfigManager> LazyInstance = new(() => new ConfigManager());
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true
    };

    private readonly string _configPath;
    private readonly string _legacyConfigPath;

    private ConfigManager()
    {
        _configPath = AppDataPaths.AppSettingsPath;
        _legacyConfigPath = AppDataPaths.GetLegacyAppSettingsPath();
        AppConfig = LoadAppConfig();
        ApplyPlayerNameDisplayMode();
    }

    public static ConfigManager Instance => LazyInstance.Value;

    public AppConfig AppConfig { get; }

    public event EventHandler? SettingsChanged;

    public SettingsConfig GetSettingsSnapshot()
    {
        AppConfigDefaults.Normalize(AppConfig);
        return AppConfig.Settings.Clone();
    }

    public ColorPickerConfig GetColorPickerSnapshot()
    {
        AppConfigDefaults.Normalize(AppConfig);
        return AppConfig.ColorPicker.Clone();
    }

    public void SaveSettings(SettingsConfig settings)
    {
        AppConfig.Settings = AppConfigDefaults.CloneNormalizedSettings(settings);
        ApplyPlayerNameDisplayMode();
        Save();
        SettingsChanged?.Invoke(this, EventArgs.Empty);
    }

    public void SaveColorPicker(ColorPickerConfig colorPicker)
    {
        AppConfig.ColorPicker = AppConfigDefaults.CloneNormalizedColorPicker(colorPicker);
        Save();
    }

    public void Save()
    {
        AppConfigDefaults.Normalize(AppConfig);

        var directory = Path.GetDirectoryName(_configPath);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        var root = new AppSettingsRoot
        {
            Config = AppConfig
        };

        var json = JsonSerializer.Serialize(root, JsonOptions);
        File.WriteAllText(_configPath, json);
    }

    private void ApplyPlayerNameDisplayMode()
    {
        PlayerRosterPresentationStore.Instance.SetNameDisplayMode(
            (PlayerNameDisplayMode)AppConfig.Settings.PlayerNameDisplayModeIndex);
    }

    private AppConfig LoadAppConfig()
    {
        var loadPath = GetReadableConfigPath();
        if (loadPath is null)
        {
            return AppConfigDefaults.Create();
        }

        try
        {
            var json = File.ReadAllText(loadPath);
            var root = JsonSerializer.Deserialize<AppSettingsRoot>(json, JsonOptions);
            var config = root?.Config ?? AppConfigDefaults.Create();
            AppConfigDefaults.Normalize(config);
            return config;
        }
        catch
        {
            return AppConfigDefaults.Create();
        }
    }

    private string? GetReadableConfigPath()
    {
        if (File.Exists(_configPath))
        {
            return _configPath;
        }

        return File.Exists(_legacyConfigPath)
            ? _legacyConfigPath
            : null;
    }

    private sealed class AppSettingsRoot
    {
        public AppConfig? Config { get; set; }
    }
}
