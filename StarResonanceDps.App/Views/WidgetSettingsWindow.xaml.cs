using System;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Interop;
using System.Windows.Threading;
using StarResonanceDps.App.ViewModels;

namespace StarResonanceDps.App.Views;

public partial class WidgetSettingsWindow : Window
{
    private const int WmNcHitTest = 0x0084;

    private const int HtLeft = 10;
    private const int HtRight = 11;
    private const int HtTop = 12;
    private const int HtTopLeft = 13;
    private const int HtTopRight = 14;
    private const int HtBottom = 15;
    private const int HtBottomLeft = 16;
    private const int HtBottomRight = 17;

    private const double ResizeBorderThickness = 8.0;

    private readonly WidgetListItemViewModel _widget;
    private bool _isSyncingExternalScrollBar;

    private WidgetSettingsViewModel ViewModel => (WidgetSettingsViewModel)DataContext;

    public WidgetSettingsWindow(WidgetListItemViewModel widget)
    {
        _widget = widget;

        InitializeComponent();
        DataContext = new WidgetSettingsViewModel(widget.Kind, widget.DisplayName);

        Loaded += WidgetSettingsWindow_Loaded;
        SourceInitialized += WidgetSettingsWindow_SourceInitialized;
    }

    private void WidgetSettingsWindow_Loaded(object sender, RoutedEventArgs e)
    {
        QueueUpdateExternalScrollBar();
    }

    private void WidgetSettingsWindow_SourceInitialized(object? sender, EventArgs e)
    {
        if (PresentationSource.FromVisual(this) is HwndSource source)
        {
            source.AddHook(WndProc);
        }
    }

    protected override void OnClosing(CancelEventArgs e)
    {
        if (ViewModel.HasUnsavedChanges)
        {
            var confirmed = ConfirmWindow.Show(
                this,
                "未保存変更の破棄",
                "保存されていない変更があります。",
                "設定を保存せずに閉じますか？");

            if (!confirmed)
            {
                e.Cancel = true;
                return;
            }
        }

        base.OnClosing(e);
    }

    private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (msg != WmNcHitTest || ResizeMode == ResizeMode.NoResize || WindowState == WindowState.Maximized)
        {
            return IntPtr.Zero;
        }

        var cursor = PointFromScreen(GetScreenPoint(lParam));
        var width = ActualWidth;
        var height = ActualHeight;

        var left = cursor.X >= 0 && cursor.X < ResizeBorderThickness;
        var right = cursor.X <= width && cursor.X > width - ResizeBorderThickness;
        var top = cursor.Y >= 0 && cursor.Y < ResizeBorderThickness;
        var bottom = cursor.Y <= height && cursor.Y > height - ResizeBorderThickness;

        var hitTest = 0;

        if (top && left)
        {
            hitTest = HtTopLeft;
        }
        else if (top && right)
        {
            hitTest = HtTopRight;
        }
        else if (bottom && left)
        {
            hitTest = HtBottomLeft;
        }
        else if (bottom && right)
        {
            hitTest = HtBottomRight;
        }
        else if (left)
        {
            hitTest = HtLeft;
        }
        else if (right)
        {
            hitTest = HtRight;
        }
        else if (top)
        {
            hitTest = HtTop;
        }
        else if (bottom)
        {
            hitTest = HtBottom;
        }

        if (hitTest == 0)
        {
            return IntPtr.Zero;
        }

        handled = true;
        return new IntPtr(hitTest);
    }

    private static Point GetScreenPoint(IntPtr lParam)
    {
        var value = lParam.ToInt64();
        var x = unchecked((short)(value & 0xFFFF));
        var y = unchecked((short)((value >> 16) & 0xFFFF));
        return new Point(x, y);
    }

    private void ThemeNavButton_Click(object sender, RoutedEventArgs e)
    {
        ScrollToSection(ThemeSection);
    }

    private void ClassColorsNavButton_Click(object sender, RoutedEventArgs e)
    {
        ScrollToSection(ClassColorsHost);
    }

    private void ResetButton_Click(object sender, RoutedEventArgs e)
    {
        var confirmed = ConfirmWindow.Show(
            this,
            "設定の初期化",
            "ウィジェット設定を初期化しますか？",
            "このウィジェットの個別設定が初期値に戻ります。");

        if (!confirmed)
        {
            return;
        }

        ViewModel.ResetToDefaults();
    }

    private void SaveButton_Click(object sender, RoutedEventArgs e)
    {
        var theme = ViewModel.SaveSettings();
        _widget.ApplyTheme(theme);
        Close();
    }

    private void ScrollToSection(FrameworkElement target)
    {
        if (ContentScrollViewer.Content is not FrameworkElement content)
        {
            target.BringIntoView();
            QueueUpdateExternalScrollBar();
            return;
        }

        var point = target.TransformToVisual(content).Transform(new Point(0, 0));
        ContentScrollViewer.ScrollToVerticalOffset(Math.Max(point.Y, 0));
        QueueUpdateExternalScrollBar();
    }

    private void ContentScrollViewer_ScrollChanged(object sender, ScrollChangedEventArgs e)
    {
        UpdateExternalScrollBar(ContentScrollViewer, SettingsExternalScrollBar);
    }

    private void ContentScrollViewer_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        QueueUpdateExternalScrollBar();
    }

    private void SettingsExternalScrollBar_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (_isSyncingExternalScrollBar || !IsLoaded)
        {
            return;
        }

        ContentScrollViewer.ScrollToVerticalOffset(e.NewValue);
    }

    private void QueueUpdateExternalScrollBar()
    {
        Dispatcher.BeginInvoke(
            () => UpdateExternalScrollBar(ContentScrollViewer, SettingsExternalScrollBar),
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
            SettingsExternalScrollBarColumn.Width = isScrollBarVisible
                ? new GridLength(16)
                : new GridLength(8);
        }
        finally
        {
            _isSyncingExternalScrollBar = false;
        }
    }
}
