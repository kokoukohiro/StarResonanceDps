using System.Windows.Controls;

namespace StarResonanceDps.App.Views.Widgets;

/// <summary>
/// バフ・デバフを1件だけ大きく出すカード。
/// プレイヤー用とモンスター用の2つのViewModelが同じこのビューを使う。
/// </summary>
public partial class BuffDebuffCardWidgetView : UserControl
{
    public BuffDebuffCardWidgetView()
    {
        InitializeComponent();
    }
}
