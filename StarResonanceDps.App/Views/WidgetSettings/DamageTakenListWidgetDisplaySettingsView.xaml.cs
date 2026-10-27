using System.Windows;
using System.Windows.Controls;
using StarResonanceDps.App.ViewModels.WidgetSettings;

namespace StarResonanceDps.App.Views.WidgetSettings;

public partial class DamageTakenListWidgetDisplaySettingsView : UserControl
{
    public DamageTakenListWidgetDisplaySettingsView()
    {
        InitializeComponent();
    }

    private DamageTakenListWidgetSettingsViewModel? ViewModel => DataContext as DamageTakenListWidgetSettingsViewModel;

    /// <summary>選んだ項目をカーソル位置へ差し込む(プレイヤーリストのプレイヤー名と同じ)。</summary>
    private void AddPlayerInfoFormatField_Click(object sender, RoutedEventArgs e)
    {
        FormatFieldInsertion.Insert(
            PlayerInfoFormatTextBox,
            ViewModel?.SelectedPlayerInfoFormatField?.Placeholder);
    }

    /// <summary>選んだ項目をカーソル位置へ差し込む(「プレイヤー名」と同じ)。</summary>
    private void AddHitInfoFormatField_Click(object sender, RoutedEventArgs e)
    {
        FormatFieldInsertion.Insert(
            HitInfoFormatTextBox,
            ViewModel?.SelectedHitInfoFormatField?.Placeholder);
    }

    private void FormatTextBox_Loaded(object sender, RoutedEventArgs e)
    {
        if (sender is TextBox textBox)
        {
            FormatFieldInsertion.MoveCaretToEndWhenUnfocused(textBox);
        }
    }

    private void FormatTextBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (sender is TextBox textBox)
        {
            FormatFieldInsertion.MoveCaretToEndWhenUnfocused(textBox);
        }
    }
}
