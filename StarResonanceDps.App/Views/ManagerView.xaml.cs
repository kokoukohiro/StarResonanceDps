using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Threading;

namespace StarResonanceDps.App.Views;

public partial class ManagerView : UserControl
{
    private SettingsWindow? _settingsWindow;
    private bool _isSyncingExternalScrollBar;

    public ManagerView()
    {
        InitializeComponent();
        Loaded += ManagerView_Loaded;
    }

    private void ManagerView_Loaded(object sender, RoutedEventArgs e)
    {
        QueueUpdateExternalScrollBar();
    }

    private void SettingsButton_Click(object sender, RoutedEventArgs e)
    {
        if (_settingsWindow is { IsVisible: true })
        {
            _settingsWindow.Activate();
            return;
        }

        _settingsWindow = new SettingsWindow
        {
            Owner = Window.GetWindow(this)
        };

        _settingsWindow.Closed += (_, _) => _settingsWindow = null;
        _settingsWindow.Show();
    }

    private void WidgetListScrollViewer_ScrollChanged(object sender, ScrollChangedEventArgs e)
    {
        UpdateExternalScrollBar(WidgetListScrollViewer, WidgetListExternalScrollBar);
    }

    private void WidgetListScrollViewer_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        QueueUpdateExternalScrollBar();
    }

    private void WidgetItemsControl_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        QueueUpdateExternalScrollBar();
    }

    private void WidgetListExternalScrollBar_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (_isSyncingExternalScrollBar || !IsLoaded)
        {
            return;
        }

        WidgetListScrollViewer.ScrollToVerticalOffset(e.NewValue);
    }

    private void QueueUpdateExternalScrollBar()
    {
        Dispatcher.BeginInvoke(
            () => UpdateExternalScrollBar(WidgetListScrollViewer, WidgetListExternalScrollBar),
            DispatcherPriority.Loaded);
    }

    private void UpdateExternalScrollBar(ScrollViewer scrollViewer, ScrollBar scrollBar)
    {
        _isSyncingExternalScrollBar = true;

        try
        {
            var maximum = Math.Max(scrollViewer.ScrollableHeight, 0);
            scrollBar.Maximum = maximum;
            scrollBar.ViewportSize = Math.Max(scrollViewer.ViewportHeight, 0);
            scrollBar.LargeChange = Math.Max(scrollViewer.ViewportHeight * 0.9, 1);
            scrollBar.SmallChange = 48;
            scrollBar.Value = Math.Min(scrollViewer.VerticalOffset, maximum);
            var isScrollBarVisible = maximum > 0;

            scrollBar.Visibility = isScrollBarVisible ? Visibility.Visible : Visibility.Collapsed;
            WidgetListExternalScrollBarColumn.Width = isScrollBarVisible
                ? new GridLength(16)
                : new GridLength(8);
        }
        finally
        {
            _isSyncingExternalScrollBar = false;
        }
    }
}
