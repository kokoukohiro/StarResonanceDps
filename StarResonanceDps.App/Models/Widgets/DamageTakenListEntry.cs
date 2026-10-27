using System.Windows.Media;
using CommunityToolkit.Mvvm.ComponentModel;

namespace StarResonanceDps.App.Models.Widgets;

/// <summary>
/// 被ダメリストの1行(被弾したプレイヤー1人)。行は作り直さず、値だけ入れ替える(<see cref="ViewModels.DamageTakenListWidgetViewModel"/>)。
/// 作りはメーターの行と同じで、左に経過・クラスアイコン(設定「アイコンカラー」で染める)・名前、右にその人の最後の被弾(スキル名・被ダメ値・属性アイコン)、下敷きに右から伸びるバー。
/// </summary>
public sealed partial class DamageTakenListEntry : ObservableObject
{
    public DamageTakenListEntry(long characterId)
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

    /// <summary>名前。書式は設定「プレイヤー名」(プレイヤーリストと同じ)。</summary>
    [ObservableProperty]
    private string _displayName = string.Empty;

    /// <summary>自分の行を強調表示するか。設定「自分の強調表示」がオンのときの自分の行だけ true。</summary>
    [ObservableProperty]
    private bool _isSelfHighlighted;

    /// <summary>経過(「1秒前」)。メーターの順位の位置に出す。更新のたびに数え直す(同じ秒の間は同じ文字)。</summary>
    [ObservableProperty]
    private string _elapsedText = string.Empty;

    /// <summary>右の書式(設定「スキル名」)の文字。被弾か設定が変わったときだけ作り直す。</summary>
    [ObservableProperty]
    private string _hitInfoText = string.Empty;

    /// <summary>被ダメ値(タイムラインの被弾の行と同じ数字の書き方)。</summary>
    [ObservableProperty]
    private string _valueText = string.Empty;

    /// <summary>被ダメ値の後ろに赤い「(戦闘不能)」を出すか。その被弾で倒れた、かつ設定「戦闘不能の原因を表示」がオン。</summary>
    [ObservableProperty]
    private bool _isDefeatShown;

    /// <summary>
    /// 被弾の属性が分かるか。属性を保存していなかった頃の記録は分からないので、属性アイコン・バー・TIPS を出さない(スキル詳細と同じ)。
    /// </summary>
    [ObservableProperty]
    private bool _hasElement;

    /// <summary>属性アイコンの形。鍵(属性)ごとに使い回す。塗りは白。</summary>
    [ObservableProperty]
    private Brush? _elementIconMask;

    /// <summary>行の TIPS(属性名「火属性」)。</summary>
    [ObservableProperty]
    private string _elementToolTipText = string.Empty;

    /// <summary>バーの塗り(設定「属性カラー」のその属性の色に、フィルターと不透明度を掛けたもの)。属性ごとに使い回す。</summary>
    [ObservableProperty]
    private Brush? _barBrush;

    /// <summary>バーの長さ(0〜1)。一覧の中で被ダメ値が一番大きい人を 1 にする。</summary>
    [ObservableProperty]
    private double _barRatio;

    /// <summary>今出している被弾の被ダメ値。バーの長さの比べに使う。</summary>
    internal long HitValue { get; set; }

    /// <summary>今出している被弾の到着時刻(UTC)。経過の文字を数えるのに使う。</summary>
    internal DateTime HitTimestamp { get; set; }

    /// <summary>今出している被弾の通し番号。同じなら右の文字を作り直さない。</summary>
    internal long HitSequence { get; set; } = -1;

    /// <summary>右の文字を作ったときの設定・言語の版。違えば作り直す。</summary>
    internal int HitTextVersion { get; set; } = -1;

    /// <summary>並びの鍵。最後の被弾の通し番号で、大きい(新しい)行ほど上。</summary>
    internal long SortKey { get; set; }
}
