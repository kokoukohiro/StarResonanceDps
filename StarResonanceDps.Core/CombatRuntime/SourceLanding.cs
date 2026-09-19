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
}

/// <summary>着地先。<paramref name="Id"/> はオプションのバフIDか技ID。</summary>
public readonly record struct SourceLanding(SourceLandingKind Kind, int Id)
{
    public static SourceLanding None => default;

    public override string ToString() => Kind switch
    {
        SourceLandingKind.RogueEntry => $"オプション {Id}",
        SourceLandingKind.Skill => $"技 {Id}",
        _ => "なし"
    };
}
