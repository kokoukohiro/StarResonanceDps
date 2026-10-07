using System.Windows;
using System.Windows.Controls;
using StarResonanceDps.App.ViewModels.WidgetSettings;

namespace StarResonanceDps.App.Views.WidgetSettings;

public partial class PlayerListNotificationSettingsView : UserControl
{
    public PlayerListNotificationSettingsView()
    {
        InitializeComponent();
    }

    private MeterWidgetSettingsViewModel? ViewModel => DataContext as MeterWidgetSettingsViewModel;

    private void AddMatchFoundNotificationField_Click(object sender, RoutedEventArgs e)
    {
        FormatFieldInsertion.Insert(
            MatchFoundNotificationTextBox,
            ViewModel?.MatchFoundNotification.SelectedFormatField?.Placeholder);
    }

    private void AddHealthLowNotificationField_Click(object sender, RoutedEventArgs e)
    {
        FormatFieldInsertion.Insert(
            HealthLowNotificationTextBox,
            ViewModel?.HealthLowNotification.SelectedFormatField?.Placeholder);
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
