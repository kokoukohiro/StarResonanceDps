using StarResonanceDps.Core.Models;
using StarResonanceDps.Core.Services;
using Zproto;

namespace StarResonanceDps.Core.CombatRuntime;

internal static class PlayerRosterProjection
{
    private static readonly object NearbyPlayerSync = new();
    private static readonly object PlayerEntitySync = new();
    private static readonly HashSet<long> NearbyPlayerUuids = [];
    private static readonly Dictionary<long, long> PlayerEntityUuidsByCharacterId = [];
    private static readonly PlayerRosterStore RosterStore = PlayerRosterStore.Instance;

    public static void BeginMap()
    {
        PreserveHumanPartySupplements();

        long[] previousNearbyPlayerUuids;
        lock (NearbyPlayerSync)
        {
            previousNearbyPlayerUuids = [.. NearbyPlayerUuids];
            NearbyPlayerUuids.Clear();
        }

        foreach (var playerUuid in previousNearbyPlayerUuids)
        {
            ClearTransientHumanSubProfession(playerUuid);
        }

        lock (PlayerEntitySync)
        {
            PlayerEntityUuidsByCharacterId.Clear();
        }

        RosterStore.BeginMap();
    }

    public static void UpdateMapName()
    {
        RosterStore.UpdateMapName(EncounterManager.SceneDisplayName);
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
        if (TryGetCharacterEntity(playerUuid, out var entity))
        {
            var characterId = entity.UID != 0
                ? entity.UID
                : Utils.UuidToEntityId(playerUuid);
            var party = PartyStateStore.Instance.Current;
            if (characterId > 0
                && !IsSelf(playerUuid, characterId)
                && party.GetMembership(characterId) == PartyMembershipState.Member
                && TryCreateEntry(characterId, entity, entity, isSelf: false, out var nearbyEntry))
            {
                RefreshPartyMemberSupplementFromNearby(nearbyEntry, entity);
            }
        }

        var removed = false;
        lock (NearbyPlayerSync)
        {
            removed = NearbyPlayerUuids.Remove(playerUuid);
        }

        if (removed)
        {
            ClearTransientHumanSubProfession(playerUuid);
            RebuildRoster();
        }
    }

    public static void ResetNearbyPlayers()
    {
        long[] previousNearbyPlayerUuids;
        lock (NearbyPlayerSync)
        {
            previousNearbyPlayerUuids = [.. NearbyPlayerUuids];
            NearbyPlayerUuids.Clear();
        }

        foreach (var playerUuid in previousNearbyPlayerUuids)
        {
            ClearTransientHumanSubProfession(playerUuid);
        }

        RebuildRoster();
    }

    public static void UpsertPlayer(long playerUuid)
    {
        UpsertPlayer(playerUuid, isSelfHint: false);
    }

    internal static bool TryGetPlayerEntityUuid(long characterId, out long entityUuid)
    {
        lock (PlayerEntitySync)
        {
            return PlayerEntityUuidsByCharacterId.TryGetValue(characterId, out entityUuid);
        }
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
        var entityUuidsByCharacterId = new Dictionary<long, long>();
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
                if (nearbyEntity is not null)
                {
                    RefreshPartyMemberSupplementFromNearby(entry, nearbyEntity);
                }

                entries.Add(entry);
                var resolvedEntityUuid = nearbyEntity?.UUID ?? metadataEntity?.UUID ?? 0;
                if (resolvedEntityUuid != 0)
                {
                    entityUuidsByCharacterId[characterId] = resolvedEntityUuid;
                }
            }
        }

        lock (PlayerEntitySync)
        {
            foreach (var pair in entityUuidsByCharacterId)
            {
                PlayerEntityUuidsByCharacterId[pair.Key] = pair.Value;
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
            if (nearbyEntity is not null)
            {
                RefreshPartyMemberSupplementFromNearby(entry, nearbyEntity);
            }

            lock (PlayerEntitySync)
            {
                PlayerEntityUuidsByCharacterId[characterId] = playerUuid;
            }
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
            source.SubProfessionId > 0
                ? PlayerClassSpecResolver.FromSubProfessionId(source.SubProfessionId)
                : source.IsSpecAbilityUnequipped
                    ? PlayerClassSpec.Rank1
                    : PlayerClassSpec.Unknown,
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

    private static void RefreshPartyMemberSupplementFromNearby(
        PlayerRosterEntry player,
        Entity nearbyEntity)
    {
        var partyStore = PartyStateStore.Instance;
        var party = partyStore.Current;
        if (party.TeamId <= 0
            || !player.IsPartyMember
            || player.IsSelf
            || player.CharacterId <= 0)
        {
            return;
        }

        var knownSupplement = party.TryGetSupplement(player.CharacterId, out var existingSupplement)
            ? existingSupplement
            : new PartyMemberSupplement();
        if (knownSupplement.IsNpc)
        {
            return;
        }

        var observedCurrentHp = nearbyEntity.GetAttrKV("AttrHp") is long currentHpValue
            ? currentHpValue
            : -1;
        var observedMaxHp = nearbyEntity.GetAttrKV("AttrMaxHp") is long maxHpValue
            ? maxHpValue
            : 0;
        var hasCurrentHp = observedCurrentHp >= 0;
        var hasMaxHp = observedMaxHp > 0;

        var name = !string.IsNullOrEmpty(player.Name) ? player.Name : knownSupplement.Name;
        var professionId = player.ProfessionId > 0 ? player.ProfessionId : knownSupplement.ProfessionId;
        var combatPower = player.CombatPower > 0 ? player.CombatPower : knownSupplement.CombatPower;
        var seasonStrength = player.SeasonStrength > 0 ? player.SeasonStrength : knownSupplement.SeasonStrength;
        var level = player.Level > 0 ? player.Level : knownSupplement.Level;
        var seasonLevel = player.SeasonLevel > 0 ? player.SeasonLevel : knownSupplement.SeasonLevel;
        var equipmentData = player.EquipmentData ?? knownSupplement.EquipmentData;
        var currentHp = hasCurrentHp ? observedCurrentHp : knownSupplement.CurrentHp;
        var maxHp = hasMaxHp ? observedMaxHp : knownSupplement.MaxHp;

        // 値が一致していても、まだ「確かなソースで取得済み」の印が付いていなければ書き込む。
        // ここで印を付けておかないと、後から NotifySocialData の古い内容で上書きされてしまう。
        if (knownSupplement.HasTrustedSocialData
            && name == knownSupplement.Name
            && professionId == knownSupplement.ProfessionId
            && combatPower == knownSupplement.CombatPower
            && seasonStrength == knownSupplement.SeasonStrength
            && level == knownSupplement.Level
            && seasonLevel == knownSupplement.SeasonLevel
            && object.Equals(equipmentData, knownSupplement.EquipmentData)
            && currentHp == knownSupplement.CurrentHp
            && maxHp == knownSupplement.MaxHp)
        {
            return;
        }

        partyStore.UpdateSupplement(
            party.TeamId,
            player.CharacterId,
            current => current with
            {
                Name = !string.IsNullOrEmpty(player.Name) ? player.Name : current.Name,
                ProfessionId = player.ProfessionId > 0 ? player.ProfessionId : current.ProfessionId,
                CombatPower = player.CombatPower > 0 ? player.CombatPower : current.CombatPower,
                SeasonStrength = player.SeasonStrength > 0 ? player.SeasonStrength : current.SeasonStrength,
                Level = player.Level > 0 ? player.Level : current.Level,
                SeasonLevel = player.SeasonLevel > 0 ? player.SeasonLevel : current.SeasonLevel,
                EquipmentData = player.EquipmentData ?? current.EquipmentData,
                CurrentHp = hasCurrentHp ? observedCurrentHp : current.CurrentHp,
                MaxHp = hasMaxHp ? observedMaxHp : current.MaxHp,
                // AOIでの実測は最も確かなソース。以後 NotifySocialData の古い内容で上書きさせない。
                HasTrustedSocialData = true
            });
    }

    private static void PreserveHumanPartySupplements()
    {
        var partyStore = PartyStateStore.Instance;
        var party = partyStore.Current;
        if (party.TeamId <= 0)
        {
            return;
        }

        foreach (var player in RosterStore.Current.Entries)
        {
            if (!player.IsPartyMember || player.IsSelf || player.IsNpc || player.CharacterId <= 0)
            {
                continue;
            }

            partyStore.UpdateSupplement(
                party.TeamId,
                player.CharacterId,
                current => current with
                {
                    Name = !string.IsNullOrEmpty(player.Name) ? player.Name : current.Name,
                    ProfessionId = player.ProfessionId > 0 ? player.ProfessionId : current.ProfessionId,
                    CombatPower = player.CombatPower > 0 ? player.CombatPower : current.CombatPower,
                    SeasonStrength = player.SeasonStrength > 0 ? player.SeasonStrength : current.SeasonStrength,
                    Level = player.Level > 0 ? player.Level : current.Level,
                    SeasonLevel = player.SeasonLevel > 0 ? player.SeasonLevel : current.SeasonLevel,
                    EquipmentData = player.EquipmentData ?? current.EquipmentData,
                    CurrentHp = player.MaxHp > 0 ? Math.Max(player.CurrentHp, 0) : current.CurrentHp,
                    MaxHp = player.MaxHp > 0 ? player.MaxHp : current.MaxHp
                });
        }
    }

    private static void ClearTransientHumanSubProfession(long playerUuid)
    {
        var characterId = Utils.UuidToEntityId(playerUuid);
        if (characterId <= 0)
        {
            return;
        }

        var party = PartyStateStore.Instance.Current;
        if (party.TryGetSupplement(characterId, out var supplement) && supplement.IsNpc)
        {
            return;
        }

        var encounter = EncounterManager.Current;
        if (encounter is not null
            && encounter.Entities.TryGetValue(playerUuid, out var entity)
            && IsCharacterEntity(entity))
        {
            entity.SetSubProfessionUnknown();
            return;
        }

        // Entity 側の推定値は破棄するが、保持している補完値は残す。
        // マップ切替で observable でなくなっただけで、特化が変わったわけではない。
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
