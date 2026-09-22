using System.Windows;
using System.Windows.Input;
using StarResonanceDps.App.Services;

namespace StarResonanceDps.App.Views;

public partial class MessageWindow : Window
{
    public MessageWindow()
    {
        InitializeComponent();
    }

    public static void Show(Window? owner, string title, string message)
    {
        Show(owner, title, message, null);
    }

    public static void Show(Window? owner, string title, string message, string? detail)
    {
        ArgumentNullException.ThrowIfNull(title);
        ArgumentNullException.ThrowIfNull(message);

        var window = new MessageWindow
        {
            Owner = owner,
            WindowStartupLocation = owner is null
                ? System.Windows.WindowStartupLocation.CenterScreen
                : System.Windows.WindowStartupLocation.CenterOwner
        };

        window.HeaderText.Text = title;
        window.MessageText.Text = message;
        window.DetailText.Text = detail ?? string.Empty;
        window.DetailText.Visibility = string.IsNullOrWhiteSpace(detail)
            ? Visibility.Collapsed
            : Visibility.Visible;

        // ShowDialog はアプリのウィンドウを全部止めるので、ウィジェットも操作できなくなる。止めるのはオーナーだけにする。
        OwnerModalWindow.ShowAndWait(window, owner);
    }

    private void Header_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ButtonState == MouseButtonState.Pressed)
        {
            DragMove();
        }
    }

    // DialogResult は ShowDialog で表示した窓にしか設定できないので、閉じるだけにする(結果は使わない)。
    private void CloseButton_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }

    private void OkButton_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }
}
