using System.Text.Json;
using System.Text.Json.Serialization;
using StarResonanceDps.Core.CombatRuntime;
using StarResonanceDps.Core.CombatRuntime.DataTypes;

namespace StarResonanceDps.App.Config;

public sealed class AppConfig
{
    public WindowBounds? StartUpState { get; set; }

    public SettingsConfig Settings { get; set; } = AppConfigDefaults.CreateSettings();

    public ColorPickerConfig ColorPicker { get; set; } = AppConfigDefaults.CreateColorPicker();
}

public sealed class WindowBounds
{
    public int X { get; set; }
    public int Y { get; set; }
    public int Width { get; set; }
    public int Height { get; set; }
}

public sealed class ColorPickerConfig
{
    public List<string> RecentColors { get; set; } = [];

    public ColorPickerConfig Clone()
    {
        return new ColorPickerConfig
        {
            RecentColors = RecentColors is null ? [] : [.. RecentColors]
        };
    }
}

public sealed class SettingsConfig
{
    public int LanguageIndex { get; set; }

    // --- 基本設定(キャプチャ) ---
    // Core は AppSettings.json を読めないので、起動時と保存時に
    // CombatRuntimeSettings.Apply で流し込む。

    public string NetCaptureDeviceName { get; set; } = CombatRuntimeSettings.AutomaticNetCaptureDeviceName;
    public EGameCapturePreference GameCapturePreference { get; set; } = EGameCapturePreference.Auto;
    public string GameCaptureCustomExeName { get; set; } = string.Empty;

    // --- 集計設定 ---

    public bool SplitEncountersOnNewPhases { get; set; }
    public bool KeepPastEncounterInMeterUntilNextDamage { get; set; }
    public bool ClearHistorySelectionOnNextEvent { get; set; } = true;

    /// <summary>戦闘履歴を残す最大の件数。<b>0 は無限。</b></summary>
    public int DatabaseMaxEncounterCount { get; set; } = 99;
    public int NumberDisplayFormatIndex { get; set; }
    public int PlayerNameDisplayModeIndex { get; set; }
    public int InternalIdDisplayModeIndex { get; set; }

    /// <summary>
    /// ウィジェットの窓を最前面に出す条件。0=常に最前面 / 1=ピン留め時のみ最前面。
    /// 反映は <c>WidgetWindow.ApplyTopmost</c>。
    /// </summary>
    public int WidgetWindowTopmostModeIndex { get; set; } = AppConfigDefaults.AlwaysWidgetWindowTopmostModeIndex;
    public int WindowColorIndex { get; set; }
    public List<string> WindowColors { get; set; } = AppConfigDefaults.CreateDefaultWindowColors();

    [JsonExtensionData]
    public Dictionary<string, JsonElement>? ExtensionData { get; set; }

    public SettingsConfig Clone()
    {
        return new SettingsConfig
        {
            LanguageIndex = LanguageIndex,
            NetCaptureDeviceName = NetCaptureDeviceName,
            GameCapturePreference = GameCapturePreference,
            GameCaptureCustomExeName = GameCaptureCustomExeName,
            SplitEncountersOnNewPhases = SplitEncountersOnNewPhases,
            KeepPastEncounterInMeterUntilNextDamage = KeepPastEncounterInMeterUntilNextDamage,
            ClearHistorySelectionOnNextEvent = ClearHistorySelectionOnNextEvent,
            DatabaseMaxEncounterCount = DatabaseMaxEncounterCount,
            NumberDisplayFormatIndex = NumberDisplayFormatIndex,
            PlayerNameDisplayModeIndex = PlayerNameDisplayModeIndex,
            InternalIdDisplayModeIndex = InternalIdDisplayModeIndex,
            WidgetWindowTopmostModeIndex = WidgetWindowTopmostModeIndex,
            WindowColorIndex = WindowColorIndex,
            WindowColors = WindowColors is null ? AppConfigDefaults.CreateDefaultWindowColors() : [.. WindowColors],
            ExtensionData = CloneExtensionData(ExtensionData)
        };
    }

    private static Dictionary<string, JsonElement>? CloneExtensionData(Dictionary<string, JsonElement>? source)
    {
        if (source is null || source.Count == 0)
        {
            return null;
        }

        return source.ToDictionary(
            pair => pair.Key,
            pair => pair.Value.Clone(),
            StringComparer.OrdinalIgnoreCase);
    }
}
