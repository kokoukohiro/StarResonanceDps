using System.Windows.Media;
using CommunityToolkit.Mvvm.ComponentModel;

namespace StarResonanceDps.App.Models.Widgets;

/// <summary>
/// 蘇生リストの1行(戦闘不能の人1人)。行は作り直さず、値だけ入れ替える(<see cref="ViewModels.ReviveListWidgetViewModel"/>)。
/// クラスアイコン・特化の文字・PT番号・名前の作りはプレイヤーリストの行と同じ。
/// </summary>
public sealed partial class ReviveListEntry : ObservableObject
{
    public ReviveListEntry(long characterId)
    {
        CharacterId = characterId;
    }

    public long CharacterId { get; }

    /// <summary>自分の行か。右クリックのメニューの「ステータス詳細」は自分の行だけに出す(プレイヤーリストと同じ)。</summary>
    [ObservableProperty]
    private bool _isSelf;

    /// <summary>この行の右クリックのメニューを開いているか。開いている間は行のホバーの背景を残す。下ろすとメニューを閉じる(画面のコード)。</summary>
    [ObservableProperty]
    private bool _isPlayerSelectionMenuOpen;

    /// <summary>クラスアイコンの形。鍵(職業)ごとに使い回す。</summary>
    [ObservableProperty]
    private Brush? _classIconMask;

    /// <summary>クラスアイコンの色(設定「アイコンカラー」)。色ごとに使い回す。</summary>
    [ObservableProperty]
    private Brush? _classBrush;

    /// <summary>クラスアイコンの下の特化の文字。</summary>
    [ObservableProperty]
    private string _classSpecDisplayName = string.Empty;

    [ObservableProperty]
    private bool _isPartyMember;

    /// <summary>PT番号のバッジの文字。番号が分からなければ「？」(プレイヤーリストと同じ)。</summary>
    [ObservableProperty]
    private string _partyNumberText = string.Empty;

    /// <summary>名前。書式は設定「プレイヤー名」(プレイヤーリストと同じ)。</summary>
    [ObservableProperty]
    private string _displayName = string.Empty;

    /// <summary>自分の行を強調表示するか。設定「自分の強調表示」がオンのときの自分の行だけ true。</summary>
    [ObservableProperty]
    private bool _isSelfHighlighted;

    /// <summary>右寄せの文字。蘇生不可の残り秒、蘇生可能、分からなければ「??秒」。</summary>
    [ObservableProperty]
    private string _stateText = string.Empty;

    /// <summary>蘇生可能か。右の文字をテキストカラー「蘇生可能」の色にする。</summary>
    [ObservableProperty]
    private bool _isRevivable;

    /// <summary>テキストカラー「蘇生可能」の色。全部の行で同じブラシ。</summary>
    [ObservableProperty]
    private Brush? _revivableBrush;

    /// <summary>並びの鍵。蘇生可能は 0、残り秒の分かる行は残り秒、分からない行は最後(正の無限大)。</summary>
    internal double SortKey { get; set; }
}
