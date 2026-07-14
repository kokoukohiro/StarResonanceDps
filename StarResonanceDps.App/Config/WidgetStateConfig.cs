using System.Text.Json.Serialization;
using StarResonanceDps.App.Models.Widgets;

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

    public WidgetState? State { get; set; }
    public WidgetThemeConfig Theme { get; set; } = WidgetConfigDefaults.CreateTheme();
    public WidgetWindowConfig Window { get; set; } = new();
    public MeterWidgetSettingsConfig? Meter { get; set; }
    public MetricTimelineWidgetSettingsConfig? MetricTimeline { get; set; }

    [JsonExtensionData]
    public Dictionary<string, object>? ExtensionData { get; set; }

    public WidgetConfig Clone()
    {
        return new WidgetConfig
        {
            IsFavorite = IsFavorite,
            IsPinned = IsPinned,
            State = State,
            Theme = Theme?.Clone() ?? WidgetConfigDefaults.CreateTheme(),
            Window = Window?.Clone() ?? new WidgetWindowConfig(),
            Meter = Meter?.Clone(),
            MetricTimeline = MetricTimeline?.Clone(),
            ExtensionData = ExtensionData is null
                ? null
                : new Dictionary<string, object>(ExtensionData, StringComparer.OrdinalIgnoreCase)
        };
    }
}

public sealed class WidgetThemeConfig
{
    public int WindowColorIndex { get; set; }
    public int WindowOpacity { get; set; } = 50;
    public List<string> WindowColors { get; set; } = WidgetConfigDefaults.CreateDefaultWindowColors();
    public string? BackgroundImagePath { get; set; }
    public string? BackgroundImageAverageColor { get; set; }
    public string? BackgroundImageAverageColorSourcePath { get; set; }

    public WidgetThemeConfig Clone()
    {
        return new WidgetThemeConfig
        {
            WindowColorIndex = WindowColorIndex,
            WindowOpacity = WindowOpacity,
            WindowColors = WindowColors is null ? WidgetConfigDefaults.CreateDefaultWindowColors() : [.. WindowColors],
            BackgroundImagePath = BackgroundImagePath,
            BackgroundImageAverageColor = BackgroundImageAverageColor,
            BackgroundImageAverageColorSourcePath = BackgroundImageAverageColorSourcePath
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

public sealed class MetricTimelineWidgetSettingsConfig
{
    public int AggregationIntervalSeconds { get; set; } = WidgetConfigDefaults.DefaultMetricTimelineAggregationIntervalSeconds;

    public MetricTimelineWidgetSettingsConfig Clone()
    {
        return new MetricTimelineWidgetSettingsConfig
        {
            AggregationIntervalSeconds = AggregationIntervalSeconds
        };
    }
}

public sealed class MeterWidgetSettingsConfig
{
    public string? PlayerInfoFormatString { get; set; }

    public int HealthValueDisplayModeIndex { get; set; } = WidgetConfigDefaults.DefaultHealthValueDisplayModeIndex;

    public int ClassColorOpacity { get; set; } = WidgetConfigDefaults.MaxClassColorOpacity;

    public Dictionary<string, int> ClassColorIndexes { get; set; } = WidgetConfigDefaults.CreateDefaultClassColorIndexes();

    public Dictionary<string, List<string>> ClassColorPalettes { get; set; } = WidgetConfigDefaults.CreateDefaultClassColorPalettes(WidgetKind.PlayerList);

    public MeterWidgetSettingsConfig Clone()
    {
        return new MeterWidgetSettingsConfig
        {
            PlayerInfoFormatString = PlayerInfoFormatString,
            HealthValueDisplayModeIndex = HealthValueDisplayModeIndex,
            ClassColorOpacity = ClassColorOpacity,
            ClassColorIndexes = ClassColorIndexes is null
                ? WidgetConfigDefaults.CreateDefaultClassColorIndexes()
                : new Dictionary<string, int>(ClassColorIndexes, StringComparer.OrdinalIgnoreCase),
            ClassColorPalettes = ClassColorPalettes is null
                ? WidgetConfigDefaults.CreateDefaultClassColorPalettes(WidgetKind.PlayerList)
                : ClassColorPalettes.ToDictionary(
                    pair => pair.Key,
                    pair => pair.Value is null ? new List<string>() : new List<string>(pair.Value),
                    StringComparer.OrdinalIgnoreCase)
        };
    }
}

public static class WidgetConfigDefaults
{
    public const int CurrentSchemaVersion = 1;
    public const int MaxPaletteColorCount = 5;
    public const int MinColorIndex = 0;
    public const int MinWindowOpacity = 0;
    public const int MaxWindowOpacity = 100;
    public const int MinClassColorIndex = 0;
    public const int MinClassColorOpacity = 0;
    public const int MaxClassColorOpacity = 100;
    public const int DefaultMetricTimelineAggregationIntervalSeconds = 10;
    public const int DefaultHealthValueDisplayModeIndex = 0;
    public const int SeparateShieldHealthValueDisplayModeIndex = 1;
    public const string DefaultEntityInfoFormatString = "Lv.{Level} {Name}";
    public const string DefaultMeterPlayerInfoFormatString = "{Name} - {Spec} ({PowerLevel}-{SeasonStrength})";
    public const string DefaultPlayerListPlayerInfoFormatString = "{Name}({PowerLevel}-{SeasonStrength})";

    private const double PlayerListInitialWindowWidth = 360d;
    private const double PlayerListInitialWindowHeight = 400d;
    private const double PlayerInfoInitialWindowWidth = 360d;
    private const double PlayerInfoInitialWindowHeight = 200d;
    private const double PlayerStatusInitialWindowWidth = 400d;
    private const double PlayerStatusInitialWindowHeight = 230d;
    private const double PlayerEquipmentInitialWindowWidth = 400d;
    private const double PlayerEquipmentInitialWindowHeight = 230d;
    private const double PlayerImagineRoleSkillInitialWindowWidth = 632d;
    private const double PlayerImagineRoleSkillInitialWindowHeight = 160d;
    private const double PlayerSkillInfoInitialWindowWidth = 620d;
    private const double PlayerSkillInfoInitialWindowHeight = 360d;
    private const double PlayerBuffListInitialWindowWidth = 360d;
    private const double PlayerBuffListInitialWindowHeight = 92d;
    private const double MetricContributionInitialWindowWidth = 980d;
    private const double MetricContributionInitialWindowHeight = 360d;
    private const double MetricSummaryInitialWindowWidth = 720d;
    private const double MetricSummaryInitialWindowHeight = 180d;
    private const double MetricTimelineInitialWindowWidth = 980d;
    private const double MetricTimelineInitialWindowHeight = 420d;

    public static IReadOnlyList<int> MetricTimelineAggregationIntervals { get; } = [10, 5, 3, 2, 1];

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
        "Unknown"
    ];

    public static readonly string[] EntityClassColorKeys =
    [
        "Monster",
        "Elite",
        "Boss",
        "Unknown"
    ];

    private static readonly Dictionary<string, string[]> PlayerListDefaultClassColorHexes = new(StringComparer.OrdinalIgnoreCase)
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
        ["Unknown"] = ["#FFFFFF", "#A8A8A8"]
    };

    private static readonly Dictionary<string, string[]> EntityListDefaultClassColorHexes = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Monster"] = ["#FFFFFF", "#FFB15C"],
        ["Elite"] = ["#FFFFFF", "#C490FF"],
        ["Boss"] = ["#FFFFFF", "#FF6A6A"],
        ["Unknown"] = ["#FFFFFF", "#A8A8A8"]
    };

    private static readonly Dictionary<string, string[]> MeterDefaultClassColorHexes = new(StringComparer.OrdinalIgnoreCase)
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
        ["Unknown"] = ["#A8A8A8", "#707070"]
    };

    private static readonly Dictionary<string, string[]> HpsMeterDefaultClassColorHexes = new(StringComparer.OrdinalIgnoreCase)
    {
        ["ShieldKnight"] = ["#63B64D", "#395F2C"],
        ["HeavyGuardian"] = ["#24B78E", "#0F5F4A"],
        ["VerdantOracle"] = ["#45D52C", "#227A20"],
        ["SoulMusician"] = ["#20A860", "#145B39"],
        ["FlameBerserker"] = ["#A4BF2A", "#63721D"],
        ["Stormblade"] = ["#10C576", "#0A6C44"],
        ["FrostMage"] = ["#76D8B0", "#467E68"],
        ["WindKnight"] = ["#19C7BA", "#0E726B"],
        ["Marksman"] = ["#C0D829", "#728119"],
        ["Transformation"] = ["#7AD957", "#4B7F37"],
        ["Unknown"] = ["#8BB58C", "#566F57"]
    };

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

    public static bool SupportsMeterSettings(WidgetKind kind)
    {
        return kind is WidgetKind.PlayerList
            or WidgetKind.EntityList
            or WidgetKind.DpsMeter
            or WidgetKind.HpsMeter;
    }

    public static bool UsesMeterClassColorOpacity(WidgetKind kind)
    {
        return kind is WidgetKind.DpsMeter or WidgetKind.HpsMeter;
    }

    public static bool SupportsMetricTimelineSettings(WidgetKind kind)
    {
        return kind is WidgetKind.DpsGraph or WidgetKind.HpsGraph;
    }

    public static void MigrateVersion1Defaults(WidgetConfig config)
    {
        config.Theme ??= CreateTheme();

        if (UsesLegacyWindowColorPalette(config.Theme.WindowColors))
        {
            config.Theme.WindowColors = CreateDefaultWindowColors();
            config.Theme.WindowColorIndex = MinColorIndex;
        }
    }

    public static void MigratePlayerListFormatDefault(WidgetConfig config)
    {
        if (config.Meter is not { } meter
            || !string.Equals(
                meter.PlayerInfoFormatString,
                DefaultMeterPlayerInfoFormatString,
                StringComparison.Ordinal))
        {
            return;
        }

        meter.PlayerInfoFormatString = DefaultPlayerListPlayerInfoFormatString;
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

    public static WidgetConfig Create(WidgetKind kind)
    {
        return new WidgetConfig
        {
            IsFavorite = false,
            IsPinned = false,
            State = WidgetState.Stopped,
            Theme = CreateTheme(),
            Window = CreateDefaultWindowConfig(kind),
            Meter = SupportsMeterSettings(kind) ? CreateMeterSettings(kind) : null,
            MetricTimeline = SupportsMetricTimelineSettings(kind) ? CreateMetricTimelineSettings() : null
        };
    }

    private static WidgetWindowConfig CreateDefaultWindowConfig(WidgetKind kind)
    {
        return kind switch
        {
            WidgetKind.PlayerList or WidgetKind.EntityList => new WidgetWindowConfig
            {
                Width = PlayerListInitialWindowWidth,
                Height = PlayerListInitialWindowHeight
            },
            WidgetKind.DpsMeter or WidgetKind.HpsMeter => new WidgetWindowConfig
            {
                Width = PlayerListInitialWindowWidth,
                Height = PlayerListInitialWindowHeight
            },
            WidgetKind.PlayerInfo => new WidgetWindowConfig
            {
                Width = PlayerInfoInitialWindowWidth,
                Height = PlayerInfoInitialWindowHeight
            },
            WidgetKind.PlayerStatus => new WidgetWindowConfig
            {
                Width = PlayerStatusInitialWindowWidth,
                Height = PlayerStatusInitialWindowHeight
            },
            WidgetKind.PlayerEquipment => new WidgetWindowConfig
            {
                Width = PlayerEquipmentInitialWindowWidth,
                Height = PlayerEquipmentInitialWindowHeight
            },
            WidgetKind.ImagineRoleSkillInfo => new WidgetWindowConfig
            {
                Width = PlayerImagineRoleSkillInitialWindowWidth,
                Height = PlayerImagineRoleSkillInitialWindowHeight
            },
            WidgetKind.SkillInfo => new WidgetWindowConfig
            {
                Width = PlayerSkillInfoInitialWindowWidth,
                Height = PlayerSkillInfoInitialWindowHeight
            },
            WidgetKind.BuffList or WidgetKind.DebuffList => new WidgetWindowConfig
            {
                Width = PlayerBuffListInitialWindowWidth,
                Height = PlayerBuffListInitialWindowHeight
            },
            WidgetKind.DamageContribution or WidgetKind.HealingContribution => new WidgetWindowConfig
            {
                Width = MetricContributionInitialWindowWidth,
                Height = MetricContributionInitialWindowHeight
            },
            WidgetKind.DamageSummary or WidgetKind.HealingSummary => new WidgetWindowConfig
            {
                Width = MetricSummaryInitialWindowWidth,
                Height = MetricSummaryInitialWindowHeight
            },
            WidgetKind.DpsGraph or WidgetKind.HpsGraph => new WidgetWindowConfig
            {
                Width = MetricTimelineInitialWindowWidth,
                Height = MetricTimelineInitialWindowHeight
            },
            _ => new WidgetWindowConfig()
        };
    }

    public static WidgetThemeConfig CreateTheme()
    {
        return new WidgetThemeConfig
        {
            WindowColorIndex = 0,
            WindowOpacity = 50,
            WindowColors = CreateDefaultWindowColors()
        };
    }

    public static MetricTimelineWidgetSettingsConfig CreateMetricTimelineSettings()
    {
        return new MetricTimelineWidgetSettingsConfig
        {
            AggregationIntervalSeconds = DefaultMetricTimelineAggregationIntervalSeconds
        };
    }

    public static MeterWidgetSettingsConfig CreateMeterSettings(WidgetKind kind)
    {
        return new MeterWidgetSettingsConfig
        {
            PlayerInfoFormatString = GetDefaultPlayerInfoFormatString(kind),
            HealthValueDisplayModeIndex = DefaultHealthValueDisplayModeIndex,
            ClassColorOpacity = MaxClassColorOpacity,
            ClassColorIndexes = CreateDefaultClassColorIndexes(kind),
            ClassColorPalettes = CreateDefaultClassColorPalettes(kind)
        };
    }

    public static List<string> CreateDefaultWindowColors()
    {
        return [.. DefaultWindowColorHexes];
    }

    public static string GetDefaultPlayerInfoFormatString(WidgetKind kind)
    {
        return kind switch
        {
            WidgetKind.EntityList => DefaultEntityInfoFormatString,
            WidgetKind.PlayerList => DefaultPlayerListPlayerInfoFormatString,
            _ => DefaultMeterPlayerInfoFormatString
        };
    }

    public static IReadOnlyList<string> GetClassColorKeys(WidgetKind kind)
    {
        return kind == WidgetKind.EntityList
            ? EntityClassColorKeys
            : ClassColorKeys;
    }

    public static Dictionary<string, int> CreateDefaultClassColorIndexes(WidgetKind kind = WidgetKind.PlayerList)
    {
        return GetClassColorKeys(kind)
            .ToDictionary(key => key, _ => MinClassColorIndex, StringComparer.OrdinalIgnoreCase);
    }

    public static Dictionary<string, List<string>> CreateDefaultClassColorPalettes(WidgetKind kind)
    {
        return GetClassColorKeys(kind).ToDictionary(
            key => key,
            key => CreateDefaultClassColors(kind, key),
            StringComparer.OrdinalIgnoreCase);
    }

    public static List<string> CreateDefaultClassColors(WidgetKind kind, string key)
    {
        var source = kind switch
        {
            WidgetKind.PlayerList => PlayerListDefaultClassColorHexes,
            WidgetKind.EntityList => EntityListDefaultClassColorHexes,
            WidgetKind.HpsMeter => HpsMeterDefaultClassColorHexes,
            _ => MeterDefaultClassColorHexes
        };

        return source.TryGetValue(key, out var colors)
            ? [.. colors]
            : ["#A8A8A8", "#707070"];
    }

    public static WidgetConfig CloneNormalized(WidgetKind kind, WidgetConfig? config)
    {
        var normalized = (config ?? Create(kind)).Clone();
        Normalize(kind, normalized);
        return normalized;
    }

    public static WidgetThemeConfig CloneNormalizedTheme(WidgetThemeConfig? theme)
    {
        var normalized = (theme ?? CreateTheme()).Clone();
        NormalizeTheme(normalized);
        return normalized;
    }

    public static MeterWidgetSettingsConfig CloneNormalizedMeter(WidgetKind kind, MeterWidgetSettingsConfig? meter)
    {
        var normalized = (meter ?? CreateMeterSettings(kind)).Clone();
        NormalizeMeter(kind, normalized);
        return normalized;
    }

    public static MetricTimelineWidgetSettingsConfig CloneNormalizedMetricTimeline(MetricTimelineWidgetSettingsConfig? metricTimeline)
    {
        var normalized = (metricTimeline ?? CreateMetricTimelineSettings()).Clone();
        NormalizeMetricTimeline(normalized);
        return normalized;
    }

    public static void Normalize(WidgetConfig config)
    {
        config.Theme ??= CreateTheme();
        config.Window ??= new WidgetWindowConfig();

        if (config.State is { } state
            && state is not WidgetState.Stopped
            && state is not WidgetState.Running)
        {
            config.State = WidgetState.Stopped;
        }

        NormalizeTheme(config.Theme);
    }

    public static void Normalize(WidgetKind kind, WidgetConfig config)
    {
        Normalize(config);
        config.Meter = SupportsMeterSettings(kind)
            ? CloneNormalizedMeter(kind, config.Meter)
            : null;
        config.MetricTimeline = SupportsMetricTimelineSettings(kind)
            ? CloneNormalizedMetricTimeline(config.MetricTimeline)
            : null;

        switch (kind)
        {
            case WidgetKind.PlayerList:
            case WidgetKind.EntityList:
                config.Window.Width ??= PlayerListInitialWindowWidth;
                config.Window.Height ??= PlayerListInitialWindowHeight;
                break;

            case WidgetKind.DpsMeter:
            case WidgetKind.HpsMeter:
                config.Window.Width ??= PlayerListInitialWindowWidth;
                config.Window.Height ??= PlayerListInitialWindowHeight;
                break;

            case WidgetKind.PlayerInfo:
                config.Window.Width ??= PlayerInfoInitialWindowWidth;
                config.Window.Height ??= PlayerInfoInitialWindowHeight;
                break;

            case WidgetKind.PlayerStatus:
                config.Window.Width ??= PlayerStatusInitialWindowWidth;
                config.Window.Height ??= PlayerStatusInitialWindowHeight;
                break;

            case WidgetKind.PlayerEquipment:
                config.Window.Width ??= PlayerEquipmentInitialWindowWidth;
                config.Window.Height ??= PlayerEquipmentInitialWindowHeight;
                break;
            case WidgetKind.ImagineRoleSkillInfo:
                config.Window.Width ??= PlayerImagineRoleSkillInitialWindowWidth;
                config.Window.Height ??= PlayerImagineRoleSkillInitialWindowHeight;
                break;
            case WidgetKind.SkillInfo:
                config.Window.Width ??= PlayerSkillInfoInitialWindowWidth;
                config.Window.Height ??= PlayerSkillInfoInitialWindowHeight;
                break;
            case WidgetKind.BuffList:
            case WidgetKind.DebuffList:
                config.Window.Width ??= PlayerBuffListInitialWindowWidth;
                config.Window.Height ??= PlayerBuffListInitialWindowHeight;
                break;
            case WidgetKind.DamageContribution:
            case WidgetKind.HealingContribution:
                config.Window.Width ??= MetricContributionInitialWindowWidth;
                config.Window.Height ??= MetricContributionInitialWindowHeight;
                break;
            case WidgetKind.DamageSummary:
            case WidgetKind.HealingSummary:
                config.Window.Width ??= MetricSummaryInitialWindowWidth;
                config.Window.Height ??= MetricSummaryInitialWindowHeight;
                break;
            case WidgetKind.DpsGraph:
            case WidgetKind.HpsGraph:
                config.Window.Width ??= MetricTimelineInitialWindowWidth;
                config.Window.Height ??= MetricTimelineInitialWindowHeight;
                break;
        }
    }

    public static void NormalizeTheme(WidgetThemeConfig theme)
    {
        theme.WindowColors = NormalizeColorList(theme.WindowColors, DefaultWindowColorHexes, MaxPaletteColorCount);
        theme.WindowColorIndex = Math.Clamp(theme.WindowColorIndex, MinColorIndex, theme.WindowColors.Count - 1);
        theme.WindowOpacity = Math.Clamp(theme.WindowOpacity, MinWindowOpacity, MaxWindowOpacity);
        theme.BackgroundImagePath = string.IsNullOrWhiteSpace(theme.BackgroundImagePath)
            ? null
            : theme.BackgroundImagePath.Trim();

        if (theme.BackgroundImagePath is null)
        {
            theme.BackgroundImageAverageColor = null;
            theme.BackgroundImageAverageColorSourcePath = null;
            return;
        }

        theme.BackgroundImageAverageColor = TryNormalizeHexColor(theme.BackgroundImageAverageColor, out var averageColor)
            ? averageColor
            : null;
        theme.BackgroundImageAverageColorSourcePath = string.IsNullOrWhiteSpace(theme.BackgroundImageAverageColorSourcePath)
            ? null
            : theme.BackgroundImageAverageColorSourcePath.Trim();
    }

    public static void NormalizeMetricTimeline(MetricTimelineWidgetSettingsConfig metricTimeline)
    {
        if (!MetricTimelineAggregationIntervals.Contains(metricTimeline.AggregationIntervalSeconds))
        {
            metricTimeline.AggregationIntervalSeconds = DefaultMetricTimelineAggregationIntervalSeconds;
        }
    }

    public static void NormalizeMeter(WidgetKind kind, MeterWidgetSettingsConfig meter)
    {
        meter.PlayerInfoFormatString ??= GetDefaultPlayerInfoFormatString(kind);
        meter.HealthValueDisplayModeIndex = Math.Clamp(
            meter.HealthValueDisplayModeIndex,
            DefaultHealthValueDisplayModeIndex,
            SeparateShieldHealthValueDisplayModeIndex);
        meter.ClassColorOpacity = UsesMeterClassColorOpacity(kind)
            ? Math.Clamp(meter.ClassColorOpacity, MinClassColorOpacity, MaxClassColorOpacity)
            : MaxClassColorOpacity;
        meter.ClassColorIndexes ??= CreateDefaultClassColorIndexes(kind);
        meter.ClassColorPalettes ??= CreateDefaultClassColorPalettes(kind);

        var normalizedIndexes = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        var normalizedPalettes = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);

        foreach (var key in GetClassColorKeys(kind))
        {
            var defaultColors = CreateDefaultClassColors(kind, key);
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
