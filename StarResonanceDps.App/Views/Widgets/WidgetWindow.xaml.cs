using System;
using System.ComponentModel;
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

    public static readonly DependencyProperty HeaderTextProperty = DependencyProperty.Register(
        nameof(HeaderText),
        typeof(string),
        typeof(WidgetWindow),
        new PropertyMetadata(string.Empty));

    private readonly WidgetListItemViewModel _widget;
    private readonly IWidgetVerticalScrollContent? _verticalScrollContent;
    private readonly DispatcherTimer _saveBoundsTimer;
    private readonly bool _usesWidgetDisplayNameForHeader;
    private bool _isRestoringBounds = true;
    private bool _isSynchronizingContentScrollBar;

    public WidgetWindow(
        WidgetListItemViewModel widget,
        FrameworkElement? widgetContent,
        WidgetWindowConfig savedBounds,
        Window? owner,
        string? headerText = null,
        FrameworkElement? headerChromeActions = null,
        FrameworkElement? headerActions = null,
        FrameworkElement? footerContent = null)
    {
        _widget = widget;
        _usesWidgetDisplayNameForHeader = string.IsNullOrWhiteSpace(headerText);

        InitializeComponent();
        DataContext = widget;
        HeaderText = _usesWidgetDisplayNameForHeader
            ? widget.DisplayName
            : headerText!;
        WidgetContentHost.Content = widgetContent;
        WidgetHeaderChromeActionsHost.Content = headerChromeActions;
        WidgetHeaderActionsHost.Content = headerActions;
        SetFooterContent(footerContent);
        _widget.PropertyChanged += Widget_PropertyChanged;

        _verticalScrollContent = widgetContent as IWidgetVerticalScrollContent;
        if (_verticalScrollContent is not null)
        {
            Grid.SetColumnSpan(WidgetContentHost, 1);
            _verticalScrollContent.VerticalScrollMetricsChanged += VerticalScrollContent_VerticalScrollMetricsChanged;
            WidgetContentScrollBar.ValueChanged += WidgetContentScrollBar_ValueChanged;
        }

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

    public string HeaderText
    {
        get => (string)GetValue(HeaderTextProperty);
        private set => SetValue(HeaderTextProperty, value);
    }

    public void SetHeaderText(string headerText)
    {
        HeaderText = string.IsNullOrWhiteSpace(headerText)
            ? _widget.DisplayName
            : headerText;
    }

    public void ApplyPinState(bool isPinned)
    {
        Topmost = isPinned;
        QueueContentScrollBarUpdate();
    }

    private void SetFooterContent(FrameworkElement? footerContent)
    {
        WidgetFooterHost.Content = footerContent;

        var visibility = footerContent is null
            ? Visibility.Collapsed
            : Visibility.Visible;

        WidgetFooterFrame.Visibility = visibility;
        WidgetFooterHost.Visibility = visibility;
    }

    protected override void OnClosed(EventArgs e)
    {
        SaveBounds();
        _saveBoundsTimer.Stop();
        _saveBoundsTimer.Tick -= SaveBoundsTimer_Tick;
        _widget.PropertyChanged -= Widget_PropertyChanged;

        if (_verticalScrollContent is not null)
        {
            _verticalScrollContent.VerticalScrollMetricsChanged -= VerticalScrollContent_VerticalScrollMetricsChanged;
            WidgetContentScrollBar.ValueChanged -= WidgetContentScrollBar_ValueChanged;
        }

        if (WidgetContentHost.Content is FrameworkElement { DataContext: IDisposable disposable })
        {
            disposable.Dispose();
        }

        base.OnClosed(e);
    }

    private void WidgetWindow_Loaded(object sender, RoutedEventArgs e)
    {
        _isRestoringBounds = false;
        UpdateWindowRootClip();
        QueueContentScrollBarUpdate();
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
        if (msg != WmNcHitTest || WindowState == WindowState.Maximized)
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
        if (e.LeftButton != MouseButtonState.Pressed)
        {
            return;
        }

        DragMove();
    }

    private void CloseButton_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }

    private void Widget_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (_usesWidgetDisplayNameForHeader
            && e.PropertyName == nameof(WidgetListItemViewModel.DisplayName))
        {
            HeaderText = _widget.DisplayName;
        }
    }

    private void WidgetWindow_BoundsChanged(object? sender, EventArgs e)
    {
        ScheduleBoundsSave();
    }

    private void WidgetWindow_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        UpdateWindowRootClip();
        QueueContentScrollBarUpdate();
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

    private void VerticalScrollContent_VerticalScrollMetricsChanged(object? sender, EventArgs e)
    {
        UpdateContentScrollBar();
    }

    private void QueueContentScrollBarUpdate()
    {
        if (_verticalScrollContent is null)
        {
            WidgetContentScrollBar.Visibility = Visibility.Collapsed;
            WidgetContentScrollBarColumn.Width = new GridLength(5);
            return;
        }

        Dispatcher.BeginInvoke(
            UpdateContentScrollBar,
            DispatcherPriority.Loaded);
    }

    private void WidgetContentScrollBar_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (_isSynchronizingContentScrollBar || _verticalScrollContent is null)
        {
            return;
        }

        _verticalScrollContent.SetVerticalScrollOffset(e.NewValue);
    }

    private void UpdateContentScrollBar()
    {
        if (_verticalScrollContent is null || !IsLoaded)
        {
            WidgetContentScrollBar.Visibility = Visibility.Collapsed;
            WidgetContentScrollBarColumn.Width = new GridLength(5);
            return;
        }

        var metrics = _verticalScrollContent.GetVerticalScrollMetrics();
        var maximum = Math.Max(metrics.Maximum, 0);
        var viewport = Math.Max(metrics.ViewportSize, 0);

        _isSynchronizingContentScrollBar = true;
        try
        {
            WidgetContentScrollBar.Minimum = 0;
            WidgetContentScrollBar.Maximum = maximum;
            WidgetContentScrollBar.ViewportSize = viewport;
            WidgetContentScrollBar.LargeChange = Math.Max(metrics.LargeChange, 1);
            WidgetContentScrollBar.SmallChange = Math.Max(metrics.SmallChange, 1);
            WidgetContentScrollBar.Value = Math.Clamp(metrics.Value, 0, maximum);

            var isScrollBarVisible = maximum > 0;

            WidgetContentScrollBar.Visibility = isScrollBarVisible
                ? Visibility.Visible
                : Visibility.Collapsed;

            WidgetContentScrollBarColumn.Width = isScrollBarVisible
                ? new GridLength(16)
                : new GridLength(5);
        }
        finally
        {
            _isSynchronizingContentScrollBar = false;
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
