namespace StarResonanceDps.App.Models.Widgets;

/// <summary>ウィジェットの状態の印の色。値の大小が悪さの順。</summary>
public enum WidgetDataStatusLevel
{
    /// <summary>緑。欠けたと分かる事実が無い(既定)。</summary>
    Complete,

    /// <summary>黄。マップ移動やログインし直す以外の操作で直る欠けがある。</summary>
    Partial,

    /// <summary>赤。マップ移動やログインし直さないと直らない、または直らない欠けがある。</summary>
    Unavailable
}

/// <summary>
/// 欠けの理由。理由ごとに、直し方(利用者がゲームの中ですること)と色が決まっている(TIPS の文は <see cref="WidgetDataStatusText"/>)。
/// 赤は <see cref="MapOrRelogin"/> と <see cref="Unreadable"/>、ほかは黄。
/// </summary>
[Flags]
public enum WidgetDataStatusReasons
{
    None = 0,

    /// <summary>マップ移動やログインし直すまで直らない(途中起動・まとめの差分の付け直しが無かったマップ)。赤。</summary>
    MapOrRelogin = 1,

    /// <summary>パーティ画面を開くと直る(パーティの顔ぶれ・助っ人・近くにいない仲間の現在地)。黄。</summary>
    PartyScreen = 2,

    /// <summary>該当プレイヤーをクリックすると直る(写真)。黄。</summary>
    PlayerClick = 4,

    /// <summary>該当プレイヤーに近づき直すと直る(起動前から近くにいた人・一度も近くで見ていない人・今近くにいない人)。黄。</summary>
    PlayerApproach = 8,

    /// <summary>該当の敵に近づき直すと直る(起動前から近くにいた敵・今近くにいない敵)。黄。</summary>
    EnemyApproach = 16,

    /// <summary>データを読めない(装備情報・履歴の回の読み込み)。直らないので赤。</summary>
    Unreadable = 32
}

/// <summary>ウィジェットの状態。欠けの理由の組で、色は理由から決まる。値で比べられるので、同じなら通知しない。</summary>
public readonly record struct WidgetDataStatus(WidgetDataStatusReasons Reasons)
{
    /// <summary>赤になる理由。</summary>
    private const WidgetDataStatusReasons UnavailableReasons = WidgetDataStatusReasons.MapOrRelogin | WidgetDataStatusReasons.Unreadable;

    public static WidgetDataStatus Complete => default;

    public WidgetDataStatusLevel Level => (Reasons & UnavailableReasons) != WidgetDataStatusReasons.None
        ? WidgetDataStatusLevel.Unavailable
        : Reasons != WidgetDataStatusReasons.None
            ? WidgetDataStatusLevel.Partial
            : WidgetDataStatusLevel.Complete;

    /// <summary>理由を足す。</summary>
    public WidgetDataStatus With(WidgetDataStatusReasons reason) => new(Reasons | reason);

    /// <summary>条件が真なら理由を足す。</summary>
    public WidgetDataStatus WithIf(bool condition, WidgetDataStatusReasons reason) => condition ? With(reason) : this;

    /// <summary>ほかの状態の理由を全部足す。</summary>
    public WidgetDataStatus Combine(WidgetDataStatus other) => new(Reasons | other.Reasons);
}
