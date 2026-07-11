using System;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Threading;
using StarResonanceDps.App.ViewModels;

namespace StarResonanceDps.App.Views;

public partial class ManagerView : UserControl
{
    private const string HelpUrl = "https://github.com/kokoukohiro/StarResonanceDps";

    private bool _isSyncingExternalScrollBar;
    private MainViewModel? _viewModel;

    public ManagerView()
    {
        InitializeComponent();
        LogsContent.ContentScrollViewer.ScrollChanged += LogsContentScrollViewer_ScrollChanged;
        LogsContent.ContentScrollViewer.SizeChanged += LogsContentScrollViewer_SizeChanged;
        Loaded += ManagerView_Loaded;
        Unloaded += ManagerView_Unloaded;
    }

    private void ManagerView_Loaded(object sender, RoutedEventArgs e)
    {
        AttachViewModel();
        WidgetsNavigationButton.IsChecked = true;
    }

    private void ManagerView_Unloaded(object sender, RoutedEventArgs e)
    {
        DetachViewModel();
    }

    private void AttachViewModel()
    {
        var viewModel = DataContext as MainViewModel;
        if (ReferenceEquals(_viewModel, viewModel))
        {
            return;
        }

        DetachViewModel();
        _viewModel = viewModel;

        if (_viewModel is not null)
        {
            _viewModel.HistoryWindowRequested += ViewModel_HistoryWindowRequested;
        }
    }

    private void DetachViewModel()
    {
        if (_viewModel is not null)
        {
            _viewModel.HistoryWindowRequested -= ViewModel_HistoryWindowRequested;
            _viewModel = null;
        }
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
        ShowManagerDialog(new SettingsWindow());
    }

    private void WidgetActionComboBox_DropDownClosed(object sender, EventArgs e)
    {
        if (sender is not ComboBox comboBox)
        {
            return;
        }

        if (DataContext is MainViewModel viewModel)
        {
            viewModel.WidgetActionIndex = -1;
        }

        comboBox.SetCurrentValue(Selector.SelectedIndexProperty, -1);
        comboBox.Items.MoveCurrentToPosition(-1);
    }

    private void ViewModel_HistoryWindowRequested(object? sender, EventArgs e)
    {
        Dispatcher.BeginInvoke(
            () => ShowManagerDialog(new HistoryWindow()),
            DispatcherPriority.ContextIdle);
    }

    private void ShowManagerDialog(Window dialog)
    {
        var managerWindow = Window.GetWindow(this);

        if (managerWindow is not null)
        {
            const double leftOffset = 24;
            const double topOffset = 72;

            dialog.Owner = managerWindow;
            dialog.WindowStartupLocation = WindowStartupLocation.Manual;
            dialog.Left = managerWindow.Left + leftOffset;
            dialog.Top = managerWindow.Top + topOffset;
        }

        dialog.ShowDialog();
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
