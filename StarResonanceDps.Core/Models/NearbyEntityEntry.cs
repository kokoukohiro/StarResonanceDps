using StarResonanceDps.Core.CombatRuntime;
using Zproto;
using MonsterClassification = StarResonanceDps.Core.CombatRuntime.EMonsterType;

namespace StarResonanceDps.Core.Models;

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
    EntityCampRelation CampRelation);

public enum EntityCampRelation
{
    NonHostile,
    Friendly,
    Hostile
}
