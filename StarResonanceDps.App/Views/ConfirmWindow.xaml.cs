using System.Windows;
using System.Windows.Input;
using StarResonanceDps.App.Localization;

namespace StarResonanceDps.App.Views;

public partial class ConfirmWindow : Window
{
    public ConfirmWindow()
    {
        InitializeComponent();
    }

    public static bool Show(Window owner, string titleResourceKey, string messageResourceKey, string detailResourceKey)
    {
        var localization = LocalizationManager.Instance;
        return ShowText(
            owner,
            localization.GetString(titleResourceKey),
            localization.GetString(messageResourceKey),
            localization.GetString(detailResourceKey));
    }

    public static bool ShowText(Window owner, string title, string message, string detail)
    {
        var window = new ConfirmWindow
        {
            Owner = owner
        };

        window.HeaderText.Text = title;
        window.MessageText.Text = message;
        window.DetailText.Text = detail;

        return window.ShowDialog() == true;
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

    private void NoButton_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
    }

    private void YesButton_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = true;
    }
}
