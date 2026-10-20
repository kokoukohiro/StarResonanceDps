using System.Windows.Controls;

namespace StarResonanceDps.App.Views.WidgetSettings;

/// <summary>「バフ・デバフ」の一覧。行の並べ替えは一覧に付けた <c>Behaviors.RowReorderBehavior</c> が受け持つ。</summary>
public partial class BuffDebuffGroupEntrySettingsView : UserControl
{
    public BuffDebuffGroupEntrySettingsView()
    {
        InitializeComponent();
    }
}
