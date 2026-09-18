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
    Rank1,

    /// <summary>
    /// 変身クラス。職業ID 8 / 14 / 15 は特化(クラスR2)を持たないので、
    /// <c>SubProfessionId</c> ではなく職業IDから固定で決まる。
    ///
    /// <para>
    /// 根拠は <c>TalentStageTable</c> — この3クラスの行は全て <c>RootId=0</c> の「クラスR1」で、
    /// R2 のエントリが存在しない。名前は <c>ProfessionTable</c> に無く、
    /// <c>SkillAoyiTable.ResonanceObject</c> から引く。
    /// </para>
    /// </summary>
    TransformDorothy,
    TransformLucy,
    TransformNatsu,

    /// <summary>
    /// イマジン「絶技！ミーンに変身」(技 3941)で変身している間。
    ///
    /// <para>
    /// この変身では職業IDが値なし(0)で届き、変身クラスの職業IDが無い。
    /// 変身のバフ <see cref="PlayerClassSpecResolver.MeanTransformBuffId"/> が乗っている間だけこれになる。
    /// </para>
    /// </summary>
    TransformMean
}

public static class PlayerClassSpecResolver
{
    /// <summary>ミーンに変身している間だけ乗るバフ(变异蜂-变身)。</summary>
    public const int MeanTransformBuffId = 2110101;

    /// <summary>その実体にミーンの変身のバフが今乗っているか。外れるか時間切れになれば false。</summary>
    public static bool HasMeanTransformBuff(long entityUuid)
    {
        if (entityUuid == 0)
        {
            return false;
        }

        foreach (var buff in Services.ActiveBuffStore.Instance.GetActive(entityUuid))
        {
            if (buff.BaseId == MeanTransformBuffId)
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>変身している間の特化か(アイコンは変身のものにそろえる)。</summary>
    public static bool IsTransformation(PlayerClassSpec spec)
    {
        return spec is PlayerClassSpec.TransformDorothy
            or PlayerClassSpec.TransformLucy
            or PlayerClassSpec.TransformNatsu
            or PlayerClassSpec.TransformMean;
    }

    /// <summary>
    /// 表示用の特化を解決する。特化の判定は Core で完結させ、ウィジェット側で組み立てない。
    ///
    /// <para>
    /// <see cref="PlayerClassSpec.Rank1"/>(アビリティ未装着)は <c>SubProfessionId</c> だけからは
    /// 導けない。0 は「未装着」と「まだ観測していない」の両方を意味するため、
    /// 未装着が確定したかどうかのフラグと組にして初めて区別できる。
    /// </para>
    /// </summary>
    /// <param name="hasMeanTransformBuff">ミーンの変身のバフが乗っているか(<see cref="HasMeanTransformBuff"/>)。</param>
    public static PlayerClassSpec Resolve(
        int professionId,
        int subProfessionId,
        bool isSpecAbilityUnequipped,
        bool hasMeanTransformBuff)
    {
        // ミーンの変身は職業IDを持たないので、バフで決める。
        if (hasMeanTransformBuff)
        {
            return PlayerClassSpec.TransformMean;
        }

        // 変身クラスは特化を持たない。職業IDだけで決まり、SubProfessionId は見ない。
        if (TryResolveTransformation(professionId, out var transformation))
        {
            return transformation;
        }

        if (subProfessionId > 0)
        {
            return FromSubProfessionId(subProfessionId);
        }

        return isSpecAbilityUnequipped ? PlayerClassSpec.Rank1 : PlayerClassSpec.Unknown;
    }

    /// <summary>
    /// 変身クラス(特化を持たない職業)かどうか。
    /// <see cref="PlayerClassSpec.TransformDorothy"/> のコメントに一次データの根拠がある。
    /// </summary>
    public static bool TryResolveTransformation(int professionId, out PlayerClassSpec spec)
    {
        switch (professionId)
        {
            case 8:
                spec = PlayerClassSpec.TransformDorothy;
                return true;
            case 14:
                spec = PlayerClassSpec.TransformLucy;
                return true;
            case 15:
                spec = PlayerClassSpec.TransformNatsu;
                return true;
            default:
                spec = PlayerClassSpec.Unknown;
                return false;
        }
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
