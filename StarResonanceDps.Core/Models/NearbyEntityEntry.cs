using StarResonanceDps.Core.CombatRuntime;
using Zproto;
using MonsterClassification = StarResonanceDps.Core.CombatRuntime.EMonsterType;

namespace StarResonanceDps.Core.Models;

/// <param name="HasHpBar">
/// ゲーム内でプレイヤーに見える HP バーを持つ実体か。エンティティリストは、これが true なら名前が無くても載せる。
/// 表で判定できない実体(<c>AttrId</c> が未着・表に無い番号)は true。
/// </param>
/// <param name="IsInCombat">いま戦闘状態か(実体の属性 <c>AttrCombatState</c>、104)。出現時の一覧と周りの差分で届く。</param>
/// <param name="TargetCharacterId">
/// 今狙っている相手のキャラ番号(実体の属性 <c>AttrTargetUuid</c>、450)。相手がプレイヤーでない・いないときは 0。
/// ヘイトの1位とは限らない。
/// </param>
/// <param name="HateRanking">ヘイトの一覧(実体の属性 <c>AttrHateList</c>、474)のプレイヤーだけを、値の大きい順に4人まで。</param>
public sealed record NearbyEntityEntry(
    long EntityUuid,
    long EntityId,
    string Name,
    EEntityType EntityType,
    MonsterClassification MonsterType,
    int Level,
    long CurrentHp,
    long MaxHp,
    int CurrentBreak,
    int MaxBreak,
    bool IsInvulnerable,
    bool IsBreakLocked,
    EntityCampRelation CampRelation,
    long CurrentShield,
    bool HasHpBar,
    bool IsInCombat,
    long TargetCharacterId,
    NearbyEntityHateRanking HateRanking);

/// <summary>ヘイトの一覧の1人。</summary>
public readonly record struct NearbyEntityHateRank(long CharacterId, long HateValue);

/// <summary>
/// ヘイトの一覧の上位(プレイヤーだけ、値の大きい順、<see cref="MaxCount"/> 人まで)。
/// 中身が同じなら等しい(行の値が変わらないときに通知を出さないため)。
/// </summary>
public sealed class NearbyEntityHateRanking : IEquatable<NearbyEntityHateRanking>
{
    public const int MaxCount = 4;

    private readonly NearbyEntityHateRank[] _ranks;

    private NearbyEntityHateRanking(NearbyEntityHateRank[] ranks)
    {
        _ranks = ranks;
    }

    public static NearbyEntityHateRanking Empty { get; } = new([]);

    public int Count => _ranks.Length;

    public NearbyEntityHateRank this[int index] => _ranks[index];

    /// <param name="ranks">値の大きい順に並べた、プレイヤーだけの上位。<see cref="MaxCount"/> 人より多ければ後ろを捨てる。</param>
    public static NearbyEntityHateRanking Create(IEnumerable<NearbyEntityHateRank> ranks)
    {
        ArgumentNullException.ThrowIfNull(ranks);

        var items = ranks.Take(MaxCount).ToArray();
        return items.Length == 0 ? Empty : new NearbyEntityHateRanking(items);
    }

    public bool Equals(NearbyEntityHateRanking? other)
    {
        if (ReferenceEquals(this, other))
        {
            return true;
        }

        return other is not null && _ranks.AsSpan().SequenceEqual(other._ranks);
    }

    public override bool Equals(object? obj)
    {
        return obj is NearbyEntityHateRanking other && Equals(other);
    }

    public override int GetHashCode()
    {
        var hash = new HashCode();
        foreach (var rank in _ranks)
        {
            hash.Add(rank);
        }

        return hash.ToHashCode();
    }
}

public enum EntityCampRelation
{
    NonHostile,
    Friendly,
    Hostile
}
