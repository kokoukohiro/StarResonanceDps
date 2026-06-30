namespace StarResonanceDps.App.Config;

public static class AppConfigDefaults
{
    public const int MaxPaletteColorCount = 5;
    public const int MaxRecentColorCount = 10;

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
            NetworkAdapterIndex = 0,
            LanguageIndex = 0,
            NumberDisplayFormatIndex = 0,
            WindowColorIndex = 0,
            WindowColors = CreateDefaultWindowColors()
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
        settings.NetworkAdapterIndex = Clamp(settings.NetworkAdapterIndex, 0, 1);
        settings.LanguageIndex = Clamp(settings.LanguageIndex, 0, 4);
        settings.NumberDisplayFormatIndex = Clamp(settings.NumberDisplayFormatIndex, 0, 1);

        settings.WindowColors = NormalizeColorList(settings.WindowColors, DefaultWindowColorHexes, MaxPaletteColorCount);
        settings.WindowColorIndex = Clamp(settings.WindowColorIndex, 0, settings.WindowColors.Count - 1);
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
