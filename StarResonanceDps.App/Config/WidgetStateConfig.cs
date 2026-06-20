using System.Text.Json.Serialization;
using StarResonanceDps.Core.Models;

namespace StarResonanceDps.App.Config;

public sealed class WidgetStateDocument
{
    public int SchemaVersion { get; set; } = WidgetConfigDefaults.CurrentSchemaVersion;

    public Dictionary<string, WidgetConfig> Widgets { get; set; } = new(StringComparer.OrdinalIgnoreCase);
}

public sealed class WidgetConfig
{
    public bool IsFavorite { get; set; }
    public bool IsPinned { get; set; }
    public WidgetThemeConfig Theme { get; set; } = WidgetConfigDefaults.CreateTheme();
    public WidgetWindowConfig Window { get; set; } = new();
    public MeterWidgetSettingsConfig Meter { get; set; } = WidgetConfigDefaults.CreateMeterSettings();

    [JsonExtensionData]
    public Dictionary<string, object>? ExtensionData { get; set; }

    public WidgetConfig Clone()
    {
        return new WidgetConfig
        {
            IsFavorite = IsFavorite,
            IsPinned = IsPinned,
            Theme = Theme?.Clone() ?? WidgetConfigDefaults.CreateTheme(),
            Window = Window?.Clone() ?? new WidgetWindowConfig(),
            Meter = Meter?.Clone() ?? WidgetConfigDefaults.CreateMeterSettings(),
            ExtensionData = ExtensionData is null
                ? null
                : new Dictionary<string, object>(ExtensionData, StringComparer.OrdinalIgnoreCase)
        };
    }
}

public sealed class WidgetThemeConfig
{
    public int WindowColorIndex { get; set; }
    public int WindowOpacity { get; set; } = 100;
    public List<string> WindowColors { get; set; } = WidgetConfigDefaults.CreateDefaultWindowColors();

    public WidgetThemeConfig Clone()
    {
        return new WidgetThemeConfig
        {
            WindowColorIndex = WindowColorIndex,
            WindowOpacity = WindowOpacity,
            WindowColors = WindowColors is null ? WidgetConfigDefaults.CreateDefaultWindowColors() : [.. WindowColors]
        };
    }
}

public sealed class WidgetWindowConfig
{
    public double? X { get; set; }
    public double? Y { get; set; }
    public double? Width { get; set; }
    public double? Height { get; set; }

    public WidgetWindowConfig Clone()
    {
        return new WidgetWindowConfig
        {
            X = X,
            Y = Y,
            Width = Width,
            Height = Height
        };
    }
}

public sealed class MeterWidgetSettingsConfig
{
    public int ClassColorOpacity { get; set; } = 100;

    public Dictionary<string, int> ClassColorIndexes { get; set; } = WidgetConfigDefaults.CreateDefaultClassColorIndexes();

    public Dictionary<string, List<string>> ClassColorPalettes { get; set; } = WidgetConfigDefaults.CreateDefaultClassColorPalettes();

    public MeterWidgetSettingsConfig Clone()
    {
        return new MeterWidgetSettingsConfig
        {
            ClassColorOpacity = ClassColorOpacity,
            ClassColorIndexes = ClassColorIndexes is null
                ? WidgetConfigDefaults.CreateDefaultClassColorIndexes()
                : new Dictionary<string, int>(ClassColorIndexes, StringComparer.OrdinalIgnoreCase),
            ClassColorPalettes = ClassColorPalettes is null
                ? WidgetConfigDefaults.CreateDefaultClassColorPalettes()
                : ClassColorPalettes.ToDictionary(
                    pair => pair.Key,
                    pair => pair.Value is null ? new List<string>() : new List<string>(pair.Value),
                    StringComparer.OrdinalIgnoreCase)
        };
    }
}

public static class WidgetConfigDefaults
{
    public const int CurrentSchemaVersion = 2;
    public const int MaxPaletteColorCount = 5;
    public const int MinColorIndex = 0;
    public const int MinWindowOpacity = 0;
    public const int MaxWindowOpacity = 100;
    public const int MinClassColorIndex = 0;
    public const int MinClassColorOpacity = 0;
    public const int MaxClassColorOpacity = 100;

    private static readonly string[] DefaultWindowColorHexes =
    [
        "#0B1624",
        "#FFFFFF",
        "#FFF450",
        "#E5A1B3",
        "#CDECFF"
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
        ["ShieldKnight"] = ["#0F68B3", "#08406F"],
        ["HeavyGuardian"] = ["#08A0DC", "#056482"],
        ["VerdantOracle"] = ["#32BF0F", "#1D7410"],
        ["SoulMusician"] = ["#1F9F0E", "#145F0A"],
        ["FlameBerserker"] = ["#B33000", "#6F1F00"],
        ["Stormblade"] = ["#6B39DE", "#3F2485"],
        ["FrostMage"] = ["#5C82E1", "#355094"],
        ["WindKnight"] = ["#11B5B2", "#0A6E6C"],
        ["Marksman"] = ["#D4D116", "#8A8810"],
        ["Transformation"] = ["#B06BE8", "#6E3A9C"],
        ["Enemy"] = ["#D95757", "#7E3030"],
        ["Unknown"] = ["#A8A8A8", "#707070"]
    };

    // Version 1 values shipped as the initial widget-state.json palette.  Keep this
    // separately so migration only replaces the known defaults and never overwrites
    // user-created colors.
    private static readonly HashSet<string> LegacyWidgetWindowColorHexes = new(StringComparer.OrdinalIgnoreCase)
    {
        "#2297F4",
        "#7C5CFF",
        "#9FD14A",
        "#FF9F2E",
        "#F05284",
        "#000000",
        "#FFFFFF"
    };

    private static readonly Dictionary<string, string[]> LegacyClassColorHexes = new(StringComparer.OrdinalIgnoreCase)
    {
        ["ShieldKnight"] = ["#1E8EF5", "#0F4D87"],
        ["HeavyGuardian"] = ["#C95A13", "#78340D"],
        ["VerdantOracle"] = ["#32BF0F", "#1D7410"],
        ["SoulMusician"] = ["#1F9F0E", "#145F0A"],
        ["FlameBerserker"] = ["#B33000", "#6F1F00"],
        ["Stormblade"] = ["#6B39DE", "#3F2485"],
        ["FrostMage"] = ["#47B7FF", "#226F9E"],
        ["WindKnight"] = ["#1F9FDE", "#145F85"],
        ["Marksman"] = ["#D4D116", "#8A8810"],
        ["Unknown"] = ["#A8A8A8", "#707070"]
    };

    public static void MigrateVersion1Defaults(WidgetConfig config)
    {
        config.Theme ??= CreateTheme();
        config.Meter ??= CreateMeterSettings();

        if (UsesLegacyWindowColorPalette(config.Theme.WindowColors))
        {
            config.Theme.WindowColors = CreateDefaultWindowColors();
            config.Theme.WindowColorIndex = MinColorIndex;
        }

        config.Meter.ClassColorPalettes ??= CreateDefaultClassColorPalettes();
        config.Meter.ClassColorIndexes ??= CreateDefaultClassColorIndexes();

        foreach (var (key, legacyPalette) in LegacyClassColorHexes)
        {
            if (!config.Meter.ClassColorPalettes.TryGetValue(key, out var palette)
                || !ColorSequencesEqual(palette, legacyPalette))
            {
                continue;
            }

            config.Meter.ClassColorPalettes[key] = CreateDefaultClassColors(key);
            config.Meter.ClassColorIndexes[key] = MinClassColorIndex;
        }
    }

    private static bool UsesLegacyWindowColorPalette(IEnumerable<string>? colors)
    {
        if (colors is null)
        {
            return false;
        }

        var normalized = new List<string>();
        foreach (var color in colors)
        {
            if (!TryNormalizeHexColor(color, out var value))
            {
                return false;
            }

            normalized.Add(value);
        }

        return normalized.Count == MaxPaletteColorCount
            && normalized.All(LegacyWidgetWindowColorHexes.Contains);
    }

    private static bool ColorSequencesEqual(IEnumerable<string>? colors, IReadOnlyList<string> expected)
    {
        if (colors is null)
        {
            return false;
        }

        var normalized = new List<string>();
        foreach (var color in colors)
        {
            if (!TryNormalizeHexColor(color, out var value))
            {
                return false;
            }

            normalized.Add(value);
        }

        return normalized.Count == expected.Count
            && normalized.SequenceEqual(expected, StringComparer.OrdinalIgnoreCase);
    }

    public static WidgetConfig Create(WidgetKind kind)
    {
        return new WidgetConfig
        {
            IsFavorite = false,
            IsPinned = false,
            Theme = CreateTheme(),
            Window = new WidgetWindowConfig(),
            Meter = CreateMeterSettings()
        };
    }

    public static WidgetThemeConfig CreateTheme()
    {
        return new WidgetThemeConfig
        {
            WindowColorIndex = 0,
            WindowOpacity = 100,
            WindowColors = CreateDefaultWindowColors()
        };
    }

    public static MeterWidgetSettingsConfig CreateMeterSettings()
    {
        return new MeterWidgetSettingsConfig
        {
            ClassColorOpacity = 100,
            ClassColorIndexes = CreateDefaultClassColorIndexes(),
            ClassColorPalettes = CreateDefaultClassColorPalettes()
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
            : ["#A8A8A8", "#707070"];
    }

    public static WidgetConfig CloneNormalized(WidgetKind kind, WidgetConfig? config)
    {
        var normalized = (config ?? Create(kind)).Clone();
        Normalize(normalized);
        return normalized;
    }

    public static WidgetThemeConfig CloneNormalizedTheme(WidgetThemeConfig? theme)
    {
        var normalized = (theme ?? CreateTheme()).Clone();
        NormalizeTheme(normalized);
        return normalized;
    }

    public static MeterWidgetSettingsConfig CloneNormalizedMeter(MeterWidgetSettingsConfig? meter)
    {
        var normalized = (meter ?? CreateMeterSettings()).Clone();
        NormalizeMeter(normalized);
        return normalized;
    }

    public static void Normalize(WidgetConfig config)
    {
        config.Theme ??= CreateTheme();
        config.Window ??= new WidgetWindowConfig();
        config.Meter ??= CreateMeterSettings();
        NormalizeTheme(config.Theme);
        NormalizeMeter(config.Meter);
    }

    public static void NormalizeTheme(WidgetThemeConfig theme)
    {
        theme.WindowColors = NormalizeColorList(theme.WindowColors, DefaultWindowColorHexes, MaxPaletteColorCount);
        theme.WindowColorIndex = Math.Clamp(theme.WindowColorIndex, MinColorIndex, theme.WindowColors.Count - 1);
        theme.WindowOpacity = Math.Clamp(theme.WindowOpacity, MinWindowOpacity, MaxWindowOpacity);
    }

    public static void NormalizeMeter(MeterWidgetSettingsConfig meter)
    {
        meter.ClassColorOpacity = Math.Clamp(meter.ClassColorOpacity, MinClassColorOpacity, MaxClassColorOpacity);
        meter.ClassColorIndexes ??= CreateDefaultClassColorIndexes();
        meter.ClassColorPalettes ??= CreateDefaultClassColorPalettes();

        var normalizedIndexes = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        var normalizedPalettes = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);

        foreach (var key in ClassColorKeys)
        {
            var defaultColors = CreateDefaultClassColors(key);
            var sourceColors = meter.ClassColorPalettes.TryGetValue(key, out var colors)
                ? colors
                : defaultColors;
            var palette = NormalizeColorList(sourceColors, defaultColors, MaxPaletteColorCount);
            normalizedPalettes[key] = palette;

            var selectedIndex = meter.ClassColorIndexes.TryGetValue(key, out var index)
                ? index
                : MinClassColorIndex;
            normalizedIndexes[key] = Math.Clamp(selectedIndex, MinClassColorIndex, palette.Count - 1);
        }

        meter.ClassColorIndexes = normalizedIndexes;
        meter.ClassColorPalettes = normalizedPalettes;
    }

    public static string GetKey(WidgetKind kind)
    {
        return kind.ToString();
    }

    private static List<string> NormalizeColorList(IEnumerable<string>? colors, IEnumerable<string> fallback, int maxCount)
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

        if (result.Count == 0)
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

        if (result.Count == 0)
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
}
