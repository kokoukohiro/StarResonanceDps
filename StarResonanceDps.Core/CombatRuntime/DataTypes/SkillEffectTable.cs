namespace StarResonanceDps.Core.CombatRuntime.DataTypes
{
    public class SkillEffectTable
    {
        public Dictionary<string, SkillEffect> Data = [];
    }

    /// <summary>
    /// スキルの効果。<c>SkillTable.EffectIDs</c> から引く。
    ///
    /// <para>
    /// <c>SkillAttrDes</c> は表示用の計算式で、<c>{*skillpara.damageMerge({120024400102},...)*}</c>
    /// のように <b><c>DamageAttrTable</c> のIDが埋め込まれている</b>。
    /// イマジンからアクティブスキルを辿る唯一の手掛かり。
    /// </para>
    /// </summary>
    public class SkillEffect
    {
        public int Id { get; set; }
        public int SkillId { get; set; }
        public List<List<string>> SkillAttrDes { get; set; } = null!;
    }
}
