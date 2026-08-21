using StarResonanceDps.Core.Models;
using StarResonanceDps.Core.Services;
using Zproto;

namespace StarResonanceDps.Core.CombatRuntime;

internal static class PlayerRosterProjection
{
    private static readonly object NearbyPlayerSync = new();
    private static readonly HashSet<long> NearbyPlayerUuids = [];
    private static readonly PlayerRosterStore RosterStore = PlayerRosterStore.Instance;

    public static void BeginMap()
    {
        lock (NearbyPlayerSync)
        {
            NearbyPlayerUuids.Clear();
        }

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

    public static void AddOrUpdateNearbyPlayer(long playerUuid)
    {
        if (!TryGetCharacterEntity(playerUuid, out _))
        {
            return;
        }

        var characterId = Utils.UuidToEntityId(playerUuid);
        if (IsSelf(playerUuid, characterId))
        {
            UpsertPlayer(playerUuid, isSelfHint: true);
            return;
        }

        lock (NearbyPlayerSync)
        {
            NearbyPlayerUuids.Add(playerUuid);
        }

        UpsertPlayer(playerUuid, isSelfHint: false);
    }

    public static void RemoveNearbyPlayer(long playerUuid)
    {
        var removed = false;
        lock (NearbyPlayerSync)
        {
            removed = NearbyPlayerUuids.Remove(playerUuid);
        }

        if (removed)
        {
            RebuildRoster();
        }
    }

    public static void ResetNearbyPlayers()
    {
        lock (NearbyPlayerSync)
        {
            NearbyPlayerUuids.Clear();
        }

        RebuildRoster();
    }

    public static void UpsertPlayer(long playerUuid)
    {
        UpsertPlayer(playerUuid, isSelfHint: false);
    }

    public static void RebuildRoster()
    {
        var party = PartyStateStore.Instance.Current;
        var encounter = EncounterManager.Current;
        long[] nearbyPlayerUuids;
        lock (NearbyPlayerSync)
        {
            nearbyPlayerUuids = [.. NearbyPlayerUuids];
        }

        var nearbyPlayersByCharacterId = nearbyPlayerUuids
            .Select(entityUuid => (EntityUuid: entityUuid, CharacterId: Utils.UuidToEntityId(entityUuid)))
            .Where(player => player.CharacterId > 0)
            .GroupBy(player => player.CharacterId)
            .ToDictionary(group => group.Key, group => group.Last().EntityUuid);
        var metadataPlayersByCharacterId = encounter?.Entities.Values
            .Where(IsCharacterEntity)
            .Select(entity => (
                Entity: entity,
                CharacterId: entity.UID != 0
                    ? entity.UID
                    : Utils.UuidToEntityId(entity.UUID)))
            .Where(player => player.CharacterId > 0)
            .GroupBy(player => player.CharacterId)
            .ToDictionary(group => group.Key, group => group.Last().Entity)
            ?? new Dictionary<long, Entity>();

        var characterIds = new List<long>();
        var addedCharacterIds = new HashSet<long>();
        var selfCharacterId = AppState.PlayerUID != 0
            ? AppState.PlayerUID
            : Utils.UuidToEntityId(MessageManager.currentUserUuid != 0
                ? MessageManager.currentUserUuid
                : AppState.PlayerUUID);
        foreach (var characterId in party.OrderedCharacterIds)
        {
            AddCharacterId(characterId);
        }

        AddCharacterId(selfCharacterId);

        foreach (var characterId in nearbyPlayersByCharacterId.Keys)
        {
            AddCharacterId(characterId);
        }

        var entries = new List<PlayerRosterEntry>(characterIds.Count);
        var existingSelf = RosterStore.Current.Entries.FirstOrDefault(entry => entry.IsSelf);
        foreach (var characterId in characterIds)
        {
            var isSelf = characterId == selfCharacterId;
            Entity? nearbyEntity = null;
            Entity? metadataEntity = null;
            if (isSelf)
            {
                var selfUuid = MessageManager.currentUserUuid != 0
                    ? MessageManager.currentUserUuid
                    : AppState.PlayerUUID != 0
                        ? AppState.PlayerUUID
                        : Utils.EntityIdToUuid(characterId, (long)EEntityType.EntChar, false, false);
                if (encounter != null)
                {
                    encounter.Entities.TryGetValue(selfUuid, out nearbyEntity);
                }

                metadataEntity = nearbyEntity;
            }
            else if (nearbyPlayersByCharacterId.TryGetValue(characterId, out var nearbyUuid)
                && encounter != null)
            {
                encounter.Entities.TryGetValue(nearbyUuid, out nearbyEntity);
                metadataEntity = nearbyEntity;
            }

            if (metadataEntity is null)
            {
                metadataPlayersByCharacterId.TryGetValue(characterId, out metadataEntity);
            }

            var isPartyMember = party.GetMembership(characterId) == PartyMembershipState.Member;
            if (!isSelf && !isPartyMember && nearbyEntity is null)
            {
                continue;
            }

            if (isSelf && nearbyEntity is null && existingSelf?.CharacterId == characterId)
            {
                entries.Add(existingSelf);
                continue;
            }

            if (TryCreateEntry(characterId, nearbyEntity, metadataEntity, isSelf, out var entry))
            {
                entries.Add(entry);
            }
        }

        RosterStore.Replace(entries);

        void AddCharacterId(long characterId)
        {
            if (characterId > 0 && addedCharacterIds.Add(characterId))
            {
                characterIds.Add(characterId);
            }
        }
    }

    private static void UpsertPlayer(long playerUuid, bool isSelfHint)
    {
        if (!TryGetCharacterEntity(playerUuid, out var entity))
        {
            return;
        }

        var characterId = entity.UID != 0
            ? entity.UID
            : Utils.UuidToEntityId(playerUuid);
        if (characterId == 0)
        {
            return;
        }

        var isSelf = isSelfHint || IsSelf(playerUuid, characterId);
        var isPartyMember = PartyStateStore.Instance.Current.GetMembership(characterId)
            == PartyMembershipState.Member;
        var isNearby = IsNearbyPlayer(playerUuid);
        if (!isSelf && !isPartyMember && !isNearby)
        {
            return;
        }

        var nearbyEntity = isSelf || isNearby ? entity : null;
        if (TryCreateEntry(characterId, nearbyEntity, entity, isSelf, out var entry))
        {
            RosterStore.Upsert(entry);
        }
    }

    private static bool TryCreateEntry(
        long characterId,
        Entity? nearbyEntity,
        Entity? metadataEntity,
        bool isSelf,
        out PlayerRosterEntry entry)
    {
        if (characterId == 0)
        {
            entry = null!;
            return false;
        }

        var source = PlayerDataSourceResolver.Resolve(characterId, nearbyEntity, metadataEntity, isSelf);
        var party = PartyStateStore.Instance.Current;
        var combatAttributes = nearbyEntity is null
            ? default
            : new PlayerCombatAttributes(
                PhysicalAttack: PlayerDataSourceResolver.GetInt(nearbyEntity, "AttrAttack"),
                MagicalAttack: PlayerDataSourceResolver.GetInt(nearbyEntity, "AttrMattack"),
                Strength: PlayerDataSourceResolver.GetInt(nearbyEntity, "AttrStrength"),
                Dexterity: PlayerDataSourceResolver.GetInt(nearbyEntity, "AttrDexterity"),
                Intelligence: PlayerDataSourceResolver.GetInt(nearbyEntity, "AttrIntelligence"),
                Endurance: PlayerDataSourceResolver.GetInt(nearbyEntity, "AttrVitality"),
                Armor: PlayerDataSourceResolver.GetInt(nearbyEntity, "AttrDefense"),
                Critical: PlayerDataSourceResolver.GetInt(nearbyEntity, "AttrCri"),
                CriticalPercent: PlayerDataSourceResolver.GetInt(nearbyEntity, "AttrCrit"),
                Haste: PlayerDataSourceResolver.GetInt(nearbyEntity, "AttrHaste"),
                HastePercent: PlayerDataSourceResolver.GetInt(nearbyEntity, "AttrHastePct"),
                Luck: PlayerDataSourceResolver.GetInt(nearbyEntity, "AttrLuck"),
                LuckPercent: PlayerDataSourceResolver.GetInt(nearbyEntity, "AttrLuckyStrikeProb"),
                Mastery: PlayerDataSourceResolver.GetInt(nearbyEntity, "AttrMastery"),
                MasteryPercent: PlayerDataSourceResolver.GetInt(nearbyEntity, "AttrMasteryPct"),
                Versatility: PlayerDataSourceResolver.GetInt(nearbyEntity, "AttrVersatility"),
                VersatilityPercent: PlayerDataSourceResolver.GetInt(nearbyEntity, "AttrVersatilityPct"),
                BlockPercent: PlayerDataSourceResolver.GetInt(nearbyEntity, "AttrBlockPct"));

        entry = new PlayerRosterEntry(
            source.CharacterId,
            source.Name,
            source.ProfessionId,
            source.CombatPower,
            source.SeasonStrength,
            source.CurrentHp,
            source.MaxHp,
            PlayerClassSpecResolver.FromSubProfessionId(source.SubProfessionId),
            isSelf,
            combatAttributes,
            source.SubProfessionId,
            source.Level,
            source.SeasonLevel,
            source.EquipmentData,
            source.IsNpc,
            nearbyEntity is null ? 0 : Utils.GetCurrentShield(nearbyEntity),
            party.GetMembership(characterId) == PartyMembershipState.Member,
            party.GetPartyNumber(characterId));
        return true;
    }

    private static bool TryGetCharacterEntity(long playerUuid, out Entity entity)
    {
        entity = null!;
        var encounter = EncounterManager.Current;
        if (playerUuid == 0
            || encounter is null
            || !encounter.Entities.TryGetValue(playerUuid, out entity))
        {
            return false;
        }

        return IsCharacterEntity(entity);
    }

    private static bool IsCharacterEntity(Entity entity)
    {
        return entity.EntityType == EEntityType.EntChar
            || Utils.UuidToEntityType(entity.UUID) == (long)EEntityType.EntChar;
    }

    private static bool IsNearbyPlayer(long playerUuid)
    {
        lock (NearbyPlayerSync)
        {
            return NearbyPlayerUuids.Contains(playerUuid);
        }
    }

    private static bool IsSelf(long playerUuid, long characterId)
    {
        return playerUuid == MessageManager.currentUserUuid
            || playerUuid == AppState.PlayerUUID
            || (AppState.PlayerUID != 0 && characterId == AppState.PlayerUID);
    }
}
