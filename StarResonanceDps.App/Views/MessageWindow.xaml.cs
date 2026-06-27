using System.Windows;
using System.Windows.Input;

namespace StarResonanceDps.App.Views;

public partial class MessageWindow : Window
{
    public MessageWindow()
    {
        InitializeComponent();
    }

    public static void Show(Window? owner, string title, string message)
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
        window.ShowDialog();
    }

    private void Header_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ButtonState == MouseButtonState.Pressed)
        {
            DragMove();
        }
    }

    private void CloseButton_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
    }

    private void OkButton_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = true;
    }
}
