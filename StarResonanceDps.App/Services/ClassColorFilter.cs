using System.Windows.Media;
using StarResonanceDps.App.Config;

namespace StarResonanceDps.App.Services;

/// <summary>
/// クラスカラーに掛けるレンズフィルター。
/// 素のクラスカラーとフィルター色を「強さ」の比率で混ぜるだけ。
/// </summary>
public static class ClassColorFilter
{
    public static Color Apply(Color color, MeterWidgetSettingsConfig settings)
    {
        if (settings.ClassColorFilterEnabled != true)
        {
            return color;
        }

        var palette = settings.ClassColorFilterColors;
        if (palette is null || palette.Count == 0)
        {
            return color;
        }

        var index = Math.Clamp(settings.ClassColorFilterColorIndex, 0, palette.Count - 1);
        if (!ColorUtilities.TryParseHex(palette[index], out var filterColor))
        {
            return color;
        }

        return Apply(color, filterColor, settings.ClassColorFilterStrength);
    }

    /// <summary>スキル詳細の属性カラー用。掛け方はクラスカラーとまったく同じ。</summary>
    public static Color Apply(Color color, ElementColorWidgetSettingsConfig settings)
    {
        if (settings.FilterEnabled != true)
        {
            return color;
        }

        var palette = settings.FilterColors;
        if (palette is null || palette.Count == 0)
        {
            return color;
        }

        var index = Math.Clamp(settings.FilterColorIndex, 0, palette.Count - 1);
        if (!ColorUtilities.TryParseHex(palette[index], out var filterColor))
        {
            return color;
        }

        return Apply(color, filterColor, settings.FilterStrength);
    }

    public static Color Apply(Color color, Color filterColor, double strength)
    {
        var weight = Math.Clamp(
            strength,
            WidgetConfigDefaults.MinClassColorFilterStrength,
            WidgetConfigDefaults.MaxClassColorFilterStrength) / 100d;

        return ColorUtilities.Blend(color, filterColor, weight);
    }
}
