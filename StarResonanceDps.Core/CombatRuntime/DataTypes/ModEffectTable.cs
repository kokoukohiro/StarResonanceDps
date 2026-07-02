namespace StarResonanceDps.Core.CombatRuntime.DataTypes
{
    public class ModEffectTable
    {
        public Dictionary<int, EffectData> Data = [];
    }

    public class EffectData
    {
        public int Id { get; set; }
        public int EffectID { get; set; }
        public string EffectName { get; set; } = null!;
        public int Level { get; set; }
        public string EffectConfigIcon { get; set; } = null!;
        public List<List<int>> EffectConfig { get; set; } = null!;
        public List<object> EffectKey { get; set; } = null!;
        public List<object> EffectValue { get; set; } = null!;
        public int EnhancementNum { get; set; }
        public int PlayerLevel { get; set; }
        public string EffectOverview { get; set; } = null!;
        public int EffectType { get; set; }
        public bool IsNegative { get; set; }
        public int FightValue { get; set; }
        public bool IsShowShield { get; set; }
    }
}
