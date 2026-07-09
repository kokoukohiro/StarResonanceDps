using System;
using System.Windows;
using System.Windows.Controls;

namespace StarResonanceDps.App.Views.Widgets;

public partial class PlayerMetricContributionWidgetView : UserControl, IWidgetVerticalScrollContent
{
    public PlayerMetricContributionWidgetView()
    {
        InitializeComponent();
    }

    public event EventHandler? VerticalScrollMetricsChanged;

    public WidgetVerticalScrollMetrics GetVerticalScrollMetrics()
    {
        var maximum = Math.Max(ContributionScrollViewer.ScrollableHeight, 0d);
        var viewport = Math.Max(ContributionScrollViewer.ViewportHeight, 0d);

        return new WidgetVerticalScrollMetrics(
            maximum,
            viewport,
            Math.Min(ContributionScrollViewer.VerticalOffset, maximum),
            Math.Max(viewport * 0.9d, 1d),
            32d);
    }

    public void SetVerticalScrollOffset(double verticalOffset)
    {
        var maximum = Math.Max(ContributionScrollViewer.ScrollableHeight, 0d);
        var offset = double.IsFinite(verticalOffset)
            ? Math.Clamp(verticalOffset, 0d, maximum)
            : 0d;

        ContributionScrollViewer.ScrollToVerticalOffset(offset);
    }

    private void PlayerMetricContributionWidgetView_Loaded(object sender, RoutedEventArgs e)
    {
        NotifyVerticalScrollMetricsChanged();
    }

    private void PlayerMetricContributionWidgetView_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        NotifyVerticalScrollMetricsChanged();
    }

    private void ContributionScrollViewer_ScrollChanged(object sender, ScrollChangedEventArgs e)
    {
        NotifyVerticalScrollMetricsChanged();
    }

    private void NotifyVerticalScrollMetricsChanged()
    {
        VerticalScrollMetricsChanged?.Invoke(this, EventArgs.Empty);
    }
}
