using System;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Threading;

namespace StarResonanceDps.App.Views;

public partial class ManagerView : UserControl
{
    private const string HelpUrl = "https://github.com/kokoukohiro/StarResonanceDps";

    private bool _isSyncingExternalScrollBar;

    public ManagerView()
    {
        InitializeComponent();
        LogsContent.ContentScrollViewer.ScrollChanged += LogsContentScrollViewer_ScrollChanged;
        LogsContent.ContentScrollViewer.SizeChanged += LogsContentScrollViewer_SizeChanged;
        Loaded += ManagerView_Loaded;
    }

    private void ManagerView_Loaded(object sender, RoutedEventArgs e)
    {
        WidgetsNavigationButton.IsChecked = true;
    }

    private void WidgetsNavigationButton_Checked(object sender, RoutedEventArgs e)
    {
        ShowNavigationContent(ManagerContent.Widgets);
    }

    private void PluginsNavigationButton_Checked(object sender, RoutedEventArgs e)
    {
        ShowNavigationContent(ManagerContent.Plugins);
    }

    private void LogsNavigationButton_Checked(object sender, RoutedEventArgs e)
    {
        ShowNavigationContent(ManagerContent.Logs);
    }

    private void HelpButton_Click(object sender, RoutedEventArgs e)
    {
        Process.Start(new ProcessStartInfo(HelpUrl)
        {
            UseShellExecute = true
        });
    }

    private void ShowNavigationContent(ManagerContent content)
    {
        WidgetContent.Visibility = content == ManagerContent.Widgets
            ? Visibility.Visible
            : Visibility.Collapsed;
        PluginsContent.Visibility = content == ManagerContent.Plugins
            ? Visibility.Visible
            : Visibility.Collapsed;
        LogsContent.Visibility = content == ManagerContent.Logs
            ? Visibility.Visible
            : Visibility.Collapsed;

        QueueUpdateExternalScrollBar();
    }

    private void SettingsButton_Click(object sender, RoutedEventArgs e)
    {
        var managerWindow = Window.GetWindow(this);

        var settingsWindow = new SettingsWindow();

        if (managerWindow is not null)
        {
            const double leftOffset = 24;
            const double topOffset = 72;

            settingsWindow.Owner = managerWindow;
            settingsWindow.WindowStartupLocation = WindowStartupLocation.Manual;
            settingsWindow.Left = managerWindow.Left + leftOffset;
            settingsWindow.Top = managerWindow.Top + topOffset;
        }

        settingsWindow.ShowDialog();
    }

    private void WidgetListScrollViewer_ScrollChanged(object sender, ScrollChangedEventArgs e)
    {
        UpdateExternalScrollBar();
    }

    private void WidgetListScrollViewer_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        QueueUpdateExternalScrollBar();
    }

    private void WidgetItemsControl_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        QueueUpdateExternalScrollBar();
    }

    private void LogsContentScrollViewer_ScrollChanged(object sender, ScrollChangedEventArgs e)
    {
        UpdateExternalScrollBar();
    }

    private void LogsContentScrollViewer_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        QueueUpdateExternalScrollBar();
    }

    private void ManagerContentExternalScrollBar_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (_isSyncingExternalScrollBar || !IsLoaded)
        {
            return;
        }

        GetActiveScrollViewer()?.ScrollToVerticalOffset(e.NewValue);
    }

    private void QueueUpdateExternalScrollBar()
    {
        Dispatcher.BeginInvoke(
            UpdateExternalScrollBar,
            DispatcherPriority.Loaded);
    }

    private void UpdateExternalScrollBar()
    {
        var scrollViewer = GetActiveScrollViewer();

        if (scrollViewer is null)
        {
            ManagerContentExternalScrollBar.Visibility = Visibility.Collapsed;
            ManagerContentExternalScrollBarColumn.Width = new GridLength(8);
            return;
        }

        _isSyncingExternalScrollBar = true;

        try
        {
            var maximum = Math.Max(scrollViewer.ScrollableHeight, 0);
            ManagerContentExternalScrollBar.Maximum = maximum;
            ManagerContentExternalScrollBar.ViewportSize = Math.Max(scrollViewer.ViewportHeight, 0);
            ManagerContentExternalScrollBar.LargeChange = Math.Max(scrollViewer.ViewportHeight * 0.9, 1);
            ManagerContentExternalScrollBar.SmallChange = 48;
            ManagerContentExternalScrollBar.Value = Math.Min(scrollViewer.VerticalOffset, maximum);

            var isScrollBarVisible = maximum > 0;
            ManagerContentExternalScrollBar.Visibility = isScrollBarVisible
                ? Visibility.Visible
                : Visibility.Collapsed;
            ManagerContentExternalScrollBarColumn.Width = isScrollBarVisible
                ? new GridLength(16)
                : new GridLength(8);
        }
        finally
        {
            _isSyncingExternalScrollBar = false;
        }
    }

    private ScrollViewer? GetActiveScrollViewer()
    {
        if (WidgetContent.Visibility == Visibility.Visible)
        {
            return WidgetListScrollViewer;
        }

        return LogsContent.Visibility == Visibility.Visible
            ? LogsContent.ContentScrollViewer
            : null;
    }

    private enum ManagerContent
    {
        Widgets,
        Plugins,
        Logs
    }
}
