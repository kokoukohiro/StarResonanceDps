using System.Windows;
using System.Windows.Controls;
using StarResonanceDps.App.Services;
using StarResonanceDps.App.ViewModels;
using StarResonanceDps.App.ViewModels.WidgetSettings;

namespace StarResonanceDps.App.Views.WidgetSettings;

public partial class BuffListGaugeColorSettingsView : UserControl
{
    public BuffListGaugeColorSettingsView()
    {
        InitializeComponent();
    }

    private BuffListWidgetSettingsViewModel ViewModel => (BuffListWidgetSettingsViewModel)DataContext;

    private void ColorOptionRadioButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement { DataContext: ColorOptionViewModel option })
        {
            option.Select();
        }
    }

    private void ColorPickerButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not FrameworkElement { Tag: string gaugeKey })
        {
            return;
        }

        var owner = Window.GetWindow(this);
        var window = new ColorPickerWindow(ViewModel.GetSelectedGaugeColor(gaugeKey));
        if (owner is not null)
        {
            window.Owner = owner;
        }

        OwnerModalWindow.Show(window, owner, () =>
        {
            if (window.IsConfirmed)
            {
                ViewModel.ApplyGaugeColor(gaugeKey, window.SelectedColor);
            }
        });
    }
}
