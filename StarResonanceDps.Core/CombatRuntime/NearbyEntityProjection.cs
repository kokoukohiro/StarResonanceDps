using StarResonanceDps.Core.Models;
using StarResonanceDps.Core.Services;
using Zproto;

namespace StarResonanceDps.Core.CombatRuntime;

internal static class NearbyEntityProjection
{
    private static readonly NearbyEntityStore EntityStore = NearbyEntityStore.Instance;

    public static void BeginMap()
    {
        Diagnostics.SceneResetProbe.CaptureReset("エンティティリスト", EntityStore.Current.Entries.Count);
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
        EntityStore.UpdateMapName(EncounterManager.SceneDisplayName);
    }

    public static void AddOrUpdateAppearedEntity(long entityUuid)
    {
        var entity = GetMonsterEntity(entityUuid);
        if (entity is null)
        {
            return;
        }

        if (IsHiddenEntity(entityUuid))
        {
            EntityStore.Remove(entityUuid);
            return;
        }

        EntityStore.UpsertAppeared(CreateEntry(entityUuid, entity));
    }

    public static void RefreshEntity(long entityUuid, IReadOnlySet<EAttrType> changedAttributes)
    {
        ArgumentNullException.ThrowIfNull(changedAttributes);

        var entity = GetMonsterEntity(entityUuid);
        if (entity is null)
        {
            return;
        }

        if (changedAttributes.Contains(EAttrType.AttrId) && IsHiddenEntity(entityUuid))
        {
            EntityStore.Remove(entityUuid);
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

        if (IsHiddenEntity(entityUuid))
        {
            EntityStore.Remove(entityUuid);
            return;
        }

        EntityStore.UpsertAppeared(CreateEntry(entityUuid, entity));
    }

    public static void RemoveEntity(long entityUuid)
    {
        NearbyEntityCampState.Remove(entityUuid);
        EntityStore.Remove(entityUuid);
    }

    private static Entity? GetMonsterEntity(long entityUuid)
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

        var entityType = ResolveEntityType(entityUuid, entity);
        if (entityType != EEntityType.EntMonster)
        {
            EntityStore.Remove(entityUuid);
            return null;
        }

        return entity;
    }

    private static NearbyEntityEntry CreateEntry(long entityUuid, Entity entity)
    {
        var entityId = ResolveEntityId(entityUuid, entity);
        return new NearbyEntityEntry(
            entityUuid,
            entityId,
            ResolveName(entity, entityId, entityUuid),
            ResolveEntityType(entityUuid, entity),
            entity.MonsterType,
            ResolveLevel(entity),
            entity.Hp,
            entity.MaxHp,
            GetInt(entity, "AttrStunned"),
            GetInt(entity, "AttrMaxStunned"),
            GetInt(entity, "AttrCanLessenHp") > 0,
            GetInt(entity, "AttrIsLockStunned") > 0,
            NearbyEntityCampState.GetRelation(entityUuid),
            Utils.GetCurrentShield(entity));
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
                MonsterType = entity.MonsterType
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
    /// 一覧に出さないエンティティか。<b>召喚体を除く。</b>
    ///
    /// <para>
    /// 判定は <b>UUIDのビット15</b>(<see cref="Utils.IsSummonByUuid"/>)。ワイヤ側の情報なので、
    /// <c>MonsterTable</c> に載っていない相手でも判定できる。
    /// </para>
    ///
    /// <para>
    /// 以前は <c>MonsterTable.HudShowParam[0] == 0</c> で除外していたが、実測(2026-09-01)で
    /// <b>ボスがこれに当たって一度も表示されていなかった</b>(Light·Tonatiuh 102701、
    /// Rin·Izcorgiky 102101 など)。アンパックを更新しても値は変わらず(共通3038件で差分0)、
    /// 表が古いのではなく判定が合っていなかった。<c>HudShowParam</c> の意味は Lua にも proto にも
    /// 定義が無く追えていない。
    /// </para>
    ///
    /// <para>
    /// 召喚ビットで見ると実測どおりに分かれる。立っているのは他プレイヤーのイマジン召喚
    /// (`3000027` 等)、効果の実体(燃烧地面・时空立场・奶环 等)、そして
    /// <b>何も無い場所に湧いて素性が分からなかった `3110008` と `Monster 25`</b>。
    /// 立っていないのはボス・取り巻き・クリスタル・訓練ダミー・設置物とプレイヤー。
    /// </para>
    /// </summary>
    private static bool IsHiddenEntity(long entityUuid)
    {
        return Utils.IsSummonByUuid(entityUuid);
    }

    private static EEntityType ResolveEntityType(long entityUuid, Entity entity)
    {
        return entity.EntityType == EEntityType.EntErrType
            ? (EEntityType)Utils.UuidToEntityType(entityUuid)
            : entity.EntityType;
    }

    private static long ResolveEntityId(long entityUuid, Entity entity)
    {
        return entity.UID != 0 ? entity.UID : Utils.UuidToEntityId(entityUuid);
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
