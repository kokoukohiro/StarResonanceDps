namespace StarResonanceDps.App.Models.Widgets;

public enum WidgetKind
{
    DpsMeter,
    HpsMeter,
    DtpsMeter,
    TakenDamageLog,
    PlayerList,
    PlayerInfo,
    PlayerStatus,
    PlayerEquipment,
    BuffList,
    DebuffList,
    DamageContribution,
    DpsGraph,
    HealingContribution,
    HpsGraph,
    DamageSummary,
    HealingSummary,
    EntityList = 18,
    BuffDebuffCard = 19,

    /// <summary>アビリティ情報。プレイヤーのタレント(根のバフ)の一覧。</summary>
    PlayerTalent = 20,

    /// <summary>潜在心相晶情報。プレイヤーの心相晶のノード・上級と因子(根のバフ)の一覧。</summary>
    PlayerSeasonTalent = 21,

    /// <summary>モジュール情報。プレイヤーのモジュール効果(根のバフ)の一覧。</summary>
    PlayerModule = 22,

    /// <summary>バフ・デバフグループ。自分のバフ・デバフのうち、設定で選んだものだけの一覧。</summary>
    BuffDebuffGroup = 23,

    /// <summary>蘇生リスト。戦闘不能のプレイヤーと、蘇生不可デバフの残り時間の一覧。</summary>
    ReviveList = 24,

    /// <summary>被ダメリスト。被弾したプレイヤーと、その人の最後の被弾の一覧。</summary>
    DamageTakenList = 25,

    /// <summary>被ダメ詳細。プレイヤーの被ダメの合計・通常・会心・幸運と属性ごとの割合。</summary>
    DamageTakenSummary = 26
}
