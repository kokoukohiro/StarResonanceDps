using CommunityToolkit.Mvvm.ComponentModel;
using StarResonanceDps.App.Services;
using StarResonanceDps.Core.CombatRuntime;
using StarResonanceDps.App.Localization;

namespace StarResonanceDps.App.Models.Widgets;

public sealed partial class PlayerBuffEntry : ObservableObject
{
    /// <summary>
    /// 残り時間が分からないときにバッジへ出す文字。
    /// <b>アイコンに重ねる1文字の枠</b>なので記号1つ。4言語とも同じなのでリソースは持たない。
    /// </summary>
    private const string UnknownDurationBadgeText = "?";

    /// <summary>
    /// 一覧の列で数字の位置に差し込む文字。単位は <c>Widget_BuffDurationFormat</c> のものが付く。
    /// </summary>
    private const string UnknownDurationListText = "??";

    public PlayerBuffEntry(PlayerBuffSnapshot snapshot)
    {
        Key = snapshot.Key;
        Uuid = snapshot.Uuid;
        Update(snapshot);
    }

    public string Key { get; private set; }

    public long Uuid { get; private set; }

    /// <summary>バフのID。まとまり(料理・薬剤)の判定に要る。</summary>
    public int BaseId { get; private set; }

    [ObservableProperty]
    private string _name = string.Empty;

    [ObservableProperty]
    private string _layerText = string.Empty;

    [ObservableProperty]
    private string _durationText = string.Empty;

    [ObservableProperty]
    private string _durationWithUnitText = string.Empty;

    [ObservableProperty]
    private string? _iconPath;

    /// <summary>行のゲージの長さ(0〜1)。満タンになる残り時間は表示設定で決まる。</summary>
    [ObservableProperty]
    private double _barRatio = 1d;

    /// <summary>ゲージが満タンか。満タンのときだけ右端も角丸にする。</summary>
    [ObservableProperty]
    private bool _isBarFull = true;

    /// <summary>
    /// ゲージが 0 になるまでの秒数。表示側はこのぶんかけて線形に縮める。
    /// <b>0 は「今は減らない」</b>(満タンで頭打ちの間と、減らないバフ)。
    /// </summary>
    [ObservableProperty]
    private double _barDecaySeconds;

    /// <summary>残り時間をそのまま持つ。ゲージの長さの設定が変わったら比率を計算し直すのに要る。</summary>
    private double? _remainingSeconds;

    private bool _isRemainingUnknown;

    public void Update(PlayerBuffSnapshot snapshot)
    {
        Key = snapshot.Key;
        Uuid = snapshot.Uuid;
        BaseId = snapshot.BaseId;
        Name = snapshot.Name ?? string.Empty;
        // 1スタックは数字を出さない。重なっているときだけ意味がある。
        LayerText = snapshot.Layer > 1 ? snapshot.Layer.ToString() : string.Empty;
        DurationText = FormatDuration(snapshot.RemainingSeconds, snapshot.IsRemainingUnknown);
        DurationWithUnitText = FormatDurationWithUnit(snapshot.RemainingSeconds, snapshot.IsRemainingUnknown);
        IconPath = CombatIconResolver.ResolveBuffIcon(snapshot.IconName);

        _remainingSeconds = snapshot.RemainingSeconds;
        _isRemainingUnknown = snapshot.IsRemainingUnknown;
    }

    /// <summary>
    /// ゲージの長さを計算し直す。
    ///
    /// <para>
    /// <b>残り時間が分からないバフと持続時間の無いバフは、ずっと満タン</b>(ユーザー決定)。
    /// 減っていかないものを短いゲージで出すと、消えかけに見えるため。
    /// </para>
    /// </summary>
    public void UpdateBar(int gaugeLengthSeconds)
    {
        double ratio;
        double decaySeconds;
        if (_isRemainingUnknown || _remainingSeconds is not { } seconds || gaugeLengthSeconds <= 0)
        {
            ratio = 1d;
            decaySeconds = 0d;
        }
        else
        {
            ratio = Math.Clamp(seconds / gaugeLengthSeconds, 0d, 1d);

            // 満タンで頭打ちの間はまだ縮まない。縮み始めるのは残りがゲージの長さを切ってから。
            decaySeconds = seconds <= gaugeLengthSeconds ? Math.Max(seconds, 0d) : 0d;
        }

        BarRatio = ratio;
        IsBarFull = ratio >= 1d;
        BarDecaySeconds = decaySeconds;
    }

    /// <summary>
    /// 残り時間の数字だけ。<b>単位を含めない。</b>
    ///
    /// <para>
    /// プレイヤーリストのスキル枠バッジはアイコンに数字を重ねる設計で、単位が入る余地がない。
    /// 単位付きが要る一覧側は <see cref="DurationWithUnitText"/> を見る。
    /// </para>
    /// </summary>
    private static string FormatDuration(double? seconds, bool isUnknown)
    {
        if (isUnknown)
        {
            // アイコンに重ねる枠なので1文字。記号なので4言語とも同じで、リソースは持たない。
            return UnknownDurationBadgeText;
        }

        var roundedSeconds = RoundSeconds(seconds);

        return roundedSeconds is null
            ? string.Empty
            : roundedSeconds.Value.ToString(System.Globalization.CultureInfo.CurrentCulture);
    }

    /// <summary>
    /// 残り時間を<b>単位まで含めた1つの文字列</b>にしたもの。バフ/デバフ一覧の列で使う。
    ///
    /// <para>
    /// 数字と単位を別要素にすると、固定幅の中で右端を揃えるのが2要素の合成になる。
    /// 1つの文字列にしておけば <c>TextAlignment="Right"</c> だけで揃い、
    /// 持続時間が分からないバフ(空文字)では単位も自然に消える。
    /// </para>
    /// </summary>
    private static string FormatDurationWithUnit(double? seconds, bool isUnknown)
    {
        // 数字の位置に「??」を差し込む。単位は既存フォーマットのものが付くので、
        // en は ??s、ja/zh は ??秒、ko は ??초 になる。リソースの追加は要らない。
        object value;
        if (isUnknown)
        {
            value = UnknownDurationListText;
        }
        else
        {
            var roundedSeconds = RoundSeconds(seconds);
            if (roundedSeconds is null)
            {
                return string.Empty;
            }

            value = roundedSeconds.Value;
        }

        return string.Format(
            System.Globalization.CultureInfo.CurrentCulture,
            LocalizationManager.Instance.GetString("Widget_BuffDurationFormat"),
            value);
    }

    private static int? RoundSeconds(double? seconds)
    {
        return seconds is null
            ? null
            : Math.Max(0, (int)Math.Ceiling(seconds.Value));
    }


}
