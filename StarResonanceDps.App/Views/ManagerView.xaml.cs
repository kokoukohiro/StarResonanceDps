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

        if (content == ManagerContent.Widgets)
        {
            QueueUpdateExternalScrollBar();
            return;
        }

        WidgetListExternalScrollBar.Visibility = Visibility.Collapsed;
        WidgetListExternalScrollBarColumn.Width = new GridLength(8);
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
        if (WidgetContent.Visibility != Visibility.Visible)
        {
            scrollBar.Visibility = Visibility.Collapsed;
            WidgetListExternalScrollBarColumn.Width = new GridLength(8);
            return;
        }

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

    private enum ManagerContent
    {
        Widgets,
        Plugins,
        Logs
    }
}
