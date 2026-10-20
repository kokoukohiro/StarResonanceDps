using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using StarResonanceDps.App.ViewModels;

namespace StarResonanceDps.App.Views.Widgets;

/// <summary>
/// バフ・デバフを1件だけ大きく出すカード。
/// プレイヤー用とモンスター用の2つのViewModelが同じこのビューを使う。
///
/// <para>
/// 右クリックのメニュー(「グループに追加」)は、自分のカードでグループに足せるときだけ開く
/// (<see cref="BuffDebuffCardWidgetViewModel.CanAddToGroup"/>)。開き方はバフ・デバフ一覧の行のメニューと同じ。
/// </para>
/// </summary>
public partial class BuffDebuffCardWidgetView : UserControl
{
    private const string CardContextMenuKey = "BuffDebuffCard.ContextMenu";

    public BuffDebuffCardWidgetView()
    {
        InitializeComponent();
    }

    private void Card_PreviewMouseRightButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (sender is not FrameworkElement target
            || DataContext is not BuffDebuffCardWidgetViewModel viewModel
            || !viewModel.CanAddToGroup()
            || TryFindResource(CardContextMenuKey) is not ContextMenu menu)
        {
            return;
        }

        menu.DataContext = viewModel;
        menu.PlacementTarget = target;
        menu.IsOpen = true;
        e.Handled = true;
    }

    private void BuffDebuffCardWidgetView_Unloaded(object sender, RoutedEventArgs e)
    {
        if (TryFindResource(CardContextMenuKey) is ContextMenu { IsOpen: true } menu)
        {
            menu.IsOpen = false;
        }
    }
}
