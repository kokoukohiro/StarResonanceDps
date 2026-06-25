using System.Windows.Controls;
using StarResonanceDps.Plugins.KeybindTool.ViewModels;

namespace StarResonanceDps.Plugins.KeybindTool.Views;

public partial class KeybindToolView : UserControl
{
    internal KeybindToolView(KeybindToolViewModel viewModel)
    {
        ArgumentNullException.ThrowIfNull(viewModel);

        InitializeComponent();
        DataContext = viewModel;
    }
}
