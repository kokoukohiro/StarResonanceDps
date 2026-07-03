using System.Windows;
using System.Windows.Controls;
using StarResonanceDps.App.ViewModels;

namespace StarResonanceDps.App.Views.Settings;

public partial class ClassColorSettingsView : UserControl
{
    public ClassColorSettingsView()
    {
        InitializeComponent();
    }

    private ClassColorSettingsViewModel ViewModel => (ClassColorSettingsViewModel)DataContext;

    private void ColorOptionRadioButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement { DataContext: ColorOptionViewModel option })
        {
            option.Select();
        }
    }

    private void ColorPickerButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not FrameworkElement { Tag: string classKey })
        {
            return;
        }

        var owner = Window.GetWindow(this);
        var window = new ColorPickerWindow(ViewModel.GetSelectedClassColor(classKey));

        if (owner is not null)
        {
            window.Owner = owner;
        }

        if (window.ShowDialog() == true)
        {
            ViewModel.ApplyClassColor(classKey, window.SelectedColor);
        }
    }
}
