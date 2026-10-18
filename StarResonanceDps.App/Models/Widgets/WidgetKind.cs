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
    PlayerModule = 22
}
