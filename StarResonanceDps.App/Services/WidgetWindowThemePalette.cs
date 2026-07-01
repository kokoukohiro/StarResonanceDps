using System.Windows.Media;

namespace StarResonanceDps.App.Services;

/// <summary>
/// Per-widget rendering colors for the floating widget window.
/// Non-text elements are rendered with opaque colors as one layer, then the configured
/// window opacity is applied to that layer. Header text and the close icon stay opaque.
/// </summary>
public sealed class WidgetWindowThemePalette
{
    // Blend(from, to, 0.75) results in 25% text and 75% surface.
    private const double DividerSurfaceWeight = 0.75;
    private const double CloseSurfaceWeight = 0.50;
    private const double ScrollThumbSurfaceWeight = 0.625;
    private const double TextSurfaceWeight = 0.075;

    private WidgetWindowThemePalette(
        Color surface,
        Color divider,
        Color text,
        Color close,
        Color scrollThumb,
        double nonTextOpacity)
    {
        WidgetWindowSurface = surface;
        WidgetWindowDivider = divider;
        WidgetWindowText = text;
        WidgetWindowClose = close;
        WidgetWindowScrollThumb = scrollThumb;
        NonTextOpacity = nonTextOpacity;

        WidgetWindowSurfaceBrush = CreateBrush(WidgetWindowSurface);
        WidgetWindowDividerBrush = CreateBrush(WidgetWindowDivider);
        WidgetWindowTextBrush = CreateBrush(WidgetWindowText);
        WidgetWindowCloseBrush = CreateBrush(WidgetWindowClose);
        WidgetWindowScrollThumbBrush = CreateBrush(WidgetWindowScrollThumb);
    }

    public Color WidgetWindowSurface { get; }
    public Color WidgetWindowDivider { get; }
    public Color WidgetWindowText { get; }
    public Color WidgetWindowClose { get; }
    public Color WidgetWindowScrollThumb { get; }

    /// <summary>
    /// Opacity shared by the widget window's background, divider, scroll thumb, and future non-text body UI.
    /// Text and the close icon intentionally do not use this value.
    /// </summary>
    public double NonTextOpacity { get; }

    public SolidColorBrush WidgetWindowSurfaceBrush { get; }
    public SolidColorBrush WidgetWindowDividerBrush { get; }
    public SolidColorBrush WidgetWindowTextBrush { get; }
    public SolidColorBrush WidgetWindowCloseBrush { get; }
    public SolidColorBrush WidgetWindowScrollThumbBrush { get; }

    public static WidgetWindowThemePalette Create(
        Color windowSurface,
        int opacityPercent,
        Color? backgroundImageAverageColor = null)
    {
        var surface = Color.FromRgb(windowSurface.R, windowSurface.G, windowSurface.B);
        var contrastSource = backgroundImageAverageColor is { } imageColor
            ? Color.FromRgb(imageColor.R, imageColor.G, imageColor.B)
            : surface;
        var opacity = Math.Clamp(opacityPercent, 0, 100) / 100d;
        var readableBase = ColorUtilities.GetReadableTextColor(contrastSource);
        var text = ColorUtilities.Blend(readableBase, contrastSource, TextSurfaceWeight);
        var divider = ColorUtilities.Blend(text, contrastSource, DividerSurfaceWeight);
        var close = ColorUtilities.Blend(text, contrastSource, CloseSurfaceWeight);
        var scrollThumb = ColorUtilities.Blend(text, contrastSource, ScrollThumbSurfaceWeight);

        return new WidgetWindowThemePalette(surface, divider, text, close, scrollThumb, opacity);
    }

    private static SolidColorBrush CreateBrush(Color color)
    {
        var brush = new SolidColorBrush(color);
        brush.Freeze();
        return brush;
    }
}
