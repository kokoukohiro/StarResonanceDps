namespace StarResonanceDps.Core.CombatRuntime.DataTypes
{
    /// <summary>モジュール効果の表(<c>Data/Raw/ModEffectTable.json</c>)。行は効果の段階ごと。</summary>
    public class ModEffectTable
    {
        public Dictionary<string, ModEffect> Data = [];
    }

    public class ModEffect
    {
        public int Id { get; set; }

        /// <summary>モジュール効果の番号。同じ効果の段階ごとの行が同じ値を持つ。名前は <c>ModEffectNames.json</c> をこの番号で引く。</summary>
        public int EffectID { get; set; }

        public int Level { get; set; }

        /// <summary>段階の効果。<c>[3, バフID, 1]</c> はモジュールを付けている間ずっと付くバフ(モジュールの根のバフ)。</summary>
        public List<List<int>> EffectConfig { get; set; } = [];
    }
}
