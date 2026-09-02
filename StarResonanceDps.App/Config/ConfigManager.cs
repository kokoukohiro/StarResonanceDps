using System.IO;
using System.Text.Json;
using StarResonanceDps.Core.CombatRuntime;
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
    private SettingsConfig? _settingsPreview;

    private ConfigManager()
    {
        _configPath = AppDataPaths.AppSettingsPath;
        _legacyConfigPath = AppDataPaths.GetLegacyAppSettingsPath();
        AppConfig = LoadAppConfig();
        ApplyDisplaySettings(AppConfig.Settings);
    }

    public static ConfigManager Instance => LazyInstance.Value;

    public AppConfig AppConfig { get; }

    public event EventHandler? SettingsChanged;

    public event EventHandler? SettingsPreviewChanged;

    public SettingsConfig GetSettingsSnapshot()
    {
        AppConfigDefaults.Normalize(AppConfig);
        return AppConfigDefaults.CloneNormalizedSettings(_settingsPreview ?? AppConfig.Settings);
    }

    public void SetSettingsPreview(SettingsConfig settings)
    {
        _settingsPreview = AppConfigDefaults.CloneNormalizedSettings(settings);
        ApplyDisplaySettings(_settingsPreview);
        SettingsPreviewChanged?.Invoke(this, EventArgs.Empty);
    }

    public void ClearSettingsPreview()
    {
        if (_settingsPreview is null)
        {
            return;
        }

        _settingsPreview = null;
        ApplyDisplaySettings(AppConfig.Settings);
        SettingsPreviewChanged?.Invoke(this, EventArgs.Empty);
    }

    public ColorPickerConfig GetColorPickerSnapshot()
    {
        AppConfigDefaults.Normalize(AppConfig);
        return AppConfig.ColorPicker.Clone();
    }

    public MeterWidgetSettingsConfig? TakeLegacyClassColorSettings()
    {
        var extensionData = AppConfig.Settings.ExtensionData;
        var classColorsKey = extensionData?
            .Keys
            .FirstOrDefault(key => string.Equals(key, "ClassColors", StringComparison.OrdinalIgnoreCase));
        if (extensionData is null
            || classColorsKey is null
            || !extensionData.TryGetValue(classColorsKey, out var value)
            || value.ValueKind is JsonValueKind.Undefined or JsonValueKind.Null)
        {
            return null;
        }

        MeterWidgetSettingsConfig? settings = null;
        try
        {
            settings = JsonSerializer.Deserialize<MeterWidgetSettingsConfig>(value.GetRawText(), JsonOptions);
        }
        catch (JsonException)
        {
        }

        extensionData.Remove(classColorsKey);
        if (extensionData.Count == 0)
        {
            AppConfig.Settings.ExtensionData = null;
        }

        Save();
        return settings;
    }

    public void SaveSettings(SettingsConfig settings)
    {
        AppConfig.Settings = AppConfigDefaults.CloneNormalizedSettings(settings);
        _settingsPreview = null;
        ApplyDisplaySettings(AppConfig.Settings);
        Save();
        SettingsChanged?.Invoke(this, EventArgs.Empty);
        SettingsPreviewChanged?.Invoke(this, EventArgs.Empty);
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

    private static void ApplyDisplaySettings(SettingsConfig settings)
    {
        ApplyPlayerNameDisplayMode(settings);
        ApplyInternalIdDisplayMode(settings);
    }

    private static void ApplyInternalIdDisplayMode(SettingsConfig settings)
    {
        CombatDataCatalog.SetInternalIdDisplay(
            (InternalIdDisplayMode)settings.InternalIdDisplayModeIndex);
    }

    private static void ApplyPlayerNameDisplayMode(SettingsConfig settings)
    {
        PlayerRosterPresentationStore.Instance.SetNameDisplayMode(
            (PlayerNameDisplayMode)settings.PlayerNameDisplayModeIndex);
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
