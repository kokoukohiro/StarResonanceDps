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
    private SettingsConfig? _settingsPreview;

    private ConfigManager()
    {
        _configPath = AppDataPaths.AppSettingsPath;
        AppConfig = LoadAppConfig();
        ApplyDisplaySettings(AppConfig.Settings);
        ApplyRuntimeSettings(AppConfig.Settings);
    }

    public static ConfigManager Instance => LazyInstance.Value;

    public AppConfig AppConfig { get; }

    /// <summary>起動時に、ファイルはあるのに読めなかった(壊れている・設定が入っていない)か。既定値で動いている。</summary>
    public bool HasLoadFailed { get; private set; }

    public event EventHandler? SettingsChanged;

    public event EventHandler? SettingsPreviewChanged;

    /// <summary><see cref="SaveWidgetWindowTopmostModeIndex"/> が保存した。開いている全体設定の画面が表示と保存済みの控えを合わせる。</summary>
    public event EventHandler? WidgetWindowTopmostModeSaved;

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

    public void SaveSettings(SettingsConfig settings)
    {
        AppConfig.Settings = AppConfigDefaults.CloneNormalizedSettings(settings);
        _settingsPreview = null;
        ApplyDisplaySettings(AppConfig.Settings);
        ApplyRuntimeSettings(AppConfig.Settings);
        Save();
        SettingsChanged?.Invoke(this, EventArgs.Empty);
        SettingsPreviewChanged?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>
    /// 表示設定「ウィジェットウィンドウ」(最前面の選び方)だけを書き換えて保存する。ホットキーの切り替えが使う。
    /// 全体設定の画面で変更中の値(プレビュー)があれば、その中の同じ項目も合わせる。
    /// ほかの項目のプレビューは保存しない(画面の未保存の変更を巻き込まない)。
    /// </summary>
    public void SaveWidgetWindowTopmostModeIndex(int index)
    {
        AppConfig.Settings.WidgetWindowTopmostModeIndex = index;
        if (_settingsPreview is not null)
        {
            _settingsPreview.WidgetWindowTopmostModeIndex = index;
        }

        Save();
        WidgetWindowTopmostModeSaved?.Invoke(this, EventArgs.Empty);
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

    /// <summary>
    /// Core が読む設定を流し込む。<b>プレビューでは呼ばない。</b>
    /// 見た目の下見でキャプチャ先が切り替わったり、戦闘の区切り方が変わっては困る。
    /// </summary>
    private static void ApplyRuntimeSettings(SettingsConfig settings)
    {
        CombatRuntimeSettings.Apply(
            settings.NetCaptureDeviceName,
            settings.GameCapturePreference,
            settings.GameCaptureCustomExeName,
            settings.SplitEncountersOnNewPhases,
            settings.KeepPastEncounterInMeterUntilNextDamage,
            settings.ClearHistorySelectionOnNextEvent,
            settings.DatabaseMaxEncounterCount,
            settings.BenchmarkDurationSeconds,
            settings.BenchmarkFirstTargetOnly);
    }

    /// <summary>
    /// <c>NetworkAdapterSession</c> がアダプターを選び直したときに呼ぶ。
    /// Core が持っている現在値を <c>AppSettings.json</c> へ写して保存する。
    /// </summary>
    public void PersistCaptureSettingsFromRuntime()
    {
        AppConfig.Settings.NetCaptureDeviceName = CombatRuntimeSettings.NetCaptureDeviceName;
        AppConfig.Settings.GameCapturePreference = CombatRuntimeSettings.GameCapturePreference;
        AppConfig.Settings.GameCaptureCustomExeName = CombatRuntimeSettings.GameCaptureCustomExeName;
        Save();
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
        if (!File.Exists(_configPath))
        {
            return AppConfigDefaults.Create();
        }

        try
        {
            var json = File.ReadAllText(_configPath);
            var root = JsonSerializer.Deserialize<AppSettingsRoot>(json, JsonOptions);
            if (root?.Config is not { } config)
            {
                HasLoadFailed = true;
                return AppConfigDefaults.Create();
            }

            AppConfigDefaults.Normalize(config);
            return config;
        }
        catch
        {
            // 読めないファイルは丸ごと既定値で動く。メイン窓が出たところで知らせ(SettingsLoadFailureMessage)、次の保存で上書きする。
            HasLoadFailed = true;
            return AppConfigDefaults.Create();
        }
    }

    private sealed class AppSettingsRoot
    {
        public AppConfig? Config { get; set; }
    }
}
