namespace StarResonanceDps.App.Config;

public static class AppConfigDefaults
{
    public const int MaxPaletteColorCount = 5;
    public const int MaxRecentColorCount = 10;
    public const int MinClassColorIndex = 0;

    private static readonly string[] DefaultWindowColorHexes =
    [
        "#1F1F1F",
        "#FCFCFC"
    ];

    public static readonly string[] ClassColorKeys =
    [
        "ShieldKnight",
        "HeavyGuardian",
        "VerdantOracle",
        "SoulMusician",
        "FlameBerserker",
        "Stormblade",
        "FrostMage",
        "WindKnight",
        "Marksman",
        "Transformation",
        "Enemy",
        "Unknown"
    ];

    private static readonly Dictionary<string, string[]> DefaultClassColorHexes = new(StringComparer.OrdinalIgnoreCase)
    {
        ["ShieldKnight"] = ["#68A6CD", "#0F68B3"],
        ["HeavyGuardian"] = ["#68A6CD", "#08A0DC"],
        ["VerdantOracle"] = ["#83C49A", "#32BF0F"],
        ["SoulMusician"] = ["#83C49A", "#1F9F0E"],
        ["FlameBerserker"] = ["#DB8787", "#B33000"],
        ["Stormblade"] = ["#DB8787", "#6B39DE"],
        ["FrostMage"] = ["#DB8787", "#5C82E1"],
        ["WindKnight"] = ["#DB8787", "#11B5B2"],
        ["Marksman"] = ["#DB8787", "#D4D116"],
        ["Transformation"] = ["#FFFFFF", "#B06BE8"],
        ["Enemy"] = ["#FFFFFF", "#D95757"],
        ["Unknown"] = ["#FFFFFF", "#A8A8A8"]
    };

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
            NumberDisplayFormatIndex = 0,
            PlayerNameDisplayModeIndex = 0,
            WindowColorIndex = 0,
            WindowColors = CreateDefaultWindowColors(),
            ClassColors = CreateClassColorSettings()
        };
    }

    public static ClassColorSettingsConfig CreateClassColorSettings()
    {
        return new ClassColorSettingsConfig
        {
            ClassColorIndexes = CreateDefaultClassColorIndexes(),
            ClassColorPalettes = CreateDefaultClassColorPalettes()
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

    public static Dictionary<string, int> CreateDefaultClassColorIndexes()
    {
        return ClassColorKeys.ToDictionary(key => key, _ => MinClassColorIndex, StringComparer.OrdinalIgnoreCase);
    }

    public static Dictionary<string, List<string>> CreateDefaultClassColorPalettes()
    {
        return ClassColorKeys.ToDictionary(
            key => key,
            key => CreateDefaultClassColors(key),
            StringComparer.OrdinalIgnoreCase);
    }

    public static List<string> CreateDefaultClassColors(string key)
    {
        return DefaultClassColorHexes.TryGetValue(key, out var colors)
            ? [.. colors]
            : ["#FFFFFF", "#A8A8A8"];
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

    public static ClassColorSettingsConfig CloneNormalizedClassColorSettings(ClassColorSettingsConfig? classColors)
    {
        var normalized = (classColors ?? CreateClassColorSettings()).Clone();
        NormalizeClassColorSettings(normalized);
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

        settings.WindowColors = NormalizeColorList(settings.WindowColors, DefaultWindowColorHexes, MaxPaletteColorCount);
        settings.WindowColorIndex = Clamp(settings.WindowColorIndex, 0, settings.WindowColors.Count - 1);
        settings.ClassColors ??= CreateClassColorSettings();
        NormalizeClassColorSettings(settings.ClassColors);
    }

    public static void NormalizeClassColorSettings(ClassColorSettingsConfig classColors)
    {
        classColors.ClassColorIndexes ??= CreateDefaultClassColorIndexes();
        classColors.ClassColorPalettes ??= CreateDefaultClassColorPalettes();

        var normalizedIndexes = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        var normalizedPalettes = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);

        foreach (var key in ClassColorKeys)
        {
            var defaults = CreateDefaultClassColors(key);
            var sourceColors = classColors.ClassColorPalettes.TryGetValue(key, out var palette)
                ? palette
                : defaults;
            var normalizedPalette = NormalizeColorList(sourceColors, defaults, MaxPaletteColorCount);
            normalizedPalettes[key] = normalizedPalette;

            var selectedIndex = classColors.ClassColorIndexes.TryGetValue(key, out var index)
                ? index
                : MinClassColorIndex;
            normalizedIndexes[key] = Clamp(selectedIndex, MinClassColorIndex, normalizedPalette.Count - 1);
        }

        classColors.ClassColorIndexes = normalizedIndexes;
        classColors.ClassColorPalettes = normalizedPalettes;
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
