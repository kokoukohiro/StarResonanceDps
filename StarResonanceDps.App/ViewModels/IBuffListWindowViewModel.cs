using CommunityToolkit.Mvvm.Input;
using StarResonanceDps.App.Models.Widgets;

namespace StarResonanceDps.App.ViewModels;

/// <summary>
/// バフ/デバフ一覧の窓のViewModel。プレイヤーから開いたものと実体から開いたものの2種類があり、
/// 同じビュー(<see cref="Views.Widgets.PlayerBuffListWidgetView"/>)の行の右クリックのメニューが使う。
/// </summary>
public interface IBuffListWindowViewModel
{
    /// <summary>メニューの色を引くウィジェット。</summary>
    WidgetListItemViewModel Widget { get; }

    /// <summary>メニューの1つ目の項目。バフ一覧なら「バフカード」、デバフ一覧なら「デバフカード」。</summary>
    string CardMenuText { get; }

    /// <summary>メニューの2つ目の項目。バフ一覧なら「このバフを非表示」、デバフ一覧なら「このデバフを非表示」。</summary>
    string HideMenuText { get; }

    /// <summary>行のバフ・デバフカードを開く。行の左クリックと同じ。</summary>
    IRelayCommand<PlayerBuffEntry?> OpenCardCommand { get; }

    /// <summary>行のバフをウィジェットの非表示の一覧に足す。</summary>
    IRelayCommand<PlayerBuffEntry?> HideBuffCommand { get; }
}
