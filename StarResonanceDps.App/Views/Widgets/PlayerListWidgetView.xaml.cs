using System;
using System.Windows;
using System.Windows.Controls;

namespace StarResonanceDps.App.Views.Widgets;

public partial class PlayerListWidgetView : UserControl, IWidgetVerticalScrollContent
{
    public event EventHandler? VerticalScrollMetricsChanged;

    public PlayerListWidgetView()
    {
        InitializeComponent();
        Loaded += PlayerListWidgetView_Loaded;
        SizeChanged += PlayerListWidgetView_SizeChanged;
    }

    public WidgetVerticalScrollMetrics GetVerticalScrollMetrics()
    {
        var maximum = Math.Max(PlayerListScrollViewer.ScrollableHeight, 0);
        var viewport = Math.Max(PlayerListScrollViewer.ViewportHeight, 0);

        return new WidgetVerticalScrollMetrics(
            maximum,
            viewport,
            Math.Min(PlayerListScrollViewer.VerticalOffset, maximum),
            Math.Max(viewport * 0.9, 1),
            42);
    }

    public void SetVerticalScrollOffset(double verticalOffset)
    {
        var maximum = Math.Max(PlayerListScrollViewer.ScrollableHeight, 0);
        var offset = double.IsFinite(verticalOffset)
            ? Math.Clamp(verticalOffset, 0, maximum)
            : 0;

        PlayerListScrollViewer.ScrollToVerticalOffset(offset);
    }

    private void PlayerListWidgetView_Loaded(object sender, RoutedEventArgs e)
    {
        NotifyVerticalScrollMetricsChanged();
    }

    private void PlayerListWidgetView_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        NotifyVerticalScrollMetricsChanged();
    }

    private void PlayerListScrollViewer_ScrollChanged(object sender, ScrollChangedEventArgs e)
    {
        NotifyVerticalScrollMetricsChanged();
    }

    private void PlayerListItem_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button { ContextMenu: { } menu } button)
        {
            return;
        }

        menu.PlacementTarget = button;
        menu.IsOpen = true;
    }

    private void NotifyVerticalScrollMetricsChanged()
    {
        VerticalScrollMetricsChanged?.Invoke(this, EventArgs.Empty);
    }
}
