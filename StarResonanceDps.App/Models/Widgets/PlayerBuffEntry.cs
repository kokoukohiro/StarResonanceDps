using CommunityToolkit.Mvvm.ComponentModel;
using StarResonanceDps.App.Services;
using StarResonanceDps.Core.CombatRuntime;
using StarResonanceDps.App.Localization;

namespace StarResonanceDps.App.Models.Widgets;

public sealed partial class PlayerBuffEntry : ObservableObject
{
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

    public void Update(PlayerBuffSnapshot snapshot)
    {
        Key = snapshot.Key;
        Uuid = snapshot.Uuid;
        BaseId = snapshot.BaseId;
        Name = snapshot.Name ?? string.Empty;
        // 1スタックは数字を出さない。重なっているときだけ意味がある。
        LayerText = snapshot.Layer > 1 ? snapshot.Layer.ToString() : string.Empty;
        DurationText = FormatDuration(snapshot.RemainingSeconds);
        DurationWithUnitText = FormatDurationWithUnit(snapshot.RemainingSeconds);
        IconPath = CombatIconResolver.ResolveBuffIcon(snapshot.IconName);
    }

    /// <summary>
    /// 残り時間の数字だけ。<b>単位を含めない。</b>
    ///
    /// <para>
    /// プレイヤーリストのスキル枠バッジはアイコンに数字を重ねる設計で、単位が入る余地がない。
    /// 単位付きが要る一覧側は <see cref="DurationWithUnitText"/> を見る。
    /// </para>
    /// </summary>
    private static string FormatDuration(double? seconds)
    {
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
    private static string FormatDurationWithUnit(double? seconds)
    {
        var roundedSeconds = RoundSeconds(seconds);

        if (roundedSeconds is null)
        {
            return string.Empty;
        }

        return string.Format(
            System.Globalization.CultureInfo.CurrentCulture,
            LocalizationManager.Instance.GetString("Widget_BuffDurationFormat"),
            roundedSeconds.Value);
    }

    private static int? RoundSeconds(double? seconds)
    {
        return seconds is null
            ? null
            : Math.Max(0, (int)Math.Ceiling(seconds.Value));
    }


}
