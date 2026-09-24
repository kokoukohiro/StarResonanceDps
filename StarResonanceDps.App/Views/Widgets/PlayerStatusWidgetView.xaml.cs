using System;
using System.Windows;
using System.Windows.Controls;

namespace StarResonanceDps.App.Views.Widgets;

/// <summary>
/// ステータス詳細。自分の実体に届いた属性を番号順に1列で並べる。
///
/// <para>
/// 縦スクロールは<b>ウィンドウ枠側の細いスクロールバー</b>に繋ぐ(バフ一覧と同じ)。
/// 内側に WPF のスクロールバーを出すと他のウィジェットと見た目が揃わない。
/// </para>
/// </summary>
public partial class PlayerStatusWidgetView : UserControl, IWidgetVerticalScrollContent
{
    public PlayerStatusWidgetView()
    {
        InitializeComponent();
    }

    public event EventHandler? VerticalScrollMetricsChanged;

    public WidgetVerticalScrollMetrics GetVerticalScrollMetrics()
    {
        var maximum = Math.Max(StatusListScrollViewer.ScrollableHeight, 0d);
        var viewport = Math.Max(StatusListScrollViewer.ViewportHeight, 0d);

        return new WidgetVerticalScrollMetrics(
            maximum,
            viewport,
            Math.Min(StatusListScrollViewer.VerticalOffset, maximum),
            Math.Max(viewport * 0.9d, 1d),
            // 行1つぶん。行の高さ(31)と合わせる。
            31d);
    }

    public void SetVerticalScrollOffset(double verticalOffset)
    {
        var maximum = Math.Max(StatusListScrollViewer.ScrollableHeight, 0d);
        var offset = double.IsFinite(verticalOffset)
            ? Math.Clamp(verticalOffset, 0d, maximum)
            : 0d;

        StatusListScrollViewer.ScrollToVerticalOffset(offset);
    }

    private void PlayerStatusWidgetView_Loaded(object sender, RoutedEventArgs e)
    {
        NotifyVerticalScrollMetricsChanged();
    }

    private void PlayerStatusWidgetView_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        NotifyVerticalScrollMetricsChanged();
    }

    private void PlayerStatusWidgetView_DataContextChanged(
        object sender,
        DependencyPropertyChangedEventArgs e)
    {
        NotifyVerticalScrollMetricsChanged();
    }

    private void StatusListScrollViewer_ScrollChanged(object sender, ScrollChangedEventArgs e)
    {
        NotifyVerticalScrollMetricsChanged();
    }

    private void NotifyVerticalScrollMetricsChanged()
    {
        VerticalScrollMetricsChanged?.Invoke(this, EventArgs.Empty);
    }
}
