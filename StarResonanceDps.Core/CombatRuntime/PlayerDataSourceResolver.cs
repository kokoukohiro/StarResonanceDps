using Newtonsoft.Json.Linq;
using StarResonanceDps.Core.Models;
using StarResonanceDps.Core.Services;
using Zproto;

namespace StarResonanceDps.Core.CombatRuntime;

internal sealed record PlayerDataSourceSnapshot(
    long CharacterId,
    string Name,
    int ProfessionId,
    int CombatPower,
    int SubProfessionId,
    int SeasonStrength,
    int Level,
    int SeasonLevel,
    long CurrentHp,
    long MaxHp,
    bool IsNpc,
    bool IsSpecAbilityUnequipped,
    PlayerEquipmentData? EquipmentData);

internal static class PlayerDataSourceResolver
{
    public static PlayerDataSourceSnapshot Resolve(Entity entity, bool isSelf)
    {
        var characterId = entity.UID != 0
            ? entity.UID
            : Utils.UuidToEntityId(entity.UUID);

        return Resolve(characterId, entity, entity, isSelf);
    }

    public static PlayerDataSourceSnapshot Resolve(
        long characterId,
        Entity? nearbyEntity,
        Entity? metadataEntity,
        bool isSelf)
    {
        var party = PartyStateStore.Instance.Current;
        PartyMemberSupplement? partySupplement = null;
        if (!isSelf && party.TryGetSupplement(characterId, out var supplement))
        {
            partySupplement = supplement;
        }

        var nearbyName = nearbyEntity is { Name.Length: > 0 }
            ? nearbyEntity.Name
            : GetString(nearbyEntity, "AttrName");
        var nearbyProfessionId = nearbyEntity is { ProfessionId: > 0 }
            ? nearbyEntity.ProfessionId
            : GetInt(nearbyEntity, "AttrProfessionId");
        var nearbyCombatPower = nearbyEntity is { AbilityScore: > 0 }
            ? nearbyEntity.AbilityScore
            : GetInt(nearbyEntity, "AttrFightPoint");
        var nearbySeasonStrength = nearbyEntity is { SeasonStrength: > 0 }
            ? ToInt32(nearbyEntity.SeasonStrength)
            : GetFirstNonZeroInt(
                nearbyEntity,
                "AttrSeasonStrength",
                "AttrSeasonStrengthTotal",
                "AttrSeasonStrengthAdd",
                "AttrSeasonStrengthExAdd",
                "AttrSeasonStrengthPer",
                "AttrSeasonStrengthExPer");
        var nearbyLevel = nearbyEntity is { Level: > 0 }
            ? nearbyEntity.Level
            : GetInt(nearbyEntity, "AttrLevel");
        var nearbySeasonLevel = ToInt32(nearbyEntity?.SeasonLevel);
        if (nearbySeasonLevel <= 0)
        {
            nearbySeasonLevel = GetInt(nearbyEntity, "AttrSeasonLevel");
        }

        var metadataName = metadataEntity is { Name.Length: > 0 }
            ? metadataEntity.Name
            : GetString(metadataEntity, "AttrName");
        var metadataProfessionId = metadataEntity is { ProfessionId: > 0 }
            ? metadataEntity.ProfessionId
            : GetInt(metadataEntity, "AttrProfessionId");
        var metadataCombatPower = metadataEntity is { AbilityScore: > 0 }
            ? metadataEntity.AbilityScore
            : GetInt(metadataEntity, "AttrFightPoint");
        var metadataSeasonStrength = metadataEntity is { SeasonStrength: > 0 }
            ? ToInt32(metadataEntity.SeasonStrength)
            : GetFirstNonZeroInt(
                metadataEntity,
                "AttrSeasonStrength",
                "AttrSeasonStrengthTotal",
                "AttrSeasonStrengthAdd",
                "AttrSeasonStrengthExAdd",
                "AttrSeasonStrengthPer",
                "AttrSeasonStrengthExPer");
        var metadataLevel = metadataEntity is { Level: > 0 }
            ? metadataEntity.Level
            : GetInt(metadataEntity, "AttrLevel");
        var metadataSeasonLevel = ToInt32(metadataEntity?.SeasonLevel);
        if (metadataSeasonLevel <= 0)
        {
            metadataSeasonLevel = GetInt(metadataEntity, "AttrSeasonLevel");
        }

        var currentHp = nearbyEntity?.Hp ?? 0;
        var maxHp = nearbyEntity?.MaxHp ?? 0;
        var hasNearbyMaxHp = nearbyEntity is not null
            && TryGetPositiveInt64(nearbyEntity.GetAttrKV("AttrMaxHp"), out _);
        if (!isSelf
            && !hasNearbyMaxHp
            && partySupplement is not null)
        {
            if (partySupplement.MaxHp > 0)
            {
                maxHp = partySupplement.MaxHp;
            }

            if (nearbyEntity is not null
                && TryGetNonNegativeInt64(nearbyEntity.GetAttrKV("AttrHp"), out var nearbyHp))
            {
                currentHp = nearbyHp;
            }
            else if (partySupplement.MaxHp > 0 || !partySupplement.IsNpc)
            {
                currentHp = Math.Max(partySupplement.CurrentHp, 0);
            }
        }

        return new PlayerDataSourceSnapshot(
            characterId,
            !string.IsNullOrEmpty(nearbyName)
                ? nearbyName
                : !isSelf && partySupplement is { Name.Length: > 0 }
                    ? partySupplement.Name
                    : metadataName,
            isSelf
                ? nearbyProfessionId
                : nearbyProfessionId > 0
                    ? nearbyProfessionId
                    : partySupplement is { ProfessionId: > 0 }
                        ? partySupplement.ProfessionId
                        : metadataProfessionId,
            isSelf
                ? nearbyCombatPower
                : nearbyCombatPower > 0
                    ? nearbyCombatPower
                    : partySupplement is { CombatPower: > 0 }
                        ? partySupplement.CombatPower
                        : metadataCombatPower,
            ResolveSubProfessionId(characterId, nearbyEntity, metadataEntity, isSelf),
            isSelf
                ? nearbySeasonStrength
                : nearbySeasonStrength > 0
                    ? nearbySeasonStrength
                    : partySupplement is { SeasonStrength: > 0 }
                        ? partySupplement.SeasonStrength
                        : metadataSeasonStrength,
            isSelf
                ? nearbyLevel
                : nearbyLevel > 0
                    ? nearbyLevel
                    : partySupplement is { Level: > 0 }
                        ? partySupplement.Level
                        : metadataLevel,
            isSelf
                ? nearbySeasonLevel
                : nearbySeasonLevel > 0
                    ? nearbySeasonLevel
                    : partySupplement is { SeasonLevel: > 0 }
                        ? partySupplement.SeasonLevel
                        : metadataSeasonLevel,
            currentHp,
            maxHp,
            !isSelf && partySupplement?.IsNpc == true,
            ResolveSpecAbilityUnequipped(characterId, nearbyEntity, metadataEntity, isSelf),
            GetEquipmentData(nearbyEntity)
                ?? partySupplement?.EquipmentData
                ?? GetEquipmentData(metadataEntity));
    }

    /// <summary>
    /// 職業特化を解決する。
    ///
    /// <para>
    /// 真値は特化マーカーバフの観測結果で、エンティティが持っている値。相手がAOI外に出ると
    /// 観測できなくなるので、自分以外のパーティメンバーだけキャッシュから補完する
    /// (イマジン/ロールスキルと同じ扱い)。自分はキャッシュを見ない。
    /// </para>
    /// </summary>
    private static int ResolveSubProfessionId(
        long characterId,
        Entity? nearbyEntity,
        Entity? metadataEntity,
        bool isSelf)
    {
        // 変身クラス(8/14/15)は特化を持たない。キャッシュも見ない。
        // Entity 側の関門だけでは、変身前の特化がキャッシュから補完されて残ってしまう。
        var professionId = nearbyEntity is { ProfessionId: > 0 }
            ? nearbyEntity.ProfessionId
            : metadataEntity?.ProfessionId ?? 0;
        if (Models.PlayerClassSpecResolver.TryResolveTransformation(professionId, out _))
        {
            return 0;
        }

        if (nearbyEntity is { SubProfessionId: > 0 })
        {
            return nearbyEntity.SubProfessionId;
        }

        if (metadataEntity is { SubProfessionId: > 0 })
        {
            return metadataEntity.SubProfessionId;
        }

        if (isSelf)
        {
            return 0;
        }

        return PartyMemberCache.Instance.TryGetSubProfession(characterId, out var cached)
            ? cached
            : 0;
    }

    /// <summary>
    /// 特化アビリティ未装着が確定しているか。
    ///
    /// <para>
    /// 「マーカーバフが無い」だけでは未装着と言えない。単に観測していないだけの可能性がある。
    /// 全バフスナップショットを受け取った上でマーカーが1つも無かったときだけ true になる
    /// (<see cref="Entity.IsSpecAbilityUnequipped"/>)。
    /// </para>
    /// </summary>
    private static bool ResolveSpecAbilityUnequipped(
        long characterId,
        Entity? nearbyEntity,
        Entity? metadataEntity,
        bool isSelf)
    {
        if (nearbyEntity?.IsSpecAbilityUnequipped == true
            || metadataEntity?.IsSpecAbilityUnequipped == true)
        {
            return true;
        }

        return !isSelf && PartyMemberCache.Instance.IsSpecAbilityUnequipped(characterId);
    }

    public static int GetInt(Entity? entity, string key)
    {
        return entity is null ? 0 : ToInt32(entity.GetAttrKV(key));
    }

    private static PlayerEquipmentData? GetEquipmentData(Entity? entity)
    {
        var rawData = entity?.GetAttrKV("AttrEquipData");
        if (rawData is null)
        {
            return null;
        }

        if (rawData is JArray serializedItems)
        {
            var parsedItems = serializedItems.ToObject<List<EquipNine>>();
            return parsedItems is null
                ? PlayerEquipmentData.Invalid
                : CreateEquipmentData(parsedItems);
        }

        if (rawData is IEnumerable<EquipNine> items)
        {
            return CreateEquipmentData(items);
        }

        return PlayerEquipmentData.Invalid;
    }

    private static PlayerEquipmentData CreateEquipmentData(IEnumerable<EquipNine> items)
    {
        return PlayerEquipmentData.Create(
            items.Select(item => new PlayerEquipmentItem(item.Slot, item.EquipID)));
    }

    private static int GetFirstNonZeroInt(Entity? entity, params string[] keys)
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

    private static string GetString(Entity? entity, string key)
    {
        return entity?.GetAttrKV(key) as string ?? string.Empty;
    }

    private static bool TryGetPositiveInt64(object? value, out long result)
    {
        if (TryGetNonNegativeInt64(value, out result) && result > 0)
        {
            return true;
        }

        result = 0;
        return false;
    }

    private static bool TryGetNonNegativeInt64(object? value, out long result)
    {
        switch (value)
        {
            case long integer when integer >= 0:
                result = integer;
                return true;
            case int integer when integer >= 0:
                result = integer;
                return true;
            case uint integer:
                result = integer;
                return true;
            case ulong integer when integer <= long.MaxValue:
                result = (long)integer;
                return true;
            case short integer when integer >= 0:
                result = integer;
                return true;
            case ushort integer:
                result = integer;
                return true;
            case byte integer:
                result = integer;
                return true;
            case sbyte integer when integer >= 0:
                result = integer;
                return true;
            default:
                result = 0;
                return false;
        }
    }

    private static int ToInt32(object? value)
    {
        return value switch
        {
            int integer => integer,
            long integer => ClampToInt32(integer),
            uint integer => integer > int.MaxValue ? int.MaxValue : (int)integer,
            ulong integer => integer > int.MaxValue ? int.MaxValue : (int)integer,
            short integer => integer,
            ushort integer => integer,
            byte integer => integer,
            sbyte integer => integer,
            _ => 0
        };
    }

    private static int ClampToInt32(long value)
    {
        return value switch
        {
            > int.MaxValue => int.MaxValue,
            < int.MinValue => int.MinValue,
            _ => (int)value
        };
    }
}
