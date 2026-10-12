namespace StarResonanceDps.Core.CombatRuntime;

/// <summary>
/// モジュール効果のバフの、バフ/デバフリストでの出し方(<see cref="BuffDisplayRules.GetModuleBuffDisplay"/>)。
/// 決めるのは手修正 <c>Data/Overrides/ModuleBuffOverrides.json</c>。書かれていないモジュール効果のバフは <see cref="Shown"/>。
/// </summary>
public enum ModuleBuffDisplay
{
    /// <summary>モジュール効果のバフではない。</summary>
    None = 0,

    /// <summary>出す。リストの設定「モジュール効果」が「非表示」のときだけ隠れる。</summary>
    Shown = 1,

    /// <summary>付いている間ずっと変わらない(常駐)。設定が「すべて表示」のときだけ出る。</summary>
    Resident = 2,

    /// <summary>どの設定でも出さない(ほかのバフと同じことを示す)。</summary>
    Hidden = 3,
}
