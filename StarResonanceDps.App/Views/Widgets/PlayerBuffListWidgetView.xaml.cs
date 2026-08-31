using System;
using System.Windows;
using System.Windows.Controls;

namespace StarResonanceDps.App.Views.Widgets;

/// <summary>
/// バフ/デバフの一覧。プレイヤー用とエンティティ用の2つのViewModelが同じこのビューを使う。
///
/// <para>
/// 縦スクロールは<b>ウィンドウ枠側の細いスクロールバー</b>に繋ぐ。内側にWPFのスクロールバーを
/// 出すと他のウィジェットと見た目が揃わず、コンテンツ幅も削られる。
/// </para>
/// </summary>
public partial class PlayerBuffListWidgetView : UserControl, IWidgetVerticalScrollContent
{
    public PlayerBuffListWidgetView()
    {
        InitializeComponent();
    }

    public event EventHandler? VerticalScrollMetricsChanged;

    public WidgetVerticalScrollMetrics GetVerticalScrollMetrics()
    {
        var maximum = Math.Max(BuffListScrollViewer.ScrollableHeight, 0);
        var viewport = Math.Max(BuffListScrollViewer.ViewportHeight, 0);

        return new WidgetVerticalScrollMetrics(
            maximum,
            viewport,
            Math.Min(BuffListScrollViewer.VerticalOffset, maximum),
            Math.Max(viewport * 0.9, 1),
            34);
    }

    public void SetVerticalScrollOffset(double verticalOffset)
    {
        var maximum = Math.Max(BuffListScrollViewer.ScrollableHeight, 0);
        var offset = double.IsFinite(verticalOffset)
            ? Math.Clamp(verticalOffset, 0, maximum)
            : 0;

        BuffListScrollViewer.ScrollToVerticalOffset(offset);
    }

    private void PlayerBuffListWidgetView_Loaded(object sender, RoutedEventArgs e)
    {
        NotifyVerticalScrollMetricsChanged();
    }

    private void PlayerBuffListWidgetView_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        NotifyVerticalScrollMetricsChanged();
    }

    private void PlayerBuffListWidgetView_DataContextChanged(
        object sender,
        DependencyPropertyChangedEventArgs e)
    {
        NotifyVerticalScrollMetricsChanged();
    }

    private void BuffListScrollViewer_ScrollChanged(object sender, ScrollChangedEventArgs e)
    {
        NotifyVerticalScrollMetricsChanged();
    }

    private void NotifyVerticalScrollMetricsChanged()
    {
        VerticalScrollMetricsChanged?.Invoke(this, EventArgs.Empty);
    }
}
