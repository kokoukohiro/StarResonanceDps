using System.Windows.Media;
using StarResonanceDps.App.Config;

namespace StarResonanceDps.App.Services;

/// <summary>
/// 属性カラー(<see cref="ElementColorWidgetSettingsConfig"/>)から行のバーの色を作る。スキル詳細と被ダメリストが使う。
/// フィルターと不透明度が掛かるのはバーだけで、設定画面の色見本は素のまま。
/// </summary>
public static class ElementBarColors
{
    /// <summary>その属性の色見本で選んでいる色。</summary>
    public static Color Resolve(ElementColorWidgetSettingsConfig settings, Zproto.EDamageProperty element)
    {
        var key = element.ToString();
        var defaults = WidgetConfigDefaults.CreateDefaultElementColors(key);
        var palette = settings.ColorPalettes.TryGetValue(key, out var colors) && colors.Count > 0
            ? colors
            : defaults;
        var selectedIndex = settings.ColorIndexes.TryGetValue(key, out var index)
            ? index
            : WidgetConfigDefaults.MinClassColorIndex + 1;

        var hex = palette[Math.Clamp(selectedIndex, 0, palette.Count - 1)];
        return ColorUtilities.TryParseHex(hex, out var color)
            ? color
            : Colors.Gray;
    }

    /// <summary>バーの塗り。色にフィルターと不透明度を掛ける。</summary>
    public static SolidColorBrush CreateBarBrush(ElementColorWidgetSettingsConfig settings, Color color)
    {
        var filtered = ClassColorFilter.Apply(color, settings);
        var opacity = Math.Clamp(
            settings.ColorOpacity,
            WidgetConfigDefaults.MinClassColorOpacity,
            WidgetConfigDefaults.MaxClassColorOpacity);

        var brush = new SolidColorBrush(Color.FromArgb(
            (byte)Math.Round(opacity / 100d * byte.MaxValue, MidpointRounding.AwayFromZero),
            filtered.R,
            filtered.G,
            filtered.B));
        brush.Freeze();
        return brush;
    }
}
