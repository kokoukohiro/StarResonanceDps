using System.Windows;
using System.Windows.Controls;
using StarResonanceDps.App.ViewModels.WidgetSettings;

namespace StarResonanceDps.App.Views.WidgetSettings;

public partial class BuffCardWidgetDisplaySettingsView : UserControl
{
    public BuffCardWidgetDisplaySettingsView()
    {
        InitializeComponent();
    }

    private BuffCardWidgetSettingsViewModel? ViewModel => DataContext as BuffCardWidgetSettingsViewModel;

    private void AddFormatField_Click(object sender, RoutedEventArgs e)
    {
        FormatFieldInsertion.Insert(
            BuffInfoFormatTextBox,
            ViewModel?.SelectedFormatField?.Placeholder);
    }

    private void BuffInfoFormatTextBox_Loaded(object sender, RoutedEventArgs e)
    {
        FormatFieldInsertion.MoveCaretToEndWhenUnfocused(BuffInfoFormatTextBox);
    }

    private void BuffInfoFormatTextBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        FormatFieldInsertion.MoveCaretToEndWhenUnfocused(BuffInfoFormatTextBox);
    }
}
