using System;
using System.Windows;
using System.Windows.Controls;

namespace StarResonanceDps.App.Views.Widgets;

public partial class EntityListWidgetView : UserControl, IWidgetVerticalScrollContent
{
    public event EventHandler? VerticalScrollMetricsChanged;

    public EntityListWidgetView()
    {
        InitializeComponent();
        Loaded += EntityListWidgetView_Loaded;
        SizeChanged += EntityListWidgetView_SizeChanged;
    }

    public WidgetVerticalScrollMetrics GetVerticalScrollMetrics()
    {
        var maximum = Math.Max(EntityListScrollViewer.ScrollableHeight, 0);
        var viewport = Math.Max(EntityListScrollViewer.ViewportHeight, 0);

        return new WidgetVerticalScrollMetrics(
            maximum,
            viewport,
            Math.Min(EntityListScrollViewer.VerticalOffset, maximum),
            Math.Max(viewport * 0.9, 1),
            50);
    }

    public void SetVerticalScrollOffset(double verticalOffset)
    {
        var maximum = Math.Max(EntityListScrollViewer.ScrollableHeight, 0);
        var offset = double.IsFinite(verticalOffset)
            ? Math.Clamp(verticalOffset, 0, maximum)
            : 0;

        EntityListScrollViewer.ScrollToVerticalOffset(offset);
    }

    private void EntityListWidgetView_Loaded(object sender, RoutedEventArgs e)
    {
        NotifyVerticalScrollMetricsChanged();
    }

    private void EntityListWidgetView_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        NotifyVerticalScrollMetricsChanged();
    }

    private void EntityListScrollViewer_ScrollChanged(object sender, ScrollChangedEventArgs e)
    {
        NotifyVerticalScrollMetricsChanged();
    }

    private void NotifyVerticalScrollMetricsChanged()
    {
        VerticalScrollMetricsChanged?.Invoke(this, EventArgs.Empty);
    }
}
