using System.Windows;
using System.Windows.Media;
using StarResonanceDps.App.Config;

namespace StarResonanceDps.App.Services;

/// <summary>
/// バフ・デバフ一覧の行のゲージの塗り。プレイヤーから開いた一覧とエンティティリストから開いた一覧の両方が使う。
/// </summary>
public static class BuffListGaugeBrush
{
    /// <summary>
    /// 設定の2色で左から右へのグラデーションにする(停止位置はエンティティリストの HP バーと同じ)。
    /// </summary>
    public static LinearGradientBrush Create(BuffListWidgetSettingsConfig settings)
    {
        var brush = new LinearGradientBrush
        {
            StartPoint = new Point(0, 0.5),
            EndPoint = new Point(1, 0.5)
        };

        var start = ResolveColor(settings, "GaugeStart");
        var end = ResolveColor(settings, "GaugeEnd");
        brush.GradientStops.Add(new GradientStop(start, 0d));
        brush.GradientStops.Add(new GradientStop(start, 0.05d));
        brush.GradientStops.Add(new GradientStop(end, 0.95d));
        brush.GradientStops.Add(new GradientStop(end, 1d));
        brush.Freeze();
        return brush;
    }

    /// <summary>ゲージが満タンになる残り時間(秒)。</summary>
    public static int GetLengthSeconds(BuffListWidgetSettingsConfig settings)
    {
        return WidgetConfigDefaults.BuffListGaugeLengthSeconds[settings.GaugeLengthIndex];
    }

    private static Color ResolveColor(BuffListWidgetSettingsConfig settings, string key)
    {
        var palette = settings.GaugeColorPalettes.TryGetValue(key, out var colors)
            ? colors
            : WidgetConfigDefaults.CreateDefaultGaugeColors(key);
        var selectedIndex = settings.GaugeColorIndexes.TryGetValue(key, out var index)
            ? index
            : WidgetConfigDefaults.MinClassColorIndex;
        var selectedColor = palette.Count == 0
            ? WidgetConfigDefaults.CreateDefaultGaugeColors(key)[0]
            : palette[Math.Clamp(selectedIndex, 0, palette.Count - 1)];

        var color = ColorUtilities.TryParseHex(selectedColor, out var parsed)
            ? parsed
            : Colors.Gray;

        // 不透明度はメーターのクラスカラーと同じ作りで、表示に使う色のアルファへ掛ける。
        // 設定画面の色見本は素のままにしたいので、ここだけで掛ける。
        var opacity = Math.Clamp(
            settings.GaugeColorOpacity,
            WidgetConfigDefaults.MinClassColorOpacity,
            WidgetConfigDefaults.MaxClassColorOpacity);

        return Color.FromArgb(
            (byte)Math.Round(opacity / 100d * byte.MaxValue, MidpointRounding.AwayFromZero),
            color.R,
            color.G,
            color.B);
    }
}
