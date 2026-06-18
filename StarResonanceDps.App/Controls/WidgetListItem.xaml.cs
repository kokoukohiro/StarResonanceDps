using System.Windows;
using System.Windows.Controls;
using StarResonanceDps.App.ViewModels;
using StarResonanceDps.App.Views;

namespace StarResonanceDps.App.Controls;

public partial class WidgetListItem : UserControl
{
    public WidgetListItem()
    {
        InitializeComponent();
    }

    private void SettingsButton_Click(object sender, RoutedEventArgs e)
    {
        if (DataContext is not WidgetListItemViewModel widget)
        {
            return;
        }

        var owner = Window.GetWindow(this);
        var settingsWindow = new WidgetSettingsWindow(widget);

        if (owner is not null)
        {
            const double leftOffset = 24;
            const double topOffset = 72;

            settingsWindow.Owner = owner;
            settingsWindow.WindowStartupLocation = WindowStartupLocation.Manual;
            settingsWindow.Left = owner.Left + leftOffset;
            settingsWindow.Top = owner.Top + topOffset;
        }

        settingsWindow.ShowDialog();
    }
}
