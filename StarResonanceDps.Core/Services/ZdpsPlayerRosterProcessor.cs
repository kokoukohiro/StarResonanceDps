using Google.Protobuf;
using StarResonanceDps.Core.Models;
using StarResonanceDps.Core.Protocols.Zdps;

namespace StarResonanceDps.Core.Services;

internal enum ZdpsPlayerRosterProcessResult
{
    NoRelevantData,
    RosterUpdated,
    RosterUnchanged,
    InvalidPayload
}

internal readonly record struct ZdpsPlayerRosterProcessOutcome(
    ZdpsPlayerRosterProcessResult Result,
    int VisibleRosterSize,
    int ChangedPlayerCount,
    bool MapChanged,
    string MapName);

internal sealed class ZdpsPlayerRosterProcessor
{
    private const int AttrSkillId = 100;
    private const int AttrFightPoint = 10030;
    private const int AttrStrength = 11010;
    private const int AttrIntelligence = 11020;
    private const int AttrDexterity = 11030;
    private const int AttrVitality = 11040;
    private const int AttrCritical = 11110;
    private const int AttrHaste = 11120;
    private const int AttrLuck = 11130;
    private const int AttrMastery = 11140;
    private const int AttrVersatility = 11150;
    private const int AttrHp = 11310;
    private const int AttrMaxHp = 11320;
    private const int AttrPhysicalAttack = 11330;
    private const int AttrMagicalAttack = 11340;
    private const int AttrArmor = 11350;
    private const int AttrCriticalPercent = 11710;
    private const int AttrLuckPercent = 11780;
    private const int AttrHastePercent = 11930;
    private const int AttrMasteryPercent = 11940;
    private const int AttrVersatilityPercent = 11950;
    private const int AttrBlockPercent = 11970;
    private const int AttrSeasonStrength = 11440;
    private const int AttrSeasonStrengthTotal = 11441;
    private const int AttrSeasonStrengthAdd = 11442;
    private const int AttrSeasonStrengthExAdd = 11443;
    private const int AttrSeasonStrengthPer = 11444;
    private const int AttrSeasonStrengthExPer = 11445;
    private const int AttrSceneBasicId = 341;

    private readonly object _sync = new();
    private readonly PlayerRosterStore _playerRosterStore;
    private readonly Dictionary<long, PlayerRosterEntry> _playersByCharacterId = [];
    private long _selfCharacterId;
    private uint _currentMapId;
    private bool _hasMapContext;
    private bool _hasDungeonState;
    private bool _isInDungeon;
    private string _currentMapName = string.Empty;

    public ZdpsPlayerRosterProcessor(PlayerRosterStore playerRosterStore)
    {
        _playerRosterStore = playerRosterStore;
    }

    public ZdpsPlayerRosterProcessOutcome ProcessSyncNearEntities(ReadOnlySpan<byte> payload)
    {
        try
        {
            var syncNearEntities = SyncNearEntities.Parser.ParseFrom(payload);
            if (syncNearEntities.Appear.Count == 0 && syncNearEntities.Disappear.Count == 0)
            {
                return CreateOutcome(ZdpsPlayerRosterProcessResult.NoRelevantData, 0, false);
            }

            lock (_sync)
            {
                var changedPlayerCount = 0;
                foreach (var disappearedEntity in syncNearEntities.Disappear)
                {
                    var characterId = GetCharacterId(disappearedEntity.Uuid);
                    if (characterId == 0 || characterId == _selfCharacterId)
                    {
                        continue;
                    }

                    if (_playersByCharacterId.Remove(characterId))
                    {
                        changedPlayerCount++;
                    }
                }

                foreach (var entity in syncNearEntities.Appear)
                {
                    if (entity.EntType != EEntityType.EntChar)
                    {
                        continue;
                    }

                    var characterId = GetCharacterId(entity.Uuid);
                    if (characterId == 0)
                    {
                        continue;
                    }

                    if (UpsertEntityNoLock(entity, characterId == _selfCharacterId))
                    {
                        changedPlayerCount++;
                    }
                }

                return PublishNoLock(changedPlayerCount, false);
            }
        }
        catch (InvalidProtocolBufferException)
        {
            return CreateOutcome(ZdpsPlayerRosterProcessResult.InvalidPayload, 0, false);
        }
    }

    public ZdpsPlayerRosterProcessOutcome ProcessEnterScene(ReadOnlySpan<byte> payload)
    {
        try
        {
            var enterScene = EnterScene.Parser.ParseFrom(payload);
            var sceneInfo = enterScene.EnterSceneInfo;
            if (sceneInfo?.PlayerEnt is null)
            {
                return CreateOutcome(ZdpsPlayerRosterProcessResult.NoRelevantData, 0, false);
            }

            var playerEntity = sceneInfo.PlayerEnt;
            var characterId = GetCharacterId(playerEntity.Uuid);
            if (characterId == 0)
            {
                return CreateOutcome(ZdpsPlayerRosterProcessResult.NoRelevantData, 0, false);
            }

            lock (_sync)
            {
                var mapChanged = TryApplySceneMapNoLock(sceneInfo.SceneAttrs);
                if (_selfCharacterId != 0 && _selfCharacterId != characterId)
                {
                    _playersByCharacterId.Clear();
                    mapChanged = true;
                }

                _selfCharacterId = characterId;
                var changedPlayerCount = UpsertEntityNoLock(playerEntity, true) ? 1 : 0;
                return PublishNoLock(changedPlayerCount, mapChanged);
            }
        }
        catch (InvalidProtocolBufferException)
        {
            return CreateOutcome(ZdpsPlayerRosterProcessResult.InvalidPayload, 0, false);
        }
    }

    public ZdpsPlayerRosterProcessOutcome ProcessSyncContainerData(ReadOnlySpan<byte> payload)
    {
        try
        {
            var syncContainerData = SyncContainerData.Parser.ParseFrom(payload);
            var data = syncContainerData.VData;
            if (data is null || data.CharId == 0)
            {
                return CreateOutcome(ZdpsPlayerRosterProcessResult.NoRelevantData, 0, false);
            }

            lock (_sync)
            {
                var mapChanged = false;
                if (data.SceneData is not null)
                {
                    mapChanged = ApplyMapNoLock(data.SceneData.LevelMapId);
                }

                if (_selfCharacterId != 0 && _selfCharacterId != data.CharId)
                {
                    _playersByCharacterId.Clear();
                    mapChanged = true;
                }

                _selfCharacterId = data.CharId;
                var current = _playersByCharacterId.TryGetValue(data.CharId, out var existing)
                    ? existing
                    : new PlayerRosterEntry(data.CharId, string.Empty, 0, IsSelf: true);
                var next = current with
                {
                    Name = string.IsNullOrWhiteSpace(data.CharBase?.Name)
                        ? current.Name
                        : data.CharBase.Name.TrimEnd(),
                    ProfessionId = data.ProfessionList?.CurProfessionId > 0
                        ? data.ProfessionList.CurProfessionId
                        : current.ProfessionId,
                    CombatPower = data.CharBase?.FightPoint > 0
                        ? data.CharBase.FightPoint
                        : current.CombatPower,
                    CurrentHp = data.Attr?.CurHp > 0
                        ? data.Attr.CurHp
                        : current.CurrentHp,
                    MaxHp = data.Attr?.MaxHp > 0
                        ? data.Attr.MaxHp
                        : current.MaxHp,
                    IsSelf = true
                };

                var changedPlayerCount = 0;
                if (next != current)
                {
                    _playersByCharacterId[data.CharId] = next;
                    changedPlayerCount = 1;
                }

                return PublishNoLock(changedPlayerCount, mapChanged);
            }
        }
        catch (InvalidProtocolBufferException)
        {
            return CreateOutcome(ZdpsPlayerRosterProcessResult.InvalidPayload, 0, false);
        }
    }

    public ZdpsPlayerRosterProcessOutcome ProcessSyncDungeonData(ReadOnlySpan<byte> payload)
    {
        var dungeonContext = ZdpsDungeonContextReader.Read(payload);
        if (dungeonContext.Result == ZdpsDungeonContextReadResult.InvalidPayload)
        {
            return CreateOutcome(ZdpsPlayerRosterProcessResult.InvalidPayload, 0, false);
        }

        if (dungeonContext.Result != ZdpsDungeonContextReadResult.DungeonStateRead)
        {
            return CreateOutcome(ZdpsPlayerRosterProcessResult.NoRelevantData, 0, false);
        }

        lock (_sync)
        {
            var contextChanged = false;
            if (!_hasDungeonState)
            {
                _hasDungeonState = true;
                _isInDungeon = dungeonContext.IsInDungeon;
                contextChanged = dungeonContext.IsInDungeon;
            }
            else if (_isInDungeon != dungeonContext.IsInDungeon)
            {
                _isInDungeon = dungeonContext.IsInDungeon;
                contextChanged = true;
            }

            if (contextChanged)
            {
                _playersByCharacterId.Clear();
            }

            return PublishNoLock(0, contextChanged);
        }
    }

    public void Reset()
    {
        lock (_sync)
        {
            _playersByCharacterId.Clear();
            _selfCharacterId = 0;
            _currentMapId = 0;
            _hasMapContext = false;
            _hasDungeonState = false;
            _isInDungeon = false;
            _currentMapName = string.Empty;
            _playerRosterStore.Clear();
        }
    }

    private bool UpsertEntityNoLock(Entity entity, bool isSelf)
    {
        var characterId = GetCharacterId(entity.Uuid);
        if (characterId == 0)
        {
            return false;
        }

        var attributes = entity.Attrs?.Attrs;
        if (attributes is null || attributes.Count == 0)
        {
            return false;
        }

        var current = _playersByCharacterId.TryGetValue(characterId, out var existing)
            ? existing
            : new PlayerRosterEntry(characterId, string.Empty, 0, IsSelf: isSelf);
        var combatAttributes = current.CombatAttributes;

        string? name = null;
        int? professionId = null;
        int? combatPower = null;
        int? seasonStrength = null;
        long? currentHp = null;
        long? maxHp = null;
        PlayerClassSpec? classSpec = null;
        var hasCombatAttributes = false;

        foreach (var attribute in attributes)
        {
            if (attribute.RawData.Length == 0)
            {
                continue;
            }

            var reader = new CodedInputStream(attribute.RawData.ToByteArray());
            switch (attribute.Id)
            {
                case (int)EAttrType.AttrName:
                    name = reader.ReadString().TrimEnd();
                    break;

                case (int)EAttrType.AttrProfessionId:
                    professionId = reader.ReadInt32();
                    break;

                case AttrFightPoint:
                    combatPower = reader.ReadInt32();
                    break;

                case AttrSeasonStrength:
                case AttrSeasonStrengthTotal:
                case AttrSeasonStrengthAdd:
                case AttrSeasonStrengthExAdd:
                case AttrSeasonStrengthPer:
                case AttrSeasonStrengthExPer:
                    seasonStrength = reader.ReadInt32();
                    break;

                case AttrHp:
                    currentHp = reader.ReadInt64();
                    break;

                case AttrMaxHp:
                    maxHp = reader.ReadInt64();
                    break;

                case AttrSkillId:
                    var resolvedClassSpec = PlayerClassSpecResolver.FromSkillId(reader.ReadInt32());
                    if (resolvedClassSpec != PlayerClassSpec.Unknown)
                    {
                        classSpec = resolvedClassSpec;
                    }

                    break;

                case AttrPhysicalAttack:
                    combatAttributes = combatAttributes with { PhysicalAttack = reader.ReadInt32() };
                    hasCombatAttributes = true;
                    break;

                case AttrMagicalAttack:
                    combatAttributes = combatAttributes with { MagicalAttack = reader.ReadInt32() };
                    hasCombatAttributes = true;
                    break;

                case AttrStrength:
                    combatAttributes = combatAttributes with { Strength = reader.ReadInt32() };
                    hasCombatAttributes = true;
                    break;

                case AttrDexterity:
                    combatAttributes = combatAttributes with { Dexterity = reader.ReadInt32() };
                    hasCombatAttributes = true;
                    break;

                case AttrIntelligence:
                    combatAttributes = combatAttributes with { Intelligence = reader.ReadInt32() };
                    hasCombatAttributes = true;
                    break;

                case AttrVitality:
                    combatAttributes = combatAttributes with { Endurance = reader.ReadInt32() };
                    hasCombatAttributes = true;
                    break;

                case AttrArmor:
                    combatAttributes = combatAttributes with { Armor = reader.ReadInt32() };
                    hasCombatAttributes = true;
                    break;

                case AttrCritical:
                    combatAttributes = combatAttributes with { Critical = reader.ReadInt32() };
                    hasCombatAttributes = true;
                    break;

                case AttrCriticalPercent:
                    combatAttributes = combatAttributes with { CriticalPercent = reader.ReadInt32() };
                    hasCombatAttributes = true;
                    break;

                case AttrHaste:
                    combatAttributes = combatAttributes with { Haste = reader.ReadInt32() };
                    hasCombatAttributes = true;
                    break;

                case AttrHastePercent:
                    combatAttributes = combatAttributes with { HastePercent = reader.ReadInt32() };
                    hasCombatAttributes = true;
                    break;

                case AttrLuck:
                    combatAttributes = combatAttributes with { Luck = reader.ReadInt32() };
                    hasCombatAttributes = true;
                    break;

                case AttrLuckPercent:
                    combatAttributes = combatAttributes with { LuckPercent = reader.ReadInt32() };
                    hasCombatAttributes = true;
                    break;

                case AttrMastery:
                    combatAttributes = combatAttributes with { Mastery = reader.ReadInt32() };
                    hasCombatAttributes = true;
                    break;

                case AttrMasteryPercent:
                    combatAttributes = combatAttributes with { MasteryPercent = reader.ReadInt32() };
                    hasCombatAttributes = true;
                    break;

                case AttrVersatility:
                    combatAttributes = combatAttributes with { Versatility = reader.ReadInt32() };
                    hasCombatAttributes = true;
                    break;

                case AttrVersatilityPercent:
                    combatAttributes = combatAttributes with { VersatilityPercent = reader.ReadInt32() };
                    hasCombatAttributes = true;
                    break;

                case AttrBlockPercent:
                    combatAttributes = combatAttributes with { BlockPercent = reader.ReadInt32() };
                    hasCombatAttributes = true;
                    break;
            }
        }

        if (name is null
            && professionId is null
            && combatPower is null
            && seasonStrength is null
            && currentHp is null
            && maxHp is null
            && classSpec is null
            && !hasCombatAttributes)
        {
            return false;
        }

        var next = current with
        {
            Name = name ?? current.Name,
            ProfessionId = professionId ?? current.ProfessionId,
            CombatPower = combatPower ?? current.CombatPower,
            SeasonStrength = seasonStrength ?? current.SeasonStrength,
            CurrentHp = currentHp ?? current.CurrentHp,
            MaxHp = maxHp ?? current.MaxHp,
            ClassSpec = classSpec ?? current.ClassSpec,
            IsSelf = isSelf || current.IsSelf,
            CombatAttributes = hasCombatAttributes ? combatAttributes : current.CombatAttributes
        };

        if (next == current)
        {
            return false;
        }

        _playersByCharacterId[characterId] = next;
        return true;
    }

    private bool TryApplySceneMapNoLock(AttrCollection? sceneAttributes)
    {
        if (sceneAttributes?.Attrs is null)
        {
            return false;
        }

        foreach (var attribute in sceneAttributes.Attrs)
        {
            if (attribute.Id != AttrSceneBasicId || attribute.RawData.Length == 0)
            {
                continue;
            }

            var reader = new CodedInputStream(attribute.RawData.ToByteArray());
            return ApplyMapNoLock(reader.ReadUInt32());
        }

        return false;
    }

    private bool ApplyMapNoLock(uint mapId)
    {
        if (mapId == 0
            || (_hasMapContext && _currentMapId == mapId))
        {
            return false;
        }

        _playersByCharacterId.Clear();
        _currentMapId = mapId;
        _currentMapName = ZdpsSceneNameResolver.Resolve(mapId);
        _hasMapContext = true;
        return true;
    }

    private ZdpsPlayerRosterProcessOutcome PublishNoLock(int changedPlayerCount, bool mapChanged)
    {
        var visiblePlayers = _playersByCharacterId.Values
            .Where(player => !string.IsNullOrWhiteSpace(player.Name))
            .OrderByDescending(player => player.IsSelf)
            .ToArray();

        if (changedPlayerCount == 0 && !mapChanged)
        {
            return CreateOutcome(ZdpsPlayerRosterProcessResult.RosterUnchanged, 0, false, visiblePlayers.Length);
        }

        _playerRosterStore.Replace(visiblePlayers, _currentMapName);
        return CreateOutcome(ZdpsPlayerRosterProcessResult.RosterUpdated, changedPlayerCount, mapChanged, visiblePlayers.Length);
    }

    private ZdpsPlayerRosterProcessOutcome CreateOutcome(
        ZdpsPlayerRosterProcessResult result,
        int changedPlayerCount,
        bool mapChanged,
        int? visibleRosterSize = null)
    {
        var size = visibleRosterSize ?? _playersByCharacterId.Values.Count(player => !string.IsNullOrWhiteSpace(player.Name));
        return new ZdpsPlayerRosterProcessOutcome(result, size, changedPlayerCount, mapChanged, _currentMapName);
    }

    private static long GetCharacterId(long entityUuid)
    {
        return entityUuid >> 16;
    }
}
