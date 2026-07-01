namespace StarResonanceDps.Core.Models;

public enum PlayerClassSpec
{
    Unknown = 0,
    ShieldKnightRecovery,
    ShieldKnightShield,
    HeavyGuardianEarthfort,
    HeavyGuardianBlock,
    StormbladeIaidoSlash,
    StormbladeMoonStrike,
    WindKnightVanGuard,
    WindKnightSkyward,
    FrostMageIcicle,
    FrostMageFrostBeam,
    MarksmanWildpack,
    MarksmanFalconry,
    VerdantOracleSmite,
    VerdantOracleLifeBind,
    SoulMusicianDissonance,
    SoulMusicianConcerto,
    FlameBerserkerFormless,
    FlameBerserkerCrimson
}

public static class PlayerClassSpecResolver
{
    public static PlayerClassSpec FromSkillId(int skillId)
    {
        return skillId switch
        {
            1714 or 1734 => PlayerClassSpec.StormbladeIaidoSlash,
            1715 or 1740 or 1741 or 44701 or 179906 => PlayerClassSpec.StormbladeMoonStrike,
            120901 or 120902 => PlayerClassSpec.FrostMageIcicle,
            1241 => PlayerClassSpec.FrostMageFrostBeam,
            35107 or 35108 or 35109 or 1605 or 160102 => PlayerClassSpec.FlameBerserkerFormless,
            1606 or 1621 or 1622 or 1623 => PlayerClassSpec.FlameBerserkerCrimson,
            1405 or 1418 => PlayerClassSpec.WindKnightVanGuard,
            1419 => PlayerClassSpec.WindKnightSkyward,
            1518 or 1541 or 21402 => PlayerClassSpec.VerdantOracleSmite,
            20301 => PlayerClassSpec.VerdantOracleLifeBind,
            199902 => PlayerClassSpec.HeavyGuardianEarthfort,
            1930 or 1931 or 1934 or 1935 => PlayerClassSpec.HeavyGuardianBlock,
            2292 or 1700820 or 1700825 or 1700827 => PlayerClassSpec.MarksmanWildpack,
            220112 or 2203622 or 220106 => PlayerClassSpec.MarksmanFalconry,
            2405 => PlayerClassSpec.ShieldKnightRecovery,
            2406 => PlayerClassSpec.ShieldKnightShield,
            2306 or 2321 or 2335 => PlayerClassSpec.SoulMusicianDissonance,
            2301 or 2307 or 2336 or 2361 or 55302 => PlayerClassSpec.SoulMusicianConcerto,
            _ => PlayerClassSpec.Unknown
        };
    }
}
