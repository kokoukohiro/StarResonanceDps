using StarResonanceDps.Core.Models;

namespace StarResonanceDps.Core.CombatRuntime;

internal static class NearbyEntityCampState
{
    private static readonly object Sync = new();
    private static readonly Dictionary<long, int> Camps = [];
    private static long _selfEntityUuid;

    public static void BeginMap()
    {
        lock (Sync)
        {
            Camps.Clear();
            _selfEntityUuid = 0;
        }
    }

    public static bool SetSelfEntity(long entityUuid)
    {
        if (entityUuid == 0)
        {
            return false;
        }

        lock (Sync)
        {
            if (_selfEntityUuid == entityUuid)
            {
                return false;
            }

            _selfEntityUuid = entityUuid;
            return true;
        }
    }

    public static bool SetCamp(long entityUuid, bool isPresent, int camp)
    {
        if (entityUuid == 0)
        {
            return false;
        }

        lock (Sync)
        {
            var changed = isPresent
                ? !Camps.TryGetValue(entityUuid, out var existingCamp) || existingCamp != camp
                : Camps.Remove(entityUuid);

            if (isPresent)
            {
                Camps[entityUuid] = camp;
            }

            return changed && entityUuid == _selfEntityUuid;
        }
    }

    public static void Remove(long entityUuid)
    {
        if (entityUuid == 0)
        {
            return;
        }

        lock (Sync)
        {
            Camps.Remove(entityUuid);
        }
    }

    public static EntityCampRelation GetRelation(long entityUuid)
    {
        lock (Sync)
        {
            return GetRelationNoLock(entityUuid);
        }
    }

    public static IReadOnlyDictionary<long, EntityCampRelation> GetRelations(IEnumerable<long> entityUuids)
    {
        ArgumentNullException.ThrowIfNull(entityUuids);

        lock (Sync)
        {
            return entityUuids
                .Distinct()
                .ToDictionary(entityUuid => entityUuid, GetRelationNoLock);
        }
    }

    private static EntityCampRelation GetRelationNoLock(long entityUuid)
    {
        if (_selfEntityUuid == 0
            || !Camps.TryGetValue(_selfEntityUuid, out var selfCamp)
            || !Camps.TryGetValue(entityUuid, out var entityCamp))
        {
            return EntityCampRelation.NonHostile;
        }

        return entityCamp == selfCamp
            ? EntityCampRelation.Friendly
            : EntityCampRelation.Hostile;
    }
}
