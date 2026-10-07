using System.Windows;
using System.Windows.Controls;
using StarResonanceDps.App.ViewModels.WidgetSettings;

namespace StarResonanceDps.App.Views.WidgetSettings;

public partial class BuffCardNotificationSettingsView : UserControl
{
    public BuffCardNotificationSettingsView()
    {
        InitializeComponent();
    }

    private BuffCardWidgetSettingsViewModel? ViewModel => DataContext as BuffCardWidgetSettingsViewModel;

    private void AddExpiredNotificationField_Click(object sender, RoutedEventArgs e)
    {
        FormatFieldInsertion.Insert(
            ExpiredNotificationTextBox,
            ViewModel?.ExpiredNotification.SelectedFormatField?.Placeholder);
    }

    private void AddCuisinePotionLowNotificationField_Click(object sender, RoutedEventArgs e)
    {
        FormatFieldInsertion.Insert(
            CuisinePotionLowNotificationTextBox,
            ViewModel?.CuisinePotionLowNotification.SelectedFormatField?.Placeholder);
    }

    private void NotificationTextBox_Loaded(object sender, RoutedEventArgs e)
    {
        FormatFieldInsertion.MoveCaretToEndWhenUnfocused((TextBox)sender);
    }

    private void NotificationTextBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        FormatFieldInsertion.MoveCaretToEndWhenUnfocused((TextBox)sender);
    }
}
