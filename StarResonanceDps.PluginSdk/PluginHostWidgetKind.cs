namespace StarResonanceDps.PluginSdk;

/// <summary>
/// A widget of the host, opened with <see cref="IPluginWidgetService"/>.
/// The values do not change between SDK versions; widgets the host adds later get new values.
/// </summary>
public enum PluginHostWidgetKind
{
    /// <summary>DPS Meter.</summary>
    DpsMeter = 0,

    /// <summary>HPS Meter.</summary>
    HpsMeter = 1,

    /// <summary>Timeline.</summary>
    Timeline = 3,

    /// <summary>Player List.</summary>
    PlayerList = 4,

    /// <summary>Player Info.</summary>
    PlayerInfo = 5,

    /// <summary>Status. Shows only the user.</summary>
    PlayerStatus = 6,

    /// <summary>Equipment Info.</summary>
    PlayerEquipment = 7,

    /// <summary>Buff List.</summary>
    BuffList = 8,

    /// <summary>Debuff List.</summary>
    DebuffList = 9,

    /// <summary>Skill Details (Damage).</summary>
    DamageSkillDetail = 10,

    /// <summary>DPS Trend Graph.</summary>
    DpsGraph = 11,

    /// <summary>Skill Details (Healing).</summary>
    HealingSkillDetail = 12,

    /// <summary>HPS Trend Graph.</summary>
    HpsGraph = 13,

    /// <summary>Damage Details.</summary>
    DamageSummary = 14,

    /// <summary>Healing Details.</summary>
    HealingSummary = 15,

    /// <summary>Entity List.</summary>
    EntityList = 18,

    /// <summary>Talent Info.</summary>
    PlayerTalent = 20,

    /// <summary>Deep-Slumber Psychoscope Info.</summary>
    PlayerSeasonTalent = 21,

    /// <summary>Module Info.</summary>
    PlayerModule = 22,

    /// <summary>Buff &amp; Debuff Group. Shows only the user.</summary>
    BuffDebuffGroup = 23,

    /// <summary>Revive List.</summary>
    ReviveList = 24,

    /// <summary>Damage Taken Log.</summary>
    DamageTakenLog = 25,

    /// <summary>Damage Taken Details.</summary>
    DamageTakenSummary = 26,

    /// <summary>Target Info.</summary>
    TargetInfo = 27,

    /// <summary>Threat Meter.</summary>
    HateMeter = 30,

    /// <summary>Break Meter.</summary>
    BreakMeter = 31,

    /// <summary>Skill Details (Threat).</summary>
    HateSkillDetail = 32,

    /// <summary>Skill Details (Break).</summary>
    BreakSkillDetail = 33,

    /// <summary>TPS Trend Graph.</summary>
    HateGraph = 34,

    /// <summary>BPS Trend Graph.</summary>
    BreakGraph = 35,

    /// <summary>Map.</summary>
    Map = 36,

    /// <summary>Buff Uptime.</summary>
    BuffUptime = 37,
}
