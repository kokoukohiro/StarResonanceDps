using System;
using System.Windows;
using System.Windows.Controls;
using StarResonanceDps.App.Controls;

namespace StarResonanceDps.App.Views.Widgets;

/// <summary>
/// 推移グラフ。横のスクロールバーは窓の枠の層の1本(<see cref="IWidgetHorizontalScrollContent"/>)、
/// 格子と軸の線は枠の層(<see cref="WidgetWindow.FrameOverlayHost"/>)へ差し込む。どちらも分割線と同じく窓の不透明度で合成される。
/// </summary>
public partial class PlayerMetricTimelineWidgetView : UserControl, IWidgetHorizontalScrollContent
{
    private MetricTimelineGridLayer? _gridLayer;
    private WidgetWindow? _frameOverlayOwner;

    public PlayerMetricTimelineWidgetView()
    {
        InitializeComponent();
        TimelineChart.ScrollMetricsChanged += TimelineChart_ScrollMetricsChanged;
        Loaded += PlayerMetricTimelineWidgetView_Loaded;
        Unloaded += PlayerMetricTimelineWidgetView_Unloaded;
    }

    public event EventHandler? HorizontalScrollMetricsChanged;

    public WidgetHorizontalScrollMetrics GetHorizontalScrollMetrics()
    {
        return new WidgetHorizontalScrollMetrics(
            TimelineChart.ScrollableSeconds,
            TimelineChart.VisibleSeconds,
            TimelineChart.HorizontalOffset,
            TimelineChart.VisibleSeconds,
            TimelineChart.GridStepSeconds);
    }

    public void SetHorizontalScrollOffset(double horizontalOffset)
    {
        TimelineChart.HorizontalOffset = horizontalOffset;
    }

    private void TimelineChart_ScrollMetricsChanged(object? sender, EventArgs e)
    {
        HorizontalScrollMetricsChanged?.Invoke(this, EventArgs.Empty);
    }

    private void PlayerMetricTimelineWidgetView_Loaded(object sender, RoutedEventArgs e)
    {
        AttachGridLayer();
    }

    private void PlayerMetricTimelineWidgetView_Unloaded(object sender, RoutedEventArgs e)
    {
        DetachGridLayer();
    }

    private void AttachGridLayer()
    {
        if (_gridLayer is not null)
        {
            return;
        }

        if (Window.GetWindow(this) is not WidgetWindow owner
            || TryFindResource("GridLayer") is not MetricTimelineGridLayer gridLayer)
        {
            return;
        }

        _gridLayer = gridLayer;
        _frameOverlayOwner = owner;
        gridLayer.Chart = TimelineChart;
        TimelineChart.GridLayer = gridLayer;
        owner.FrameOverlayHost.Content = gridLayer;
    }

    private void DetachGridLayer()
    {
        if (_frameOverlayOwner is not null
            && ReferenceEquals(_frameOverlayOwner.FrameOverlayHost.Content, _gridLayer))
        {
            _frameOverlayOwner.FrameOverlayHost.Content = null;
        }

        if (_gridLayer is not null)
        {
            _gridLayer.Chart = null;
        }

        TimelineChart.GridLayer = null;
        _gridLayer = null;
        _frameOverlayOwner = null;
    }
}
