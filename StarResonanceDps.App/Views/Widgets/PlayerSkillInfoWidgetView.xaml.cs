using System.Windows;
using System.Windows.Controls;

namespace StarResonanceDps.App.Views.Widgets;

public partial class PlayerSkillInfoWidgetView : UserControl, IWidgetVerticalScrollContent
{
    public PlayerSkillInfoWidgetView()
    {
        InitializeComponent();
    }

    public event EventHandler? VerticalScrollMetricsChanged;

    public WidgetVerticalScrollMetrics GetVerticalScrollMetrics()
    {
        var maximum = Math.Max(SkillInfoScrollViewer.ScrollableHeight, 0d);
        var viewport = Math.Max(SkillInfoScrollViewer.ViewportHeight, 0d);

        return new WidgetVerticalScrollMetrics(
            maximum,
            viewport,
            Math.Min(SkillInfoScrollViewer.VerticalOffset, maximum),
            Math.Max(viewport * 0.9d, 1d),
            32d);
    }

    public void SetVerticalScrollOffset(double verticalOffset)
    {
        var maximum = Math.Max(SkillInfoScrollViewer.ScrollableHeight, 0d);
        var offset = double.IsFinite(verticalOffset)
            ? Math.Clamp(verticalOffset, 0d, maximum)
            : 0d;

        SkillInfoScrollViewer.ScrollToVerticalOffset(offset);
    }

    private void PlayerSkillInfoWidgetView_Loaded(object sender, RoutedEventArgs e)
    {
        NotifyVerticalScrollMetricsChanged();
    }

    private void PlayerSkillInfoWidgetView_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        NotifyVerticalScrollMetricsChanged();
    }

    private void SkillInfoScrollViewer_ScrollChanged(object sender, ScrollChangedEventArgs e)
    {
        NotifyVerticalScrollMetricsChanged();
    }

    private void NotifyVerticalScrollMetricsChanged()
    {
        VerticalScrollMetricsChanged?.Invoke(this, EventArgs.Empty);
    }
}
