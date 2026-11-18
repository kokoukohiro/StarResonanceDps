namespace StarResonanceDps.PluginSdk;

/// <summary>The element of damage. Values are the game's numbers; a number not listed here is passed as it is.</summary>
public enum PluginDamageElement
{
    /// <summary>No element.</summary>
    General = 0,

    /// <summary>Fire.</summary>
    Fire = 1,

    /// <summary>Water.</summary>
    Water = 2,

    /// <summary>Electricity.</summary>
    Electricity = 3,

    /// <summary>Wood.</summary>
    Wood = 4,

    /// <summary>Wind.</summary>
    Wind = 5,

    /// <summary>Rock.</summary>
    Rock = 6,

    /// <summary>Light.</summary>
    Light = 7,

    /// <summary>Dark.</summary>
    Dark = 8
}

/// <summary>Whether damage is physical or magical. Values are the game's numbers.</summary>
public enum PluginDamageMode
{
    /// <summary>Neither physical nor magical.</summary>
    Normal = 0,

    /// <summary>Physical.</summary>
    Physical = 1,

    /// <summary>Magical.</summary>
    Magical = 2
}

/// <summary>What damage or healing came from. Values are the game's numbers.</summary>
public enum PluginDamageSource
{
    /// <summary>A skill; the source ID is a skill ID.</summary>
    Skill = 0,

    /// <summary>A projectile; the source ID is a projectile ID.</summary>
    Bullet = 1,

    /// <summary>A buff; the source ID is a buff ID.</summary>
    Buff = 2,

    /// <summary>A fall.</summary>
    Fall = 3,

    /// <summary>The game's <c>FakeBullet</c>; the source ID is a projectile ID.</summary>
    FakeBullet = 4,

    /// <summary>Anything else.</summary>
    Other = 100
}

/// <summary>What applied a buff. Values and names are the game's (<c>EFightSource</c>); a number not listed here is passed as it is.</summary>
public enum PluginFightSourceType
{
    /// <summary>The game's <c>EFightSourceSkill</c>.</summary>
    Skill = 0,

    /// <summary>The game's <c>EFightSourceBuff</c>.</summary>
    Buff = 1,

    /// <summary>The game's <c>EFightSourceBullet</c>.</summary>
    Bullet = 2,

    /// <summary>The game's <c>EFightSourceTask</c>.</summary>
    Task = 4,

    /// <summary>The game's <c>EFightSourceTalent</c>.</summary>
    Talent = 6,

    /// <summary>The game's <c>EFightSourceSeasonMedal</c>.</summary>
    SeasonMedal = 7,

    /// <summary>The game's <c>EFightSourceUnionEffect</c>.</summary>
    UnionEffect = 8,

    /// <summary>The game's <c>EFightSourceMod</c>.</summary>
    Mod = 9,

    /// <summary>The game's <c>EFightSourceEquip</c>.</summary>
    Equip = 10,

    /// <summary>The game's <c>EFightSourceEquipSlotRefine</c>.</summary>
    EquipSlotRefine = 11,

    /// <summary>The game's <c>EFightSourceVehicle</c>.</summary>
    Vehicle = 12,

    /// <summary>The game's <c>EFightSourceSeasonTalent</c>.</summary>
    SeasonTalent = 13,

    /// <summary>The game's <c>EFightSourceSceneBegin</c>.</summary>
    SceneBegin = 1000,

    /// <summary>The game's <c>EFightSourceScene</c>.</summary>
    Scene = 1001,

    /// <summary>The game's <c>EFightSourceAffix</c>.</summary>
    Affix = 1002,

    /// <summary>The game's <c>EFightSourceOther</c>.</summary>
    Other = 10000
}
