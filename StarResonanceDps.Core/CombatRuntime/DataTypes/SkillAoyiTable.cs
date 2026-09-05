namespace StarResonanceDps.Core.CombatRuntime.DataTypes
{
    public class SkillAoyiTable
    {
        public Dictionary<string, SkillAoyi> Data = [];
    }

    /// <summary>
    /// イマジン(奥義/絶技)の定義。
    ///
    /// <para>
    /// <c>TransformationType</c> は <c>[[3, buffId, 1], ...]</c> の形で、
    /// <b>そのイマジンが付与するパッシブバフ</b>を持つ。<c>TalentTable.TalentEffect</c> と同じ書式。
    /// ダメージ・回復がこのバフから出たとき、親イマジンを一意に決められる唯一の手掛かり。
    /// </para>
    /// </summary>
    public class SkillAoyi
    {
        public int Id { get; set; }
        public List<List<int>> TransformationType { get; set; } = null!;
    }
}
