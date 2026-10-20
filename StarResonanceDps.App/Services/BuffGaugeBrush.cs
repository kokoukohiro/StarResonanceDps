using System.Windows;
using System.Windows.Media;
using StarResonanceDps.App.Config;

namespace StarResonanceDps.App.Services;

/// <summary>
/// バフ・デバフの行のゲージの塗り。プレイヤーから開いた一覧・エンティティリストから開いた一覧・バフ・デバフグループが使う。
/// </summary>
public static class BuffGaugeBrush
{
    /// <summary>
    /// 設定の2色で左から右へのグラデーションにする(停止位置はエンティティリストの HP バーと同じ)。
    /// </summary>
    public static LinearGradientBrush Create(BuffGaugeColorConfig colors)
    {
        var brush = new LinearGradientBrush
        {
            StartPoint = new Point(0, 0.5),
            EndPoint = new Point(1, 0.5)
        };

        var start = ResolveColor(colors, "GaugeStart");
        var end = ResolveColor(colors, "GaugeEnd");
        brush.GradientStops.Add(new GradientStop(start, 0d));
        brush.GradientStops.Add(new GradientStop(start, 0.05d));
        brush.GradientStops.Add(new GradientStop(end, 0.95d));
        brush.GradientStops.Add(new GradientStop(end, 1d));
        brush.Freeze();
        return brush;
    }

    /// <summary>ゲージが満タンになる残り時間(秒)。自動なら null(行ごとの長さを使う)。</summary>
    public static int? GetLengthSeconds(int gaugeLengthIndex)
    {
        return WidgetConfigDefaults.BuffGaugeLengthSeconds[gaugeLengthIndex];
    }

    private static Color ResolveColor(BuffGaugeColorConfig colors, string key)
    {
        var palette = colors.Palettes.TryGetValue(key, out var saved)
            ? saved
            : WidgetConfigDefaults.CreateDefaultGaugeColors(key);
        var selectedIndex = colors.Indexes.TryGetValue(key, out var index)
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
            colors.Opacity,
            WidgetConfigDefaults.MinClassColorOpacity,
            WidgetConfigDefaults.MaxClassColorOpacity);

        return Color.FromArgb(
            (byte)Math.Round(opacity / 100d * byte.MaxValue, MidpointRounding.AwayFromZero),
            color.R,
            color.G,
            color.B);
    }
}
