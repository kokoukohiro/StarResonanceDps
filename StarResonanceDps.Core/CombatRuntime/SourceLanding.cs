namespace StarResonanceDps.Core.CombatRuntime;

/// <summary>
/// 見出し表で名前を持たないメーターの行について、記録時に付与元をたどって着いた先の種類。
/// 名前はこの先から引く(<see cref="CombatDataCatalog.GetSourceDisplayName"/>)。
/// </summary>
public enum SourceLandingKind
{
    /// <summary>着いていない。</summary>
    None = 0,

    /// <summary>オプションのバフ(<c>RogueEntryTable.BuffId</c>)。名前は <c>RogueEntryNames</c>。</summary>
    RogueEntry = 1,

    /// <summary>プレイヤーが使った技。名前は <c>SkillNames</c>。</summary>
    Skill = 2,

    /// <summary>
    /// モジュール効果(<c>ModEffectTable.EffectID</c>)。名前は <c>ModEffectNames</c>。
    /// バフの由来(<c>SourceLandingResolver.ResolveBuffOrigin</c>)だけが着く。メーターの行は着かない。
    /// </summary>
    Module = 3,
}

/// <summary>着地先。<paramref name="Id"/> はオプションのバフID、技ID、モジュール効果の番号のどれか(<paramref name="Kind"/> で決まる)。</summary>
public readonly record struct SourceLanding(SourceLandingKind Kind, int Id)
{
    public static SourceLanding None => default;

    public override string ToString() => Kind switch
    {
        SourceLandingKind.RogueEntry => $"オプション {Id}",
        SourceLandingKind.Skill => $"技 {Id}",
        SourceLandingKind.Module => $"モジュール効果 {Id}",
        _ => "なし"
    };
}
