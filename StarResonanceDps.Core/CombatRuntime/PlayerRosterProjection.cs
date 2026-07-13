using Newtonsoft.Json.Linq;
using StarResonanceDps.Core.Models;
using StarResonanceDps.Core.Services;
using Zproto;

namespace StarResonanceDps.Core.CombatRuntime;

internal static class PlayerRosterProjection
{
    private static readonly PlayerRosterStore RosterStore = PlayerRosterStore.Instance;

    public static void BeginMap()
    {
        RosterStore.BeginMap();
    }

    public static void UpdateMapName()
    {
        RosterStore.UpdateMapName(EncounterManager.SceneName);
    }

    public static void UpsertSelf(long playerUuid)
    {
        UpsertPlayer(playerUuid, isSelfHint: true);
    }

    public static void UpsertPlayer(long playerUuid)
    {
        UpsertPlayer(playerUuid, isSelfHint: false);
    }

    private static void UpsertPlayer(long playerUuid, bool isSelfHint)
    {
        var encounter = EncounterManager.Current;
        if (playerUuid == 0 || encounter is null)
        {
            return;
        }

        if (!encounter.Entities.TryGetValue(playerUuid, out var entity))
        {
            return;
        }

        var isSelf = isSelfHint
            || playerUuid == MessageManager.currentUserUuid
            || playerUuid == AppState.PlayerUUID
            || (AppState.PlayerUID != 0 && Utils.UuidToEntityId(playerUuid) == AppState.PlayerUID);

        var isCharacter = entity.EntityType == EEntityType.EntChar
            || Utils.UuidToEntityType(playerUuid) == (long)EEntityType.EntChar;
        if (!isCharacter)
        {
            return;
        }

        var isNpc = !isSelf && GetInt(entity, "AttrId") > 0;

        var characterId = entity.UID != 0 ? entity.UID : Utils.UuidToEntityId(playerUuid);
        if (characterId == 0)
        {
            return;
        }

        var name = entity.Name ?? string.Empty;
        if (!isSelf && string.IsNullOrWhiteSpace(name))
        {
            return;
        }

        var combatAttributes = new PlayerCombatAttributes(
            PhysicalAttack: GetInt(entity, "AttrAttack"),
            MagicalAttack: GetInt(entity, "AttrMattack"),
            Strength: GetInt(entity, "AttrStrength"),
            Dexterity: GetInt(entity, "AttrDexterity"),
            Intelligence: GetInt(entity, "AttrIntelligence"),
            Endurance: GetInt(entity, "AttrVitality"),
            Armor: GetInt(entity, "AttrDefense"),
            Critical: GetInt(entity, "AttrCri"),
            CriticalPercent: GetInt(entity, "AttrCrit"),
            Haste: GetInt(entity, "AttrHaste"),
            HastePercent: GetInt(entity, "AttrHastePct"),
            Luck: GetInt(entity, "AttrLuck"),
            LuckPercent: GetInt(entity, "AttrLuckyStrikeProb"),
            Mastery: GetInt(entity, "AttrMastery"),
            MasteryPercent: GetInt(entity, "AttrMasteryPct"),
            Versatility: GetInt(entity, "AttrVersatility"),
            VersatilityPercent: GetInt(entity, "AttrVersatilityPct"),
            BlockPercent: GetInt(entity, "AttrBlockPct"));

        var seasonStrength = entity.SeasonStrength != 0
            ? ToInt32(entity.SeasonStrength)
            : GetFirstNonZeroInt(
                entity,
                "AttrSeasonStrength",
                "AttrSeasonStrengthTotal",
                "AttrSeasonStrengthAdd",
                "AttrSeasonStrengthExAdd",
                "AttrSeasonStrengthPer",
                "AttrSeasonStrengthExPer");
        var subProfessionId = entity.SubProfessionId;
        var equipmentData = GetEquipmentData(entity);

        RosterStore.Upsert(new PlayerRosterEntry(
            characterId,
            name,
            entity.ProfessionId,
            entity.AbilityScore != 0 ? entity.AbilityScore : GetInt(entity, "AttrFightPoint"),
            seasonStrength,
            entity.Hp,
            entity.MaxHp,
            PlayerClassSpecResolver.FromSubProfessionId(subProfessionId),
            isSelf,
            combatAttributes,
            subProfessionId,
            entity.Level,
            ToInt32(entity.SeasonLevel),
            equipmentData,
            isNpc));
    }

    private static PlayerEquipmentData? GetEquipmentData(Entity entity)
    {
        var rawData = entity.GetAttrKV("AttrEquipData");
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
