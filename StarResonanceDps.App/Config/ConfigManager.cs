using System.IO;
using System.Text.Json;

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

    private ConfigManager()
    {
        _configPath = Path.Combine(AppContext.BaseDirectory, "appsettings.json");
        AppConfig = LoadAppConfig();
    }

    public static ConfigManager Instance => LazyInstance.Value;

    public AppConfig AppConfig { get; }

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
        Save();
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

    private AppConfig LoadAppConfig()
    {
        if (!File.Exists(_configPath))
        {
            return AppConfigDefaults.Create();
        }

        try
        {
            var json = File.ReadAllText(_configPath);
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

    private sealed class AppSettingsRoot
    {
        public AppConfig? Config { get; set; }
    }
}
