using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Threading;
using StarResonanceDps.Plugins.KeybindTool.ViewModels;

namespace StarResonanceDps.Plugins.KeybindTool.Views;

public partial class KeybindToolView : UserControl, IDisposable
{
    private readonly KeybindToolViewModel _viewModel;
    private ScrollViewer? _contentScrollViewer;
    private bool _isSyncingExternalScrollBar;

    internal KeybindToolView(KeybindToolViewModel viewModel)
    {
        ArgumentNullException.ThrowIfNull(viewModel);

        _viewModel = viewModel;
        InitializeComponent();
        DataContext = viewModel;
        Loaded += KeybindToolView_Loaded;
    }

    /// <summary>本体が窓を閉じた後に呼ぶ。</summary>
    public void Dispose()
    {
        _viewModel.Dispose();
    }

    private void KeybindToolView_Loaded(object sender, RoutedEventArgs e)
    {
        // 一覧のスクロールは一覧のテンプレートの中にある。
        ActionList.ApplyTemplate();
        _contentScrollViewer = ActionList.Template.FindName("ContentScrollViewer", ActionList) as ScrollViewer;
        QueueUpdateExternalScrollBar();

        if (DataContext is KeybindToolViewModel viewModel)
        {
            Dispatcher.BeginInvoke(
                viewModel.ShowPendingInitialPresetUnavailableMessage,
                DispatcherPriority.ContextIdle);
            Dispatcher.BeginInvoke(
                viewModel.ShowPendingLayoutLoadFailedMessage,
                DispatcherPriority.ContextIdle);
        }
    }

    private void ContentScrollViewer_ScrollChanged(object sender, ScrollChangedEventArgs e)
    {
        UpdateExternalScrollBar();
    }

    private void ContentScrollViewer_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        QueueUpdateExternalScrollBar();
    }

    private void ContentExternalScrollBar_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (_isSyncingExternalScrollBar || !IsLoaded || _contentScrollViewer is null)
        {
            return;
        }

        _contentScrollViewer.ScrollToVerticalOffset(e.NewValue);
    }

    private void QueueUpdateExternalScrollBar()
    {
        Dispatcher.BeginInvoke(
            UpdateExternalScrollBar,
            DispatcherPriority.Loaded);
    }

    private void UpdateExternalScrollBar()
    {
        var scrollViewer = _contentScrollViewer;
        if (scrollViewer is null)
        {
            return;
        }

        _isSyncingExternalScrollBar = true;

        try
        {
            var maximum = Math.Max(scrollViewer.ScrollableHeight, 0);
            ContentExternalScrollBar.Maximum = maximum;
            ContentExternalScrollBar.ViewportSize = Math.Max(scrollViewer.ViewportHeight, 0);
            ContentExternalScrollBar.LargeChange = Math.Max(scrollViewer.ViewportHeight * 0.9, 1);
            ContentExternalScrollBar.SmallChange = 48;
            ContentExternalScrollBar.Value = Math.Min(scrollViewer.VerticalOffset, maximum);

            var isScrollBarVisible = maximum > 0;
            ContentExternalScrollBar.Visibility = isScrollBarVisible
                ? Visibility.Visible
                : Visibility.Collapsed;
            ContentExternalScrollBarColumn.Width = isScrollBarVisible
                ? new GridLength(16)
                : new GridLength(8);
        }
        finally
        {
            _isSyncingExternalScrollBar = false;
        }
    }
}
