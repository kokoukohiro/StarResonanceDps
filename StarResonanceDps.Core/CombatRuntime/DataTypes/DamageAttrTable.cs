namespace StarResonanceDps.Core.CombatRuntime.DataTypes
{
    public class DamageAttrTable
    {
        public Dictionary<string, DamageAttr> Data = [];
    }

    /// <summary>
    /// ダメージ算出の定義。<c>TypeEnum</c> が<b>そのダメージを出すスキルID</b>を指す。
    /// </summary>
    public class DamageAttr
    {
        public long Id { get; set; }
        public int TypeEnum { get; set; }
    }
}
