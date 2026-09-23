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
            // 行1つぶん。行の高さ(31)と合わせる。
            31d);
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

    /// <summary>
    /// 押すと WPF が TIPS を閉じ、カーソルを一度外へ出すまで出し直さない。
    /// 押した行の TIPS を手で開き直す。<b>置き場所(<c>PlacementTarget</c>)を入れてから開く</b> —
    /// 中身の結び付けが置き場所のデータを見ているので、入れないと空の TIPS になる。
    /// </summary>
    private void SkillRow_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not FrameworkElement row || row.ToolTip is not ToolTip toolTip)
        {
            return;
        }

        toolTip.PlacementTarget = row;
        toolTip.IsOpen = true;
    }

    /// <summary>手で開いた TIPS は自動で閉じないので、行から離れたら閉じる。</summary>
    private void SkillRow_MouseLeave(object sender, System.Windows.Input.MouseEventArgs e)
    {
        if (sender is FrameworkElement row && row.ToolTip is ToolTip toolTip)
        {
            toolTip.IsOpen = false;
        }
    }
}
