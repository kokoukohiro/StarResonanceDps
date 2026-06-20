using System.Windows;
using System.Windows.Media;
using StarResonanceDps.App.Config;

namespace StarResonanceDps.App.Services;

public sealed class ThemeManager
{
    private static readonly Lazy<ThemeManager> LazyInstance = new(() => new ThemeManager());

    private ThemeManager()
    {
    }

    public static ThemeManager Instance => LazyInstance.Value;

    public void ApplyGlobalTheme(SettingsConfig settings)
    {
        var normalized = AppConfigDefaults.CloneNormalizedSettings(settings);
        var selectedHex = normalized.WindowColors[normalized.WindowColorIndex];

        if (!ColorUtilities.TryParseHex(selectedHex, out var windowSurface))
        {
            return;
        }

        ApplyWindowSurface(windowSurface);
    }

    public void ApplyWindowSurface(Color windowSurface)
    {
        Apply(ThemeColorPalette.Create(windowSurface));
    }

    private static void Apply(ThemeColorPalette colors)
    {
        var resources = Application.Current?.Resources;
        if (resources is null)
        {
            return;
        }

        SetThemeColor(resources, "Color.WindowSurface", "Brush.WindowSurface", colors.WindowSurface);
        SetThemeColor(resources, "Color.PanelBackground", "Brush.PanelBackground", colors.PanelBackground);
        SetThemeColor(resources, "Color.Border", "Brush.Border", colors.Border);
        SetThemeColor(resources, "Color.BorderSoft", "Brush.BorderSoft", colors.BorderSoft);
        SetThemeColor(resources, "Color.ControlBackground", "Brush.ControlBackground", colors.ControlBackground);
        SetThemeColor(resources, "Color.ControlActiveBackground", "Brush.ControlActiveBackground", colors.ControlActiveBackground);
        SetThemeColor(resources, "Color.InputBackground", "Brush.InputBackground", colors.InputBackground);
        SetThemeColor(resources, "Color.InputHoverBackground", "Brush.InputHoverBackground", colors.InputHoverBackground);
        SetThemeColor(resources, "Color.TextPrimary", "Brush.TextPrimary", colors.TextPrimary);
        SetThemeColor(resources, "Color.TextSecondary", "Brush.TextSecondary", colors.TextSecondary);
        SetThemeColor(resources, "Color.TextMuted", "Brush.TextMuted", colors.TextMuted);
    }

    private static void SetThemeColor(ResourceDictionary resources, string colorKey, string brushKey, Color color)
    {
        resources[colorKey] = color;

        if (resources[brushKey] is SolidColorBrush brush && !brush.IsFrozen)
        {
            brush.Color = color;
            return;
        }

        resources[brushKey] = new SolidColorBrush(color);
    }
}
