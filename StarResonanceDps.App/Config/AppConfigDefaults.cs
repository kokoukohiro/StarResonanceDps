using System.Windows.Input;
using StarResonanceDps.Core.CombatRuntime;
using StarResonanceDps.Core.CombatRuntime.DataTypes;

namespace StarResonanceDps.App.Config;

public static class AppConfigDefaults
{
    public const int MaxPaletteColorCount = 5;
    public const int MaxRecentColorCount = 10;

    /// <summary>ウィジェットの窓を常に最前面に出す(既定)。</summary>
    public const int AlwaysWidgetWindowTopmostModeIndex = 0;

    /// <summary>ウィジェットの窓をピン留め中だけ最前面に出す。</summary>
    public const int PinnedOnlyWidgetWindowTopmostModeIndex = 1;

    private static readonly string[] DefaultWindowColorHexes =
    [
        "#1F1F1F",
        "#FCFCFC"
    ];

    public static AppConfig Create()
    {
        return new AppConfig
        {
            StartUpState = null,
            Settings = CreateSettings(),
            ColorPicker = CreateColorPicker()
        };
    }

    public static SettingsConfig CreateSettings()
    {
        return new SettingsConfig
        {
            LanguageIndex = 0,
            NetCaptureDeviceName = CombatRuntimeSettings.AutomaticNetCaptureDeviceName,
            GameCapturePreference = EGameCapturePreference.Auto,
            GameCaptureCustomExeName = string.Empty,
            SplitEncountersOnNewPhases = true,
            KeepPastEncounterInMeterUntilNextDamage = false,
            ClearHistorySelectionOnNextEvent = true,
            DatabaseMaxEncounterCount = 99,
            NumberDisplayFormatIndex = 0,
            PlayerNameDisplayModeIndex = 0,
            InternalIdDisplayModeIndex = 0,
            WidgetWindowTopmostModeIndex = AlwaysWidgetWindowTopmostModeIndex,
            WindowColorIndex = 1,
            WindowColors = CreateDefaultWindowColors(),
            Hotkeys = CreateDefaultHotkeys()
        };
    }

    /// <summary>ホットキーの既定。お気に入り F8 / ピン留め F7 / クリック透過 F6 / 3分計測 F10 / リセット F9。</summary>
    public static HotkeySettingsConfig CreateDefaultHotkeys()
    {
        return new HotkeySettingsConfig
        {
            StartFavoritesOrStopAll = HotkeyBindingConfig.Create(Key.F8),
            PinRunningOrUnpinAll = HotkeyBindingConfig.Create(Key.F7),
            ClickThroughPinnedOrClearAll = HotkeyBindingConfig.Create(Key.F6),
            ThreeMinuteBenchmark = HotkeyBindingConfig.Create(Key.F10),
            ResetEncounter = HotkeyBindingConfig.Create(Key.F9)
        };
    }

    public static ColorPickerConfig CreateColorPicker()
    {
        return new ColorPickerConfig
        {
            RecentColors = []
        };
    }

    public static List<string> CreateDefaultWindowColors()
    {
        return [.. DefaultWindowColorHexes];
    }

    public static void Normalize(AppConfig config)
    {
        config.Settings ??= CreateSettings();
        config.ColorPicker ??= CreateColorPicker();
        NormalizeSettings(config.Settings);
        NormalizeColorPicker(config.ColorPicker);
    }

    public static SettingsConfig CloneNormalizedSettings(SettingsConfig settings)
    {
        var normalized = settings.Clone();
        NormalizeSettings(normalized);
        return normalized;
    }

    public static ColorPickerConfig CloneNormalizedColorPicker(ColorPickerConfig colorPicker)
    {
        var normalized = colorPicker.Clone();
        NormalizeColorPicker(normalized);
        return normalized;
    }

    public static void NormalizeSettings(SettingsConfig settings)
    {
        settings.LanguageIndex = Clamp(settings.LanguageIndex, 0, 4);
        settings.NumberDisplayFormatIndex = Clamp(settings.NumberDisplayFormatIndex, 0, 1);
        settings.PlayerNameDisplayModeIndex = Clamp(settings.PlayerNameDisplayModeIndex, 0, 2);
        settings.InternalIdDisplayModeIndex = Clamp(settings.InternalIdDisplayModeIndex, 0, 5);
        settings.WidgetWindowTopmostModeIndex = Clamp(
            settings.WidgetWindowTopmostModeIndex,
            AlwaysWidgetWindowTopmostModeIndex,
            PinnedOnlyWidgetWindowTopmostModeIndex);
        settings.WindowColors = NormalizeColorList(settings.WindowColors, DefaultWindowColorHexes, MaxPaletteColorCount);
        settings.WindowColorIndex = Clamp(settings.WindowColorIndex, 0, settings.WindowColors.Count - 1);

        settings.NetCaptureDeviceName = string.IsNullOrWhiteSpace(settings.NetCaptureDeviceName)
            ? CombatRuntimeSettings.AutomaticNetCaptureDeviceName
            : settings.NetCaptureDeviceName.Trim();
        settings.GameCaptureCustomExeName = settings.GameCaptureCustomExeName?.Trim() ?? string.Empty;

        // 0 は無限。選べる最大は 99 件。
        settings.DatabaseMaxEncounterCount = Clamp(settings.DatabaseMaxEncounterCount, 0, 99);

        NormalizeHotkeys(settings);
    }

    /// <summary>
    /// 項目そのものが無ければ既定にする。読めないキー・修飾キーだけのキーは割り当てなし(空欄)にし、
    /// 修飾キーは Ctrl・Alt・Shift だけ残す。
    /// </summary>
    private static void NormalizeHotkeys(SettingsConfig settings)
    {
        var defaults = CreateDefaultHotkeys();
        settings.Hotkeys ??= defaults.Clone();

        foreach (var action in System.Enum.GetValues<HotkeyAction>())
        {
            var binding = settings.Hotkeys.Get(action);
            if (binding is null)
            {
                settings.Hotkeys.Set(action, defaults.Get(action).Clone());
                continue;
            }

            if (!System.Enum.IsDefined(binding.Key) || HotkeyBindingConfig.IsModifierKey(binding.Key))
            {
                binding.Key = Key.None;
            }

            binding.Modifiers = binding.IsAssigned
                ? binding.Modifiers & HotkeyBindingConfig.SupportedModifiers
                : ModifierKeys.None;
        }
    }

    public static void NormalizeColorPicker(ColorPickerConfig colorPicker)
    {
        colorPicker.RecentColors = NormalizeColorList(colorPicker.RecentColors, [], MaxRecentColorCount, allowEmpty: true);
    }

    private static List<string> NormalizeColorList(
        IEnumerable<string>? colors,
        IEnumerable<string> fallback,
        int maxCount,
        bool allowEmpty = false)
    {
        var result = new List<string>();

        if (colors is not null)
        {
            foreach (var color in colors)
            {
                if (!TryNormalizeHexColor(color, out var normalized)
                    || result.Contains(normalized, StringComparer.OrdinalIgnoreCase))
                {
                    continue;
                }

                result.Add(normalized);
                if (result.Count >= maxCount)
                {
                    break;
                }
            }
        }

        if (result.Count == 0 && !allowEmpty)
        {
            foreach (var color in fallback)
            {
                if (!TryNormalizeHexColor(color, out var normalized)
                    || result.Contains(normalized, StringComparer.OrdinalIgnoreCase))
                {
                    continue;
                }

                result.Add(normalized);
                if (result.Count >= maxCount)
                {
                    break;
                }
            }
        }

        if (result.Count == 0 && !allowEmpty)
        {
            result.Add("#FFFFFF");
        }

        return result;
    }

    private static bool TryNormalizeHexColor(string? raw, out string normalized)
    {
        normalized = string.Empty;
        if (string.IsNullOrWhiteSpace(raw))
        {
            return false;
        }

        var text = raw.Trim();
        if (text.StartsWith("#", StringComparison.Ordinal))
        {
            text = text[1..];
        }

        if (text.Length != 6)
        {
            return false;
        }

        foreach (var ch in text)
        {
            if (!Uri.IsHexDigit(ch))
            {
                return false;
            }
        }

        normalized = "#" + text.ToUpperInvariant();
        return true;
    }

    private static int Clamp(int value, int min, int max)
    {
        return Math.Min(Math.Max(value, min), max);
    }
}
