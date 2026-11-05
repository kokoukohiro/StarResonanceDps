using System.Windows;
using System.Windows.Controls;

namespace StarResonanceDps.App.Views.Widgets;

/// <summary>プラグインのウィジェットの本体(スクロールしない)とフッターの置き場。余白は付けない。</summary>
public partial class PluginWidgetBodyView : UserControl
{
    public PluginWidgetBodyView(FrameworkElement element)
    {
        InitializeComponent();
        ElementHost.Content = element;
    }
}
