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

        SetThemeBrush(resources, "Brush.WindowSurface", colors.WindowSurface);
        SetThemeBrush(resources, "Brush.PanelBackground", colors.PanelBackground);
        SetThemeBrush(resources, "Brush.Border", colors.Border);
        SetThemeBrush(resources, "Brush.BorderSoft", colors.BorderSoft);
        SetThemeBrush(resources, "Brush.ControlBackground", colors.ControlBackground);
        SetThemeBrush(resources, "Brush.ControlActiveBackground", colors.ControlActiveBackground);
        SetThemeBrush(resources, "Brush.InputBackground", colors.InputBackground);
        SetThemeBrush(resources, "Brush.InputHoverBackground", colors.InputHoverBackground);
        SetThemeBrush(resources, "Brush.TextPrimary", colors.TextPrimary);
        SetThemeBrush(resources, "Brush.TextSecondary", colors.TextSecondary);
        SetThemeBrush(resources, "Brush.TextMuted", colors.TextMuted);
    }

    private static void SetThemeBrush(ResourceDictionary resources, string brushKey, Color color)
    {
        if (resources[brushKey] is SolidColorBrush brush && !brush.IsFrozen)
        {
            brush.Color = color;
            return;
        }

        resources[brushKey] = new SolidColorBrush(color);
    }
}
