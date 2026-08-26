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
    FlameBerserkerCrimson,

    /// <summary>
    /// 特化アビリティ未装着。ゲーム内の呼称は「クラスR1」(TalentStage 0)。
    ///
    /// <para>
    /// <see cref="Unknown"/> とは別物。Unknown は「まだ観測できていない」で、
    /// これは「全バフスナップショットを受け取った上でマーカーが無かった」= 確定した未装着。
    /// </para>
    /// </summary>
    Rank1
}

public static class PlayerClassSpecResolver
{
    /// <summary>
    /// 表示用の特化を解決する。特化の判定は Core で完結させ、ウィジェット側で組み立てない。
    ///
    /// <para>
    /// <see cref="PlayerClassSpec.Rank1"/>(アビリティ未装着)は <c>SubProfessionId</c> だけからは
    /// 導けない。0 は「未装着」と「まだ観測していない」の両方を意味するため、
    /// 未装着が確定したかどうかのフラグと組にして初めて区別できる。
    /// </para>
    /// </summary>
    public static PlayerClassSpec Resolve(int subProfessionId, bool isSpecAbilityUnequipped)
    {
        if (subProfessionId > 0)
        {
            return FromSubProfessionId(subProfessionId);
        }

        return isSpecAbilityUnequipped ? PlayerClassSpec.Rank1 : PlayerClassSpec.Unknown;
    }

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
