using System;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using StarResonanceDps.App.ViewModels;

namespace StarResonanceDps.App.Views.Widgets;

public partial class PlayerEquipmentWidgetView : UserControl, IWidgetVerticalScrollContent
{
    private PlayerEquipmentWidgetViewModel? _viewModel;

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
            24);
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

    private void PlayerEquipmentWidgetView_Unloaded(object sender, RoutedEventArgs e)
    {
        DetachViewModel();
    }

    private void PlayerEquipmentWidgetView_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        NotifyVerticalScrollMetricsChanged();
    }

    private void PlayerEquipmentWidgetView_DataContextChanged(
        object sender,
        DependencyPropertyChangedEventArgs e)
    {
        DetachViewModel();

        if (e.NewValue is PlayerEquipmentWidgetViewModel viewModel)
        {
            _viewModel = viewModel;
            _viewModel.PropertyChanged += ViewModel_PropertyChanged;
        }

        NotifyVerticalScrollMetricsChanged();
    }

    private void EquipmentScrollViewer_ScrollChanged(object sender, ScrollChangedEventArgs e)
    {
        NotifyVerticalScrollMetricsChanged();
    }

    private void ViewModel_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(PlayerEquipmentWidgetViewModel.ShowUnknownAttributes)
            or nameof(PlayerEquipmentWidgetViewModel.EquipmentDataState))
        {
            Dispatcher.BeginInvoke(NotifyVerticalScrollMetricsChanged);
        }
    }

    private void DetachViewModel()
    {
        if (_viewModel is null)
        {
            return;
        }

        _viewModel.PropertyChanged -= ViewModel_PropertyChanged;
        _viewModel = null;
    }

    private void NotifyVerticalScrollMetricsChanged()
    {
        VerticalScrollMetricsChanged?.Invoke(this, EventArgs.Empty);
    }
}
