using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace StarResonanceDps.App.Views.Widgets;

/// <summary>
/// 被ダメログ。縦スクロールはウィンドウ枠側の細いスクロールバーに繋ぐ(他のウィジェットと同じ)。
///
/// <para>
/// <b>末尾に居るときだけ追記に追従する。</b> 途中を読んでいる間に行が増えても位置は動かさない。
/// 追従するかは、利用者のスクロール(内容の高さが変わっていない回)で決める。
/// </para>
/// </summary>
public partial class TakenDamageLogWidgetView : UserControl, IWidgetVerticalScrollContent
{
    private ScrollViewer? _scrollViewer;
    private bool _followsTail = true;

    public TakenDamageLogWidgetView()
    {
        InitializeComponent();
        PreviewMouseWheel += TakenDamageLogWidgetView_PreviewMouseWheel;
    }

    public event EventHandler? VerticalScrollMetricsChanged;

    public WidgetVerticalScrollMetrics GetVerticalScrollMetrics()
    {
        if (_scrollViewer is null)
        {
            return new WidgetVerticalScrollMetrics(0, 0, 0, 1, 1);
        }

        var maximum = Math.Max(_scrollViewer.ScrollableHeight, 0);
        var viewport = Math.Max(_scrollViewer.ViewportHeight, 0);

        return new WidgetVerticalScrollMetrics(
            maximum,
            viewport,
            Math.Min(_scrollViewer.VerticalOffset, maximum),
            Math.Max(viewport * 0.9, 1),
            18);
    }

    public void SetVerticalScrollOffset(double verticalOffset)
    {
        if (_scrollViewer is null)
        {
            return;
        }

        var maximum = Math.Max(_scrollViewer.ScrollableHeight, 0);
        var offset = double.IsFinite(verticalOffset)
            ? Math.Clamp(verticalOffset, 0, maximum)
            : 0;

        StarResonanceDps.App.Diagnostics.HistorySwitchProbe.ScrollInputReceived("TakenDamageLog");
        _scrollViewer.ScrollToVerticalOffset(offset);
    }

    private void TakenDamageLogWidgetView_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
    {
        StarResonanceDps.App.Diagnostics.HistorySwitchProbe.ScrollInputReceived("TakenDamageLog");
    }

    private void TakenDamageLogWidgetView_Loaded(object sender, RoutedEventArgs e)
    {
        if (_scrollViewer is null)
        {
            LogItemsControl.ApplyTemplate();
            _scrollViewer = (ScrollViewer)LogItemsControl.Template.FindName("LogScrollViewer", LogItemsControl);
            _scrollViewer.ScrollChanged += LogScrollViewer_ScrollChanged;
        }

        NotifyVerticalScrollMetricsChanged();
    }

    private void TakenDamageLogWidgetView_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        NotifyVerticalScrollMetricsChanged();
    }

    private void LogScrollViewer_ScrollChanged(object sender, ScrollChangedEventArgs e)
    {
        var scrollViewer = (ScrollViewer)sender;

        if (e.ExtentHeightChange == 0)
        {
            _followsTail = scrollViewer.VerticalOffset >= scrollViewer.ScrollableHeight - 1;
        }
        else if (_followsTail)
        {
            scrollViewer.ScrollToEnd();
        }

        if (e.VerticalChange != 0)
        {
            StarResonanceDps.App.Diagnostics.HistorySwitchProbe.ScrollPositionChanged();
        }

        NotifyVerticalScrollMetricsChanged();
    }

    private void NotifyVerticalScrollMetricsChanged()
    {
        VerticalScrollMetricsChanged?.Invoke(this, EventArgs.Empty);
    }
}
