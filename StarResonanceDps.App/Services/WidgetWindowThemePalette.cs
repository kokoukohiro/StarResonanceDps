using System.Windows.Media;

namespace StarResonanceDps.App.Services;

/// <summary>
/// Per-widget rendering colors for the floating widget window.
/// Surface-based elements are rendered with opaque colors as one layer, then the configured
/// window opacity is applied to that layer. The close icon stays opaque.
/// </summary>
public sealed class WidgetWindowThemePalette
{
    // Blend(from, to, 0.75) results in 25% text and 75% surface.
    private const double DividerSurfaceWeight = 0.75;
    private const double CloseSurfaceWeight = 0.50;
    private const double ScrollThumbSurfaceWeight = 0.625;
    private const double NonTextContrastSurfaceWeight = 0.075;

    private WidgetWindowThemePalette(
        Color surface,
        Color menuSurface,
        Color divider,
        Color close,
        Color scrollThumb,
        double nonTextOpacity)
    {
        WidgetWindowSurface = surface;
        WidgetWindowMenuSurface = menuSurface;
        WidgetWindowDivider = divider;
        WidgetWindowClose = close;
        WidgetWindowScrollThumb = scrollThumb;
        NonTextOpacity = nonTextOpacity;

        WidgetWindowSurfaceBrush = CreateBrush(WidgetWindowSurface);
        WidgetWindowMenuSurfaceBrush = CreateBrush(WidgetWindowMenuSurface);
        WidgetWindowDividerBrush = CreateBrush(WidgetWindowDivider);
        WidgetWindowCloseBrush = CreateBrush(WidgetWindowClose);
        WidgetWindowScrollThumbBrush = CreateBrush(WidgetWindowScrollThumb);
    }

    public Color WidgetWindowSurface { get; }
    public Color WidgetWindowMenuSurface { get; }
    public Color WidgetWindowDivider { get; }
    public Color WidgetWindowClose { get; }
    public Color WidgetWindowScrollThumb { get; }

    /// <summary>
    /// Opacity shared by the widget window's background, menus, divider, scroll thumb, and future non-text body UI.
    /// The close icon intentionally does not use this value.
    /// </summary>
    public double NonTextOpacity { get; }

    public SolidColorBrush WidgetWindowSurfaceBrush { get; }
    public SolidColorBrush WidgetWindowMenuSurfaceBrush { get; }
    public SolidColorBrush WidgetWindowDividerBrush { get; }
    public SolidColorBrush WidgetWindowCloseBrush { get; }
    public SolidColorBrush WidgetWindowScrollThumbBrush { get; }

    public static WidgetWindowThemePalette Create(
        Color windowSurface,
        int opacityPercent,
        Color? backgroundImageAverageColor = null)
    {
        var surface = Color.FromRgb(windowSurface.R, windowSurface.G, windowSurface.B);
        var menuSurface = backgroundImageAverageColor is { } imageColor
            ? Color.FromRgb(imageColor.R, imageColor.G, imageColor.B)
            : surface;
        var opacity = Math.Clamp(opacityPercent, 0, 100) / 100d;
        var readableBase = ColorUtilities.GetReadableTextColor(menuSurface);
        var nonTextContrast = ColorUtilities.Blend(
            readableBase,
            menuSurface,
            NonTextContrastSurfaceWeight);
        var divider = ColorUtilities.Blend(nonTextContrast, menuSurface, DividerSurfaceWeight);
        var close = ColorUtilities.Blend(nonTextContrast, menuSurface, CloseSurfaceWeight);
        var scrollThumb = ColorUtilities.Blend(nonTextContrast, menuSurface, ScrollThumbSurfaceWeight);

        return new WidgetWindowThemePalette(
            surface,
            menuSurface,
            divider,
            close,
            scrollThumb,
            opacity);
    }

    private static SolidColorBrush CreateBrush(Color color)
    {
        var brush = new SolidColorBrush(color);
        brush.Freeze();
        return brush;
    }
}
