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
        var window = new ConfirmWindow
        {
            Owner = owner
        };

        window.HeaderText.Text = localization.GetString(titleResourceKey);
        window.MessageText.Text = localization.GetString(messageResourceKey);
        window.DetailText.Text = localization.GetString(detailResourceKey);

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
