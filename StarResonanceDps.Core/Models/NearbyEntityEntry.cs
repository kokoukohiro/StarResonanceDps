using StarResonanceDps.Core.CombatRuntime;
using Zproto;
using MonsterClassification = StarResonanceDps.Core.CombatRuntime.EMonsterType;

namespace StarResonanceDps.Core.Models;

/// <param name="HasHpBar">
/// ゲーム内でプレイヤーに見える HP バーを持つ実体か。エンティティリストは、これが true なら名前が無くても載せる。
/// 表で判定できない実体(<c>AttrId</c> が未着・表に無い番号)は true。
/// </param>
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
    bool HasHpBar);

public enum EntityCampRelation
{
    NonHostile,
    Friendly,
    Hostile
}
