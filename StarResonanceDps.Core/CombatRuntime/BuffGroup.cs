namespace StarResonanceDps.Core.CombatRuntime;

/// <summary>
/// 個別に扱わず1つのまとまりとして見るバフの種類。
///
/// <para>
/// 料理も薬剤も、シーズン × 効果 × レベルの総当たりで数百件の別IDがあるが、
/// 同時に付くのは1つで、食べ直すたびに別のIDへ入れ替わる。ゲーム側の表示名も
/// 全部「料理」「薬剤」の1語に丸められている。個別のIDを追うと入れ替わりで見失うので、
/// <b>まとまりとして追う</b>。
/// </para>
/// </summary>
public enum BuffGroup
{
    None = 0,

    /// <summary>料理。<c>2032011</c>〜<c>2032284</c>。</summary>
    Cuisine = 1,

    /// <summary>薬剤。<c>2033011</c>〜<c>2033189</c>。</summary>
    Potion = 2
}
