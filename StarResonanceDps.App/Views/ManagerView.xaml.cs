using System.Windows.Controls;

namespace StarResonanceDps.App.Views;

public partial class ManagerView : UserControl
{
    public ManagerView()
    {
        InitializeComponent();
    }

    private void MoreActionComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
{
    if (sender is not ComboBox comboBox)
    {
        return;
    }

    if (comboBox.SelectedItem is not ComboBoxItem item)
    {
        return;
    }

    var action = item.Content?.ToString();

    switch (action)
    {
        case "すべてのお気に入りを起動":
            break;

        case "すべてを停止":
            break;
    }

    comboBox.SelectedIndex = -1;
}
}
