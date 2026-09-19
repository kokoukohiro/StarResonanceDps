using StarResonanceDps.Core.Models;
using StarResonanceDps.Core.Services;
using Zproto;

namespace StarResonanceDps.Core.CombatRuntime;

internal static class NearbyEntityProjection
{
    private static readonly NearbyEntityStore EntityStore = NearbyEntityStore.Instance;

    public static void BeginMap()
    {
        NearbyEntityCampState.BeginMap();
        EntityStore.BeginMap();
    }

    public static void SetSelfEntity(long entityUuid)
    {
        if (NearbyEntityCampState.SetSelfEntity(entityUuid))
        {
            RefreshVisibleCampRelations();
        }
    }

    public static void UpdateCamp(long entityUuid, bool isPresent, int camp)
    {
        if (NearbyEntityCampState.SetCamp(entityUuid, isPresent, camp))
        {
            RefreshVisibleCampRelations();
        }
    }

    public static void UpdateMapName()
    {
        EntityStore.UpdateMapName(EncounterManager.SceneName, EncounterManager.ChannelLineId);
    }

    public static void AddOrUpdateAppearedEntity(long entityUuid)
    {
        var entity = GetListedEntity(entityUuid);
        if (entity is null)
        {
            return;
        }

        EntityStore.UpsertAppeared(CreateEntry(entityUuid, entity));
    }

    public static void RefreshEntity(long entityUuid, IReadOnlySet<EAttrType> changedAttributes)
    {
        ArgumentNullException.ThrowIfNull(changedAttributes);

        var entity = GetListedEntity(entityUuid);
        if (entity is null)
        {
            return;
        }

        var campRelation = changedAttributes.Contains(EAttrType.AttrCamp)
            ? NearbyEntityCampState.GetRelation(entityUuid)
            : (EntityCampRelation?)null;

        if (EntityStore.TryRefresh(
                entityUuid,
                existing => MergeChangedFields(existing, entity, changedAttributes, campRelation)))
        {
            return;
        }

        EntityStore.UpsertAppeared(CreateEntry(entityUuid, entity));
    }

    public static void RemoveEntity(long entityUuid)
    {
        NearbyEntityCampState.Remove(entityUuid);
        EntityStore.Remove(entityUuid);
    }

    private static Entity? GetListedEntity(long entityUuid)
    {
        var encounter = EncounterManager.Current;
        if (entityUuid == 0 || encounter is null)
        {
            return null;
        }

        if (!encounter.Entities.TryGetValue(entityUuid, out var entity))
        {
            return null;
        }

        if (!IsListedType(ResolveEntityType(entityUuid, entity)))
        {
            EntityStore.Remove(entityUuid);
            return null;
        }

        return entity;
    }

    private static NearbyEntityEntry CreateEntry(long entityUuid, Entity entity)
    {
        var entityId = ResolveEntityId(entityUuid, entity);
        var entityType = ResolveEntityType(entityUuid, entity);
        return new NearbyEntityEntry(
            entityUuid,
            entityId,
            ResolveName(entity, entityId, entityUuid),
            entityType,
            entity.MonsterType,
            ResolveLevel(entity),
            entity.Hp,
            entity.MaxHp,
            GetInt(entity, "AttrStunned"),
            GetInt(entity, "AttrMaxStunned"),
            GetInt(entity, "AttrCanLessenHp") > 0,
            GetInt(entity, "AttrIsLockStunned") > 0,
            NearbyEntityCampState.GetRelation(entityUuid),
            Utils.GetCurrentShield(entity),
            ResolveHasHpBar(entityType, entity));
    }

    private static NearbyEntityEntry MergeChangedFields(
        NearbyEntityEntry existing,
        Entity entity,
        IReadOnlySet<EAttrType> changedAttributes,
        EntityCampRelation? campRelation)
    {
        var updated = existing;

        if (changedAttributes.Contains(EAttrType.AttrId))
        {
            var entityId = ResolveEntityId(existing.EntityUuid, entity);
            updated = updated with
            {
                EntityId = entityId,
                Name = ResolveName(entity, entityId, existing.EntityUuid),
                MonsterType = entity.MonsterType,
                HasHpBar = ResolveHasHpBar(existing.EntityType, entity)
            };
        }

        if (changedAttributes.Contains(EAttrType.AttrName))
        {
            updated = updated with
            {
                Name = ResolveName(entity, updated.EntityId, existing.EntityUuid)
            };
        }

        if (changedAttributes.Contains(EAttrType.AttrLevel)
            || changedAttributes.Contains(EAttrType.AttrMonsterSeasonLevel)
            || changedAttributes.Contains(EAttrType.AttrSeasonLevel))
        {
            updated = updated with { Level = ResolveLevel(entity) };
        }

        if (changedAttributes.Contains(EAttrType.AttrHp))
        {
            updated = updated with { CurrentHp = entity.Hp };
        }

        if (changedAttributes.Contains(EAttrType.AttrMaxHp))
        {
            updated = updated with { MaxHp = entity.MaxHp };
        }

        if (changedAttributes.Contains(EAttrType.AttrShieldList))
        {
            updated = updated with { CurrentShield = Utils.GetCurrentShield(entity) };
        }

        if (changedAttributes.Contains(EAttrType.AttrStunned))
        {
            updated = updated with { CurrentBreak = GetInt(entity, "AttrStunned") };
        }

        if (changedAttributes.Contains(EAttrType.AttrMaxStunned))
        {
            updated = updated with { MaxBreak = GetInt(entity, "AttrMaxStunned") };
        }

        if (changedAttributes.Contains(EAttrType.AttrCanLessenHp))
        {
            updated = updated with { IsInvulnerable = GetInt(entity, "AttrCanLessenHp") > 0 };
        }

        if (changedAttributes.Contains(EAttrType.AttrIsLockStunned))
        {
            updated = updated with { IsBreakLocked = GetInt(entity, "AttrIsLockStunned") > 0 };
        }

        if (campRelation.HasValue)
        {
            updated = updated with { CampRelation = campRelation.Value };
        }

        return updated;
    }

    private static void RefreshVisibleCampRelations()
    {
        var entries = EntityStore.Current.Entries;
        if (entries.Count == 0)
        {
            return;
        }

        var relations = NearbyEntityCampState.GetRelations(entries.Select(entry => entry.EntityUuid));
        EntityStore.UpdateCampRelations(relations);
    }

    /// <summary>
    /// 一覧に入れる種類。名前の表を持つモンスターだけ(名前は <c>CombatDataCatalog.GetEntityName</c>)。
    /// 名前で絞るのは表示側(名前は表示言語で決まる)。
    ///
    /// <para>
    /// <b>召喚ビット(UUID のビット15)では外さない。</b> 敵が召喚した敵にも立つ。
    /// </para>
    /// </summary>
    private static bool IsListedType(EEntityType entityType)
    {
        return entityType is EEntityType.EntMonster;
    }

    /// <summary>
    /// ゲーム内でプレイヤーに見える HP バーを持つ実体か。持つ実体は名前が無くても一覧に載せる。
    /// 判定は被ダメログと同じ <see cref="CombatDataCatalog.HasMonsterHpBar"/>。
    /// <c>AttrId</c> が未着のときは判定できないので、持つ側に倒す。
    /// </summary>
    private static bool ResolveHasHpBar(EEntityType entityType, Entity entity)
    {
        if (entityType != EEntityType.EntMonster)
        {
            return false;
        }

        return entity.GetAttrKV("AttrId") is not int attrId
            || attrId <= 0
            || CombatDataCatalog.HasMonsterHpBar(attrId);
    }

    private static EEntityType ResolveEntityType(long entityUuid, Entity entity)
    {
        return entity.EntityType == EEntityType.EntErrType
            ? (EEntityType)Utils.UuidToEntityType(entityUuid)
            : entity.EntityType;
    }

    /// <summary>
    /// 種別ID(<c>AttrId</c>)。未着なら 0。
    /// UUID の通し番号で代えない。通し番号を種別IDとして名前を引くと、別の実体の名前になる。
    /// </summary>
    private static long ResolveEntityId(long entityUuid, Entity entity)
    {
        return entity.GetAttrKV("AttrId") is int attrId && attrId > 0 ? attrId : 0;
    }

    private static int ResolveLevel(Entity entity)
    {
        return entity.Level != 0
            ? entity.Level
            : GetFirstNonZeroInt(entity, "AttrMonsterSeasonLevel", "AttrSeasonLevel");
    }

    private static string ResolveName(Entity entity, long entityId, long entityUuid)
    {
        if (!string.IsNullOrWhiteSpace(entity.Name))
        {
            return entity.Name;
        }

        if (entityId != 0)
        {
            return $"Monster {entityId}";
        }

        return $"Entity {entityUuid}";
    }

    private static int GetFirstNonZeroInt(Entity entity, params string[] keys)
    {
        foreach (var key in keys)
        {
            var value = GetInt(entity, key);
            if (value != 0)
            {
                return value;
            }
        }

        return 0;
    }

    private static int GetInt(Entity entity, string key)
    {
        return ToInt32(entity.GetAttrKV(key));
    }

    private static int ToInt32(object? value)
    {
        return value switch
        {
            int integer => integer,
            long integer => integer switch
            {
                > int.MaxValue => int.MaxValue,
                < int.MinValue => int.MinValue,
                _ => (int)integer
            },
            uint integer => integer > int.MaxValue ? int.MaxValue : (int)integer,
            ulong integer => integer > int.MaxValue ? int.MaxValue : (int)integer,
            short integer => integer,
            ushort integer => integer,
            byte integer => integer,
            sbyte integer => integer,
            _ => 0
        };
    }
}
