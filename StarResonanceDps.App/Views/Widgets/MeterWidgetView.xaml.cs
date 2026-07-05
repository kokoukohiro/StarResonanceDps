using System;
using System.Windows;
using System.Windows.Controls;

namespace StarResonanceDps.App.Views.Widgets;

public partial class MeterWidgetView : UserControl, IWidgetVerticalScrollContent
{
    public event EventHandler? VerticalScrollMetricsChanged;

    public MeterWidgetView()
    {
        InitializeComponent();
        Loaded += MeterWidgetView_Loaded;
        SizeChanged += MeterWidgetView_SizeChanged;
    }

    public WidgetVerticalScrollMetrics GetVerticalScrollMetrics()
    {
        var maximum = Math.Max(MeterScrollViewer.ScrollableHeight, 0);
        var viewport = Math.Max(MeterScrollViewer.ViewportHeight, 0);

        return new WidgetVerticalScrollMetrics(
            maximum,
            viewport,
            Math.Min(MeterScrollViewer.VerticalOffset, maximum),
            Math.Max(viewport * 0.9, 1),
            31);
    }

    public void SetVerticalScrollOffset(double verticalOffset)
    {
        var maximum = Math.Max(MeterScrollViewer.ScrollableHeight, 0);
        var offset = double.IsFinite(verticalOffset)
            ? Math.Clamp(verticalOffset, 0, maximum)
            : 0;

        MeterScrollViewer.ScrollToVerticalOffset(offset);
    }

    private void MeterWidgetView_Loaded(object sender, RoutedEventArgs e)
    {
        NotifyVerticalScrollMetricsChanged();
    }

    private void MeterWidgetView_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        NotifyVerticalScrollMetricsChanged();
    }

    private void MeterScrollViewer_ScrollChanged(object sender, ScrollChangedEventArgs e)
    {
        NotifyVerticalScrollMetricsChanged();
    }

    private void NotifyVerticalScrollMetricsChanged()
    {
        VerticalScrollMetricsChanged?.Invoke(this, EventArgs.Empty);
    }
}
