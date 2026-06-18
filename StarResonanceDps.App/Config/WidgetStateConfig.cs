using System.Text.Json.Serialization;
using StarResonanceDps.Core.Models;

namespace StarResonanceDps.App.Config;

public sealed class WidgetStateDocument
{
    public int SchemaVersion { get; set; } = 1;

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
    public int TextColorIndex { get; set; }
    public int WindowOpacity { get; set; } = 100;

    public WidgetThemeConfig Clone()
    {
        return new WidgetThemeConfig
        {
            WindowColorIndex = WindowColorIndex,
            TextColorIndex = TextColorIndex,
            WindowOpacity = WindowOpacity
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

    public MeterWidgetSettingsConfig Clone()
    {
        return new MeterWidgetSettingsConfig
        {
            ClassColorOpacity = ClassColorOpacity,
            ClassColorIndexes = ClassColorIndexes is null
                ? WidgetConfigDefaults.CreateDefaultClassColorIndexes()
                : new Dictionary<string, int>(ClassColorIndexes, StringComparer.OrdinalIgnoreCase)
        };
    }
}

public static class WidgetConfigDefaults
{
    public const int MinColorIndex = 0;
    public const int MaxColorIndex = 4;
    public const int MinWindowOpacity = 0;
    public const int MaxWindowOpacity = 100;
    public const int MinClassColorIndex = 0;
    public const int MaxClassColorIndex = 1;
    public const int MinClassColorOpacity = 0;
    public const int MaxClassColorOpacity = 100;

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
        "Unknown"
    ];

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
            TextColorIndex = 0,
            WindowOpacity = 100
        };
    }

    public static MeterWidgetSettingsConfig CreateMeterSettings()
    {
        return new MeterWidgetSettingsConfig
        {
            ClassColorOpacity = 100,
            ClassColorIndexes = CreateDefaultClassColorIndexes()
        };
    }

    public static Dictionary<string, int> CreateDefaultClassColorIndexes()
    {
        return ClassColorKeys.ToDictionary(key => key, _ => MinClassColorIndex, StringComparer.OrdinalIgnoreCase);
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
        theme.WindowColorIndex = Math.Clamp(theme.WindowColorIndex, MinColorIndex, MaxColorIndex);
        theme.TextColorIndex = Math.Clamp(theme.TextColorIndex, MinColorIndex, MaxColorIndex);
        theme.WindowOpacity = Math.Clamp(theme.WindowOpacity, MinWindowOpacity, MaxWindowOpacity);
    }

    public static void NormalizeMeter(MeterWidgetSettingsConfig meter)
    {
        meter.ClassColorOpacity = Math.Clamp(meter.ClassColorOpacity, MinClassColorOpacity, MaxClassColorOpacity);
        meter.ClassColorIndexes ??= CreateDefaultClassColorIndexes();

        var normalizedIndexes = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        foreach (var key in ClassColorKeys)
        {
            var value = meter.ClassColorIndexes.TryGetValue(key, out var index)
                ? index
                : MinClassColorIndex;
            normalizedIndexes[key] = Math.Clamp(value, MinClassColorIndex, MaxClassColorIndex);
        }

        meter.ClassColorIndexes = normalizedIndexes;
    }

    public static string GetKey(WidgetKind kind)
    {
        return kind.ToString();
    }
}
