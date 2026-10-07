using System.Windows;
using System.Windows.Controls;
using StarResonanceDps.App.ViewModels.WidgetSettings;

namespace StarResonanceDps.App.Views.WidgetSettings;

public partial class TakenDamageLogNotificationSettingsView : UserControl
{
    public TakenDamageLogNotificationSettingsView()
    {
        InitializeComponent();
    }

    private TakenDamageLogWidgetSettingsViewModel? ViewModel => DataContext as TakenDamageLogWidgetSettingsViewModel;

    private void AddTelegraphedSkillNotificationField_Click(object sender, RoutedEventArgs e)
    {
        FormatFieldInsertion.Insert(
            TelegraphedSkillNotificationTextBox,
            ViewModel?.TelegraphedSkillNotification.SelectedFormatField?.Placeholder);
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
