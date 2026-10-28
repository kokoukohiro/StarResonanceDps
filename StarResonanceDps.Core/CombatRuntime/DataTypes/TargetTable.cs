namespace StarResonanceDps.Core.CombatRuntime.DataTypes
{
    public class TargetTable
    {
        public Dictionary<string, TargetRow> Data = [];
    }

    public class TargetRow
    {
        public int Id { get; set; }
        public int TargetType { get; set; }
    }
}
