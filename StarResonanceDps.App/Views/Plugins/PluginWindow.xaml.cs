using System;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using StarResonanceDps.App.ViewModels;

namespace StarResonanceDps.App.Views.Plugins;

public partial class PluginWindow : Window
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

    public PluginWindow(PluginListItemViewModel plugin, FrameworkElement pluginContent, Window? owner)
    {
        ArgumentNullException.ThrowIfNull(plugin);
        ArgumentNullException.ThrowIfNull(pluginContent);

        Plugin = plugin;
        PluginContent = pluginContent;

        InitializeComponent();
        DataContext = plugin;

        Owner = owner;
        PositionRelativeToOwner(owner, plugin.OriginalIndex);

        SourceInitialized += PluginWindow_SourceInitialized;
    }

    public PluginListItemViewModel Plugin { get; }

    public FrameworkElement PluginContent { get; }

    private void PluginWindow_SourceInitialized(object? sender, EventArgs e)
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
        if (e.LeftButton == MouseButtonState.Pressed)
        {
            DragMove();
        }
    }

    private void CloseButton_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }

    private void PositionRelativeToOwner(Window? owner, int originalIndex)
    {
        if (owner is null)
        {
            WindowStartupLocation = WindowStartupLocation.CenterScreen;
            return;
        }

        const double leftOffset = 24;
        const double topOffset = 72;
        const double cascadeOffset = 20;

        Left = owner.Left + leftOffset + (cascadeOffset * originalIndex);
        Top = owner.Top + topOffset + (cascadeOffset * originalIndex);
    }

    private static Point GetScreenPoint(IntPtr lParam)
    {
        var value = lParam.ToInt64();
        var x = unchecked((short)(value & 0xFFFF));
        var y = unchecked((short)((value >> 16) & 0xFFFF));
        return new Point(x, y);
    }
}
