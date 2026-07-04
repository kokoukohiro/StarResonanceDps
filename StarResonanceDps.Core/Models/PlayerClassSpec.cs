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
    public static PlayerClassSpec FromSubProfessionId(int subProfessionId)
    {
        return subProfessionId switch
        {
            01_00_01 => PlayerClassSpec.StormbladeIaidoSlash,
            01_00_02 => PlayerClassSpec.StormbladeMoonStrike,
            02_00_01 => PlayerClassSpec.FrostMageIcicle,
            02_00_02 => PlayerClassSpec.FrostMageFrostBeam,
            03_00_01 => PlayerClassSpec.FlameBerserkerFormless,
            03_00_02 => PlayerClassSpec.FlameBerserkerCrimson,
            04_00_01 => PlayerClassSpec.WindKnightVanGuard,
            04_00_02 => PlayerClassSpec.WindKnightSkyward,
            05_00_01 => PlayerClassSpec.VerdantOracleSmite,
            05_00_02 => PlayerClassSpec.VerdantOracleLifeBind,
            09_00_01 => PlayerClassSpec.HeavyGuardianEarthfort,
            09_00_02 => PlayerClassSpec.HeavyGuardianBlock,
            11_00_01 => PlayerClassSpec.MarksmanWildpack,
            11_00_02 => PlayerClassSpec.MarksmanFalconry,
            12_00_01 => PlayerClassSpec.ShieldKnightRecovery,
            12_00_02 => PlayerClassSpec.ShieldKnightShield,
            13_00_01 => PlayerClassSpec.SoulMusicianDissonance,
            13_00_02 => PlayerClassSpec.SoulMusicianConcerto,
            _ => PlayerClassSpec.Unknown
        };
    }
}
