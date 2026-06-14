using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace StarResonanceDps.App.Controls;

public partial class AppHeader : UserControl
{
    public AppHeader()
    {
        InitializeComponent();
    }

    private Window? OwnerWindow => Window.GetWindow(this);

    private void Header_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (OwnerWindow is not { } window)
        {
            return;
        }

        if (e.ClickCount == 2)
        {
            ToggleMaximize(window);
            return;
        }

        if (e.ButtonState == MouseButtonState.Pressed)
        {
            window.DragMove();
        }
    }

    private void MinimizeButton_Click(object sender, RoutedEventArgs e)
    {
        if (OwnerWindow is { } window)
        {
            window.WindowState = WindowState.Minimized;
        }
    }

    private void MaximizeButton_Click(object sender, RoutedEventArgs e)
    {
        if (OwnerWindow is { } window)
        {
            ToggleMaximize(window);
        }
    }

    private void CloseButton_Click(object sender, RoutedEventArgs e)
    {
        OwnerWindow?.Close();
    }

    private static void ToggleMaximize(Window window)
    {
        window.WindowState = window.WindowState == WindowState.Maximized
            ? WindowState.Normal
            : WindowState.Maximized;
    }
}
