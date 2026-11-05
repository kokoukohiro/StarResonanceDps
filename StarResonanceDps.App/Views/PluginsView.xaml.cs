using System.Windows.Controls;

namespace StarResonanceDps.App.Views;

public partial class PluginsView : UserControl
{
    internal ScrollViewer ContentScrollViewer => PluginScrollViewer;

    public PluginsView()
    {
        InitializeComponent();
    }
}
