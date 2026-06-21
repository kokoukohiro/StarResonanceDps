using System;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using StarResonanceDps.App.ViewModels;

namespace StarResonanceDps.App;

public partial class MainWindow : Window
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

    public MainWindow()
    {
        InitializeComponent();
        Loaded += MainWindow_Loaded;
        SourceInitialized += MainWindow_SourceInitialized;
    }

    private void MainWindow_Loaded(object sender, RoutedEventArgs e)
    {
        if (DataContext is MainViewModel viewModel)
        {
            viewModel.RestoreRunningWidgetWindows();
        }
    }

    private void MainWindow_SourceInitialized(object? sender, EventArgs e)
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

        Point cursor = PointFromScreen(GetScreenPoint(lParam));
        double width = ActualWidth;
        double height = ActualHeight;

        bool left = cursor.X >= 0 && cursor.X < ResizeBorderThickness;
        bool right = cursor.X <= width && cursor.X > width - ResizeBorderThickness;
        bool top = cursor.Y >= 0 && cursor.Y < ResizeBorderThickness;
        bool bottom = cursor.Y <= height && cursor.Y > height - ResizeBorderThickness;

        int hitTest = HtClient;

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

    private static Point GetScreenPoint(IntPtr lParam)
    {
        long value = lParam.ToInt64();
        int x = unchecked((short)(value & 0xFFFF));
        int y = unchecked((short)((value >> 16) & 0xFFFF));
        return new Point(x, y);
    }
}
