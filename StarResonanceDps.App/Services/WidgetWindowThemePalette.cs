using System.Windows.Media;

namespace StarResonanceDps.App.Services;

/// <summary>
/// Per-widget rendering colors for the floating widget window.
/// The window surface, divider, and close icon use the configured window opacity;
/// text stays opaque so it remains readable over a transparent widget surface.
/// </summary>
public sealed class WidgetWindowThemePalette
{
    private const double DividerDarkenWeight = 0.45;
    private const double TextSurfaceWeight = 0.075;

    private WidgetWindowThemePalette(
        Color surface,
        Color border,
        Color text,
        Color nonText)
    {
        WidgetWindowSurface = surface;
        WidgetWindowBorder = border;
        WidgetWindowText = text;
        WidgetWindowNonText = nonText;

        WidgetWindowSurfaceBrush = CreateBrush(WidgetWindowSurface);
        WidgetWindowBorderBrush = CreateBrush(WidgetWindowBorder);
        WidgetWindowTextBrush = CreateBrush(WidgetWindowText);
        WidgetWindowNonTextBrush = CreateBrush(WidgetWindowNonText);
    }

    public Color WidgetWindowSurface { get; }
    public Color WidgetWindowBorder { get; }
    public Color WidgetWindowText { get; }
    public Color WidgetWindowNonText { get; }

    public SolidColorBrush WidgetWindowSurfaceBrush { get; }
    public SolidColorBrush WidgetWindowBorderBrush { get; }
    public SolidColorBrush WidgetWindowTextBrush { get; }
    public SolidColorBrush WidgetWindowNonTextBrush { get; }

    public static WidgetWindowThemePalette Create(Color windowSurface, int opacityPercent)
    {
        var opacity = Math.Clamp(opacityPercent, 0, 100);
        var readableBase = ColorUtilities.GetReadableTextColor(windowSurface);
        var text = ColorUtilities.Blend(readableBase, windowSurface, TextSurfaceWeight);
        var divider = ColorUtilities.Blend(windowSurface, Colors.Black, DividerDarkenWeight);

        return new WidgetWindowThemePalette(
            WithOpacity(windowSurface, opacity),
            WithOpacity(divider, opacity),
            text,
            WithOpacity(text, opacity));
    }

    private static Color WithOpacity(Color color, int opacityPercent)
    {
        var alpha = (byte)Math.Clamp(
            (int)Math.Round(byte.MaxValue * (opacityPercent / 100d), MidpointRounding.AwayFromZero),
            byte.MinValue,
            byte.MaxValue);

        return Color.FromArgb(alpha, color.R, color.G, color.B);
    }

    private static SolidColorBrush CreateBrush(Color color)
    {
        var brush = new SolidColorBrush(color);
        brush.Freeze();
        return brush;
    }
}
