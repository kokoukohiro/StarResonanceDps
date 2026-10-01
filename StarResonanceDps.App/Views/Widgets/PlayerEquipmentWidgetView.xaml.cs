using System;
using System.Windows;
using System.Windows.Controls;

namespace StarResonanceDps.App.Views.Widgets;

/// <summary>
/// 装備詳細。作りはバフ一覧と同じで、縦スクロールはウィンドウ枠側の細いスクロールバーに繋ぐ。
/// 行の TIPS の開き方はスキル詳細と同じ。
/// </summary>
public partial class PlayerEquipmentWidgetView : UserControl, IWidgetVerticalScrollContent
{
    public PlayerEquipmentWidgetView()
    {
        InitializeComponent();
    }

    public event EventHandler? VerticalScrollMetricsChanged;

    public WidgetVerticalScrollMetrics GetVerticalScrollMetrics()
    {
        var maximum = Math.Max(EquipmentScrollViewer.ScrollableHeight, 0);
        var viewport = Math.Max(EquipmentScrollViewer.ViewportHeight, 0);

        return new WidgetVerticalScrollMetrics(
            maximum,
            viewport,
            Math.Min(EquipmentScrollViewer.VerticalOffset, maximum),
            Math.Max(viewport * 0.9, 1),
            34);
    }

    public void SetVerticalScrollOffset(double verticalOffset)
    {
        var maximum = Math.Max(EquipmentScrollViewer.ScrollableHeight, 0);
        var offset = double.IsFinite(verticalOffset)
            ? Math.Clamp(verticalOffset, 0, maximum)
            : 0;

        EquipmentScrollViewer.ScrollToVerticalOffset(offset);
    }

    private void PlayerEquipmentWidgetView_Loaded(object sender, RoutedEventArgs e)
    {
        NotifyVerticalScrollMetricsChanged();
    }

    private void PlayerEquipmentWidgetView_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        NotifyVerticalScrollMetricsChanged();
    }

    private void PlayerEquipmentWidgetView_DataContextChanged(
        object sender,
        DependencyPropertyChangedEventArgs e)
    {
        NotifyVerticalScrollMetricsChanged();
    }

    private void EquipmentScrollViewer_ScrollChanged(object sender, ScrollChangedEventArgs e)
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
    /// TIPS を止めている行(中身が無い)は手でも開かない。
    /// </summary>
    private void EquipmentRow_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not FrameworkElement row
            || row.ToolTip is not ToolTip toolTip
            || !ToolTipService.GetIsEnabled(row))
        {
            return;
        }

        toolTip.PlacementTarget = row;
        toolTip.IsOpen = true;
    }

    /// <summary>手で開いた TIPS は自動で閉じないので、行から離れたら閉じる。</summary>
    private void EquipmentRow_MouseLeave(object sender, System.Windows.Input.MouseEventArgs e)
    {
        if (sender is FrameworkElement row && row.ToolTip is ToolTip toolTip)
        {
            toolTip.IsOpen = false;
        }
    }
}
