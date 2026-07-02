using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Threading;
using StarResonanceDps.App.Config;
using StarResonanceDps.App.ViewModels;

namespace StarResonanceDps.App.Views.Widgets;

public partial class WidgetWindow : Window
{
    private const int WmNcHitTest = 0x0084;
    private const int HtClient = 1;
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
    private readonly DispatcherTimer _saveBoundsTimer;
    private bool _isRestoringBounds = true;
    private bool _isSynchronizingPlayerListScrollBar;

    public WidgetWindow(WidgetListItemViewModel widget, WidgetWindowConfig savedBounds, Window? owner)
    {
        _widget = widget;

        InitializeComponent();
        DataContext = widget;
        PlayerListScrollViewer.ScrollChanged += PlayerListScrollViewer_ScrollChanged;
        PlayerListScrollBar.ValueChanged += PlayerListScrollBar_ValueChanged;

        // Widgets are top-level windows so the manager can be activated above every
        // unpinned widget. Pinned widgets still use Topmost through ApplyPinState.
        ApplySavedBounds(savedBounds, owner, widget.OriginalIndex);
        ApplyPinState(widget.IsPinned);

        _saveBoundsTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(300)
        };
        _saveBoundsTimer.Tick += SaveBoundsTimer_Tick;

        Loaded += WidgetWindow_Loaded;
        LocationChanged += WidgetWindow_BoundsChanged;
        SizeChanged += WidgetWindow_SizeChanged;
        StateChanged += WidgetWindow_StateChanged;
        SourceInitialized += WidgetWindow_SourceInitialized;
    }

    public WidgetListItemViewModel Widget => _widget;

    public void ApplyPinState(bool isPinned)
    {
        Topmost = isPinned;
        ResizeMode = isPinned
            ? ResizeMode.NoResize
            : ResizeMode.CanResize;
    }

    protected override void OnClosed(EventArgs e)
    {
        SaveBounds();
        _saveBoundsTimer.Stop();
        _saveBoundsTimer.Tick -= SaveBoundsTimer_Tick;
        PlayerListScrollViewer.ScrollChanged -= PlayerListScrollViewer_ScrollChanged;
        PlayerListScrollBar.ValueChanged -= PlayerListScrollBar_ValueChanged;
        base.OnClosed(e);
    }

    private void WidgetWindow_Loaded(object sender, RoutedEventArgs e)
    {
        _isRestoringBounds = false;
        UpdateWindowRootClip();
        QueuePlayerListScrollBarUpdate();
    }

    private void WidgetWindow_SourceInitialized(object? sender, EventArgs e)
    {
        if (PresentationSource.FromVisual(this) is HwndSource source)
        {
            source.AddHook(WndProc);
        }
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

        var hitTest = HtClient;

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

        if (hitTest == HtClient)
        {
            return IntPtr.Zero;
        }

        handled = true;
        return new IntPtr(hitTest);
    }

    private void Header_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (_widget.IsPinned || e.LeftButton != MouseButtonState.Pressed)
        {
            return;
        }

        DragMove();
    }

    private void CloseButton_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }

    private void WidgetWindow_BoundsChanged(object? sender, EventArgs e)
    {
        ScheduleBoundsSave();
    }

    private void WidgetWindow_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        UpdateWindowRootClip();
        QueuePlayerListScrollBarUpdate();
        ScheduleBoundsSave();
    }

    private void WidgetWindow_StateChanged(object? sender, EventArgs e)
    {
        UpdateWindowRootClip();
    }

    private void UpdateWindowRootClip()
    {
        if (WindowState == WindowState.Maximized)
        {
            WindowRoot.Clip = null;
            return;
        }

        if (WindowRoot.ActualWidth <= 0 || WindowRoot.ActualHeight <= 0)
        {
            return;
        }

        var cornerRadius = TryFindResource("Radius.Default") is CornerRadius configuredRadius
            ? configuredRadius.TopLeft
            : 6d;

        WindowRoot.Clip = new RectangleGeometry(
            new Rect(0, 0, WindowRoot.ActualWidth, WindowRoot.ActualHeight),
            cornerRadius,
            cornerRadius);
    }

    private void PlayerListScrollViewer_ScrollChanged(object sender, ScrollChangedEventArgs e)
    {
        UpdatePlayerListScrollBar();
    }

    private void QueuePlayerListScrollBarUpdate()
    {
        Dispatcher.BeginInvoke(
            UpdatePlayerListScrollBar,
            DispatcherPriority.Loaded);
    }

    private void PlayerListScrollBar_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (_isSynchronizingPlayerListScrollBar)
        {
            return;
        }

        PlayerListScrollViewer.ScrollToVerticalOffset(e.NewValue);
    }

    private void UpdatePlayerListScrollBar()
    {
        if (!_widget.IsPlayerList || !IsLoaded)
        {
            PlayerListScrollBar.Visibility = Visibility.Collapsed;
            return;
        }

        _isSynchronizingPlayerListScrollBar = true;
        try
        {
            var maximum = Math.Max(PlayerListScrollViewer.ScrollableHeight, 0);
            var viewport = Math.Max(PlayerListScrollViewer.ViewportHeight, 0);

            PlayerListScrollBar.Minimum = 0;
            PlayerListScrollBar.Maximum = maximum;
            PlayerListScrollBar.ViewportSize = viewport;
            PlayerListScrollBar.LargeChange = Math.Max(viewport * 0.9, 1);
            PlayerListScrollBar.SmallChange = 42;
            PlayerListScrollBar.Value = Math.Min(PlayerListScrollViewer.VerticalOffset, maximum);
            PlayerListScrollBar.Visibility = maximum > 0
                ? Visibility.Visible
                : Visibility.Collapsed;
        }
        finally
        {
            _isSynchronizingPlayerListScrollBar = false;
        }
    }

    private void ScheduleBoundsSave()
    {
        if (_isRestoringBounds || !IsLoaded || WindowState == WindowState.Minimized)
        {
            return;
        }

        _saveBoundsTimer.Stop();
        _saveBoundsTimer.Start();
    }

    private void SaveBoundsTimer_Tick(object? sender, EventArgs e)
    {
        _saveBoundsTimer.Stop();
        SaveBounds();
    }

    private void SaveBounds()
    {
        if (_isRestoringBounds
            || !IsLoaded
            || WindowState == WindowState.Minimized
            || !IsFinitePositive(ActualWidth)
            || !IsFinitePositive(ActualHeight)
            || !double.IsFinite(Left)
            || !double.IsFinite(Top))
        {
            return;
        }

        WidgetStateManager.Instance.SaveWidgetWindowBounds(
            _widget.Kind,
            Left,
            Top,
            ActualWidth,
            ActualHeight);
    }

    private void ApplySavedBounds(WidgetWindowConfig savedBounds, Window? owner, int originalIndex)
    {
        if (IsFinitePositive(savedBounds.Width) && IsFinitePositive(savedBounds.Height))
        {
            Width = Math.Max(savedBounds.Width!.Value, MinWidth);
            Height = Math.Max(savedBounds.Height!.Value, MinHeight);
        }

        if (double.IsFinite(savedBounds.X ?? double.NaN) && double.IsFinite(savedBounds.Y ?? double.NaN))
        {
            Left = savedBounds.X!.Value;
            Top = savedBounds.Y!.Value;
            return;
        }

        if (owner is null)
        {
            WindowStartupLocation = WindowStartupLocation.CenterScreen;
            return;
        }

        const double initialOffset = 44;
        const double cascadeOffset = 20;
        Left = owner.Left + initialOffset + (cascadeOffset * originalIndex);
        Top = owner.Top + initialOffset + (cascadeOffset * originalIndex);
    }

    private static bool IsFinitePositive(double? value)
    {
        return value.HasValue && double.IsFinite(value.Value) && value.Value > 0;
    }

    private static Point GetScreenPoint(IntPtr lParam)
    {
        var value = lParam.ToInt64();
        var x = unchecked((short)(value & 0xFFFF));
        var y = unchecked((short)((value >> 16) & 0xFFFF));
        return new Point(x, y);
    }
}
