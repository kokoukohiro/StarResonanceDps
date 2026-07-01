using Google.Protobuf;
using StarResonanceDps.Core.Models;
using StarResonanceDps.Core.Services;
using AoiSyncDelta = Zproto.AoiSyncDelta;
using Attr = Zproto.Attr;
using CharSerialize = Zproto.CharSerialize;
using DungeonSyncData = Zproto.DungeonSyncData;
using EDamageType = Zproto.EDamageType;
using EDungeonState = Zproto.EDungeonState;
using EnterScene = Zproto.WorldNtfCsharp.Types.EnterScene;
using Entity = Zproto.Entity;
using GameProtocolValueReader = StarResonanceDps.Core.Protocols.Game.GameProtocolValueReader;
using SceneData = Zproto.SceneData;
using SyncDamageInfo = Zproto.SyncDamageInfo;
using SyncNearDeltaInfo = Zproto.WorldNtfCsharp.Types.SyncNearDeltaInfo;
using SyncNearEntities = Zproto.WorldNtfCsharp.Types.SyncNearEntities;
using SyncToMeDeltaInfo = Zproto.WorldNtfCsharp.Types.SyncToMeDeltaInfo;
using TempAttr = Zproto.TempAttr;
using DirtyContainer = StarResonanceDps.Core.Protocols.Game.Binary.CharSerialize;
using DirtyDungeon = StarResonanceDps.Core.Protocols.Game.Binary.DungeonDirtyData;

namespace StarResonanceDps.Core.Combat;

public sealed class GameCombatStore
{
    private const int EntityTypeCharacter = 10;

    private const int AttrName = 1;
    private const int AttrSkillId = 100;
    private const int AttrProfessionId = 220;
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


    private static readonly Lazy<GameCombatStore> LazyInstance = new(() => new GameCombatStore(PlayerRosterStore.Instance));

    private readonly object _sync = new();
    private readonly PlayerRosterStore _playerRosterStore;
    private readonly List<GameCombatSnapshot> _history = [];
    private MutableDungeonEntry _current = MutableDungeonEntry.CreateNew(0, string.Empty);
    private long _selfCharacterId;
    private long _selfUuid;
    private bool _allowSceneUpdate = true;

    private GameCombatStore(PlayerRosterStore playerRosterStore)
    {
        _playerRosterStore = playerRosterStore;
    }

    public static GameCombatStore Instance => LazyInstance.Value;

    public event EventHandler<GameCombatSnapshotChangedEventArgs>? SnapshotChanged;

    public GameCombatSnapshot Current
    {
        get
        {
            lock (_sync)
            {
                return CreateSnapshotNoLock(_current);
            }
        }
    }

    public IReadOnlyList<GameCombatSnapshot> History
    {
        get
        {
            lock (_sync)
            {
                return _history.ToArray();
            }
        }
    }

    public void Reset()
    {
        PublishWork? work;
        lock (_sync)
        {
            _history.Clear();
            _selfCharacterId = 0;
            _selfUuid = 0;
            _allowSceneUpdate = true;
            _current = MutableDungeonEntry.CreateNew(0, string.Empty);
            work = CreatePublishWorkNoLock();
        }

        Publish(work);
    }


    public void StartNewMap()
    {
        PublishWork? work;
        lock (_sync)
        {
            CompleteCurrentDungeonEntryNoLock(DateTimeOffset.UtcNow);
            _current = MutableDungeonEntry.CreateNew(_current.LevelMapId, _current.MapName);
            _allowSceneUpdate = true;
            work = CreatePublishWorkNoLock();
        }

        Publish(work);
    }

    internal void ApplySocialScene(SceneData? scene)
    {
        if (scene is null || scene.LevelMapId == 0)
        {
            return;
        }

        PublishWork? work = null;
        lock (_sync)
        {
            CaptureProtocolNoLock("NotifySocialData.SceneData", scene);
            if (_allowSceneUpdate)
            {
                SetSceneNoLock(scene.LevelMapId, scene.LineId);
                _allowSceneUpdate = false;
                work = CreatePublishWorkNoLock();
            }
        }

        Publish(work);
    }

    internal void ApplyEnterScene(EnterScene message)
    {
        if (message.EnterSceneInfo?.PlayerEnt is null)
        {
            return;
        }

        PublishWork work;
        lock (_sync)
        {
            CaptureProtocolNoLock("EnterScene", message);
            ApplyEntityNoLock(message.EnterSceneInfo.PlayerEnt);
            work = CreatePublishWorkNoLock();
        }

        Publish(work);
    }

    internal void ApplyNearbyEntities(SyncNearEntities message)
    {
        PublishWork? work = null;
        lock (_sync)
        {
            CaptureProtocolNoLock("SyncNearEntities", message);
            foreach (var entity in message.Appear)
            {
                if (UuidToEntityId(entity.Uuid) == 0)
                {
                    continue;
                }

                ApplyEntityNoLock(entity);
                work = CreatePublishWorkNoLock();
            }
        }

        Publish(work);
    }

    internal void ApplyContainer(CharSerialize container)
    {
        if (container is null || container.CharId == 0)
        {
            return;
        }

        PublishWork work;
        lock (_sync)
        {
            CaptureProtocolNoLock("SyncContainerData", container);
            _current.ContainerData = container.Clone();
            if (container.SceneData is not null && container.SceneData.LevelMapId != 0)
            {
                SetSceneNoLock(container.SceneData.LevelMapId, container.SceneData.LineId);
            }

            _selfCharacterId = container.CharId;
            var self = GetOrCreateEntityNoLock(EntityIdToUuid(container.CharId, EntityTypeCharacter), EntityTypeCharacter);
            self.CharacterId = container.CharId;
            self.EntityType = EntityTypeCharacter;
            self.IsSelf = true;
            self.IsRosterVisible = true;
            self.ProtocolEntity = null;

            if (!string.IsNullOrWhiteSpace(container.CharBase?.Name))
            {
                self.Name = container.CharBase.Name;
            }

            if (container.ProfessionList is not null && container.ProfessionList.CurProfessionId != 0)
            {
                self.ProfessionId = container.ProfessionList.CurProfessionId;
            }

            if (container.CharBase is not null && container.CharBase.FightPoint != 0)
            {
                self.CombatPower = container.CharBase.FightPoint;
            }

            if (container.Attr is not null)
            {
                if (container.Attr.CurHp != 0)
                {
                    self.CurrentHp = container.Attr.CurHp;
                }

                if (container.Attr.MaxHp != 0)
                {
                    self.MaxHp = container.Attr.MaxHp;
                }
            }

            RefreshSelfFlagsNoLock();
            work = CreatePublishWorkNoLock();
        }

        Publish(work);
    }

    internal void ApplyContainerDirty(DirtyContainer dirty)
    {
        if (dirty is null)
        {
            return;
        }

        PublishWork? work = null;
        lock (_sync)
        {
            _current.ContainerDirtyData = dirty;
            if (dirty.CharId is int characterId && characterId != 0)
            {
                _selfCharacterId = characterId;
            }

            var selfUuid = _selfUuid != 0 ? _selfUuid : _selfCharacterId == 0 ? 0 : EntityIdToUuid(_selfCharacterId, EntityTypeCharacter);
            if (selfUuid != 0)
            {
                var self = GetOrCreateEntityNoLock(selfUuid, EntityTypeCharacter);
                self.CharacterId = _selfCharacterId == 0 ? UuidToEntityId(selfUuid) : _selfCharacterId;
                self.IsSelf = true;
                self.IsRosterVisible = true;
                if (!string.IsNullOrWhiteSpace(dirty.CharBaseInfo?.Name)) self.Name = dirty.CharBaseInfo.Name;
                if (dirty.ProfessionList?.CurProfessionId is int professionId && professionId != 0) self.ProfessionId = professionId;
                if (dirty.FightPoint?.TotalFightPoint is int combatPower && combatPower != 0) self.CombatPower = combatPower;
                if (dirty.Attr?.CurHp is long currentHp && currentHp != 0) self.CurrentHp = currentHp;
                if (dirty.Attr?.MaxHp is long maxHp && maxHp != 0) self.MaxHp = maxHp;
                if (dirty.SceneData?.LevelMapId is uint mapId && mapId != 0) SetSceneNoLock(mapId, dirty.SceneData.LineId ?? 0);
                RefreshSelfFlagsNoLock();
                work = CreatePublishWorkNoLock();
            }
        }

        Publish(work);
    }

    internal void ApplyDungeon(DungeonSyncData dungeon)
    {
        if (dungeon is null)
        {
            return;
        }

        PublishWork work;
        lock (_sync)
        {
            CaptureProtocolNoLock("SyncDungeonData", dungeon);
            _current.DungeonData = dungeon.Clone();
            _current.DungeonState = dungeon.FlowInfo is null ? 0 : (int)dungeon.FlowInfo.State;
            ReplaceDungeonDamageTotalsNoLock(dungeon.Damage?.Damages);
            work = CreatePublishWorkNoLock();
        }

        Publish(work);
    }

    internal void ApplyDungeonDirty(DirtyDungeon dirty)
    {
        if (dirty is null)
        {
            return;
        }

        PublishWork work;
        lock (_sync)
        {
            _current.DungeonDirtyData = dirty;
            if (dirty.FlowInfo?.State is EDungeonState state)
            {
                _current.DungeonState = (int)state;
            }

            ReplaceDungeonDamageTotalsNoLock(dirty.Damage?.Damages);
            work = CreatePublishWorkNoLock();
        }

        Publish(work);
    }

    internal int ApplyDeltas(SyncNearDeltaInfo message, DateTimeOffset receivedAtUtc)
    {
        var eventCount = 0;
        PublishWork? work = null;
        lock (_sync)
        {
            CaptureProtocolNoLock("SyncNearDeltaInfo", message);
            foreach (var delta in message.DeltaInfos)
            {
                eventCount += ApplyDeltaNoLock(delta, receivedAtUtc);
            }

            if (message.DeltaInfos.Count != 0)
            {
                work = CreatePublishWorkNoLock();
            }
        }

        Publish(work);
        return eventCount;
    }

    internal int ApplySelfDelta(SyncToMeDeltaInfo message, DateTimeOffset receivedAtUtc)
    {
        var eventCount = 0;
        PublishWork? work = null;
        lock (_sync)
        {
            CaptureProtocolNoLock("SyncToMeDeltaInfo", message);
            if (message.DeltaInfo is not null)
            {
                if (message.DeltaInfo.Uuid != 0)
                {
                    _selfUuid = message.DeltaInfo.Uuid;
                    _selfCharacterId = UuidToEntityId(_selfUuid);
                    RefreshSelfFlagsNoLock();
                }

                if (message.DeltaInfo.BaseDelta is not null)
                {
                    eventCount = ApplyDeltaNoLock(message.DeltaInfo.BaseDelta, receivedAtUtc);
                }

                work = CreatePublishWorkNoLock();
            }
        }

        Publish(work);
        return eventCount;
    }

    internal void CaptureAuxiliaryProtocolMessage(string source, IMessage message)
    {
        if (message is null)
        {
            return;
        }

        lock (_sync)
        {
            CaptureProtocolNoLock(source, message);
        }
    }

    private void ApplyEntityNoLock(Entity entity)
    {
        if (entity.Uuid == 0)
        {
            return;
        }

        var state = GetOrCreateEntityNoLock(entity.Uuid, (int)entity.EntType);
        state.ProtocolEntity = entity.Clone();
        state.IsSelf = IsSelfNoLock(state);
        state.IsRosterVisible = true;
        ApplyAttributesNoLock(state, entity.Attrs?.Attrs);
        ApplyTempAttributesNoLock(state, entity.TempAttrs?.Attrs);
    }

    private int ApplyDeltaNoLock(AoiSyncDelta delta, DateTimeOffset receivedAtUtc)
    {
        if (delta is null || delta.Uuid == 0)
        {
            return 0;
        }

        var target = GetOrCreateEntityNoLock(delta.Uuid, GetEntityTypeFromUuid(delta.Uuid));
        target.LastDelta = delta.Clone();
        ApplyAttributesNoLock(target, delta.Attrs?.Attrs);
        ApplyTempAttributesNoLock(target, delta.TempAttrs?.Attrs);

        var count = 0;
        if (delta.SkillEffects is not null)
        {
            foreach (var damage in delta.SkillEffects.Damages)
            {
                RecordDamageNoLock(delta.Uuid, damage, receivedAtUtc);
                count++;
            }
        }

        return count;
    }

    private void RecordDamageNoLock(long targetUuid, SyncDamageInfo source, DateTimeOffset occurredAtUtc)
    {
        var attackerUuid = source.TopSummonerId != 0 ? source.TopSummonerId : source.AttackerUuid;
        if (attackerUuid == 0)
        {
            return;
        }

        var value = source.Value != 0 ? source.Value : source.LuckyValue;
        if (value < 0)
        {
            value = source.HpLessenValue > 0 ? source.HpLessenValue : 0;
        }

        var isHeal = source.Type == EDamageType.Heal;
        var isCritical = source.IsCrit || (source.TypeFlag & 1) != 0;
        var isLucky = source.LuckyValue != 0;
        var effectiveValue = source.HpLessenValue > 0 ? source.HpLessenValue : value;
        var attacker = GetOrCreateEntityNoLock(attackerUuid, GetEntityTypeFromUuid(attackerUuid));
        var target = GetOrCreateEntityNoLock(targetUuid, GetEntityTypeFromUuid(targetUuid));
        StartDungeonEntryActivityNoLock(occurredAtUtc);
        _current.Events.Add(new GameCombatEvent(
            occurredAtUtc,
            isHeal ? GameCombatEventKind.Healing : GameCombatEventKind.Damage,
            attackerUuid,
            targetUuid,
            source.OwnerId,
            source.OwnerLevel,
            value,
            source.HpLessenValue,
            source.ShieldLessenValue,
            (int)source.Type,
            (int)source.Property,
            isCritical,
            isLucky,
            source.IsMiss,
            source.IsDead));

        if (isHeal)
        {
            attacker.HealingDone += value;
            attacker.EffectiveHealingDone += effectiveValue;
            attacker.RecordSkill(source.OwnerId, 0, value, 0, isCritical, isLucky, source.IsMiss);
            target.HealingTaken += value;
            return;
        }

        if (attackerUuid != targetUuid)
        {
            attacker.DamageDone += value;
            attacker.EffectiveDamageDone += effectiveValue;
            attacker.RecordSkill(source.OwnerId, value, 0, 0, isCritical, isLucky, source.IsMiss);
        }

        target.DamageTaken += value;
        target.EffectiveDamageTaken += effectiveValue;
        target.RecordSkill(source.OwnerId, 0, 0, value, isCritical, isLucky, source.IsMiss);
    }

    private static void ApplyTempAttributesNoLock(CombatantState state, IEnumerable<TempAttr>? attributes)
    {
        if (attributes is null)
        {
            return;
        }

        foreach (var attribute in attributes)
        {
            state.TempAttributes[attribute.Id] = attribute.Value;
        }
    }

    private void ReplaceDungeonDamageTotalsNoLock(IEnumerable<KeyValuePair<long, long>>? totals)
    {
        if (totals is null)
        {
            return;
        }

        _current.DungeonDamageTotals = totals.ToDictionary(pair => pair.Key, pair => pair.Value);
        foreach (var total in _current.DungeonDamageTotals)
        {
            var entity = _current.Entities.Values.FirstOrDefault(value => value.CharacterId == total.Key);
            if (entity is not null)
            {
                entity.ServerDungeonDamageTotal = total.Value;
            }
        }
    }

    private void SetSceneNoLock(uint levelMapId, uint lineId)
    {
        _current.LevelMapId = levelMapId;
        _current.MapName = GameSceneNameResolver.Resolve(levelMapId);
        _current.ChannelLine = lineId;
    }

    private void CaptureProtocolNoLock(string source, IMessage message)
    {
        _current.ProtocolFrames[source] = message.ToByteArray();
    }

    private void ApplyAttributesNoLock(CombatantState state, IEnumerable<Attr>? attributes)
    {
        if (attributes is null)
        {
            return;
        }

        foreach (var attribute in attributes)
        {
            if (attribute.Id == 0)
            {
                continue;
            }
            state.RawAttributes[attribute.Id] = attribute.RawData.ToByteArray();
            switch (attribute.Id)
            {
                case AttrName:
                    var name = GameProtocolValueReader.ReadRawString(attribute.RawData);
                    if (!string.IsNullOrWhiteSpace(name))
                    {
                        state.Name = name;
                    }
                    break;

                case AttrProfessionId:
                    state.ProfessionId = GameProtocolValueReader.ReadRawInt32(attribute.RawData);
                    break;

                case AttrSkillId:
                    var classSpec = PlayerClassSpecResolver.FromSkillId(GameProtocolValueReader.ReadRawInt32(attribute.RawData));
                    if (classSpec != PlayerClassSpec.Unknown)
                    {
                        state.ClassSpec = classSpec;
                    }
                    break;

                case AttrFightPoint:
                    state.CombatPower = GameProtocolValueReader.ReadRawInt32(attribute.RawData);
                    break;

                case AttrHp:
                    state.CurrentHp = GameProtocolValueReader.ReadRawInt64(attribute.RawData);
                    break;

                case AttrMaxHp:
                    state.MaxHp = GameProtocolValueReader.ReadRawInt64(attribute.RawData);
                    break;

                case AttrStrength:
                    state.CombatAttributes = state.CombatAttributes with { Strength = GameProtocolValueReader.ReadRawInt32(attribute.RawData) };
                    break;

                case AttrIntelligence:
                    state.CombatAttributes = state.CombatAttributes with { Intelligence = GameProtocolValueReader.ReadRawInt32(attribute.RawData) };
                    break;

                case AttrDexterity:
                    state.CombatAttributes = state.CombatAttributes with { Dexterity = GameProtocolValueReader.ReadRawInt32(attribute.RawData) };
                    break;

                case AttrVitality:
                    state.CombatAttributes = state.CombatAttributes with { Endurance = GameProtocolValueReader.ReadRawInt32(attribute.RawData) };
                    break;

                case AttrCritical:
                    state.CombatAttributes = state.CombatAttributes with { Critical = GameProtocolValueReader.ReadRawInt32(attribute.RawData) };
                    break;

                case AttrHaste:
                    state.CombatAttributes = state.CombatAttributes with { Haste = GameProtocolValueReader.ReadRawInt32(attribute.RawData) };
                    break;

                case AttrLuck:
                    state.CombatAttributes = state.CombatAttributes with { Luck = GameProtocolValueReader.ReadRawInt32(attribute.RawData) };
                    break;

                case AttrMastery:
                    state.CombatAttributes = state.CombatAttributes with { Mastery = GameProtocolValueReader.ReadRawInt32(attribute.RawData) };
                    break;

                case AttrVersatility:
                    state.CombatAttributes = state.CombatAttributes with { Versatility = GameProtocolValueReader.ReadRawInt32(attribute.RawData) };
                    break;

                case AttrPhysicalAttack:
                    state.CombatAttributes = state.CombatAttributes with { PhysicalAttack = GameProtocolValueReader.ReadRawInt32(attribute.RawData) };
                    break;

                case AttrMagicalAttack:
                    state.CombatAttributes = state.CombatAttributes with { MagicalAttack = GameProtocolValueReader.ReadRawInt32(attribute.RawData) };
                    break;

                case AttrArmor:
                    state.CombatAttributes = state.CombatAttributes with { Armor = GameProtocolValueReader.ReadRawInt32(attribute.RawData) };
                    break;

                case AttrCriticalPercent:
                    state.CombatAttributes = state.CombatAttributes with { CriticalPercent = GameProtocolValueReader.ReadRawInt32(attribute.RawData) };
                    break;

                case AttrLuckPercent:
                    state.CombatAttributes = state.CombatAttributes with { LuckPercent = GameProtocolValueReader.ReadRawInt32(attribute.RawData) };
                    break;

                case AttrHastePercent:
                    state.CombatAttributes = state.CombatAttributes with { HastePercent = GameProtocolValueReader.ReadRawInt32(attribute.RawData) };
                    break;

                case AttrMasteryPercent:
                    state.CombatAttributes = state.CombatAttributes with { MasteryPercent = GameProtocolValueReader.ReadRawInt32(attribute.RawData) };
                    break;

                case AttrVersatilityPercent:
                    state.CombatAttributes = state.CombatAttributes with { VersatilityPercent = GameProtocolValueReader.ReadRawInt32(attribute.RawData) };
                    break;

                case AttrBlockPercent:
                    state.CombatAttributes = state.CombatAttributes with { BlockPercent = GameProtocolValueReader.ReadRawInt32(attribute.RawData) };
                    break;

                case AttrSeasonStrength:
                case AttrSeasonStrengthTotal:
                case AttrSeasonStrengthAdd:
                case AttrSeasonStrengthExAdd:
                case AttrSeasonStrengthPer:
                case AttrSeasonStrengthExPer:
                    state.SeasonStrength = GameProtocolValueReader.ReadRawInt32(attribute.RawData);
                    break;
            }
        }
    }


    private CombatantState GetOrCreateEntityNoLock(long uuid, int entityType)
    {
        if (_current.Entities.TryGetValue(uuid, out var existing))
        {
            if (entityType != 0)
            {
                existing.EntityType = entityType;
            }

            existing.IsSelf = IsSelfNoLock(existing);
            return existing;
        }

        var created = new CombatantState(uuid, entityType == 0 ? GetEntityTypeFromUuid(uuid) : entityType)
        {
            CharacterId = UuidToEntityId(uuid)
        };
        created.IsSelf = IsSelfNoLock(created);
        _current.Entities.Add(uuid, created);
        return created;
    }

    private bool IsSelfNoLock(CombatantState entity)
    {
        return (_selfUuid != 0 && entity.EntityUuid == _selfUuid)
            || (_selfCharacterId != 0 && entity.CharacterId == _selfCharacterId);
    }

    private void RefreshSelfFlagsNoLock()
    {
        foreach (var entity in _current.Entities.Values)
        {
            entity.IsSelf = IsSelfNoLock(entity);
        }
    }

    private static bool IsCharacterEntity(CombatantState entity)
    {
        return entity.IsSelf
            || entity.EntityType == EntityTypeCharacter
            || GetEntityTypeFromUuid(entity.EntityUuid) == EntityTypeCharacter;
    }

    private void StartDungeonEntryActivityNoLock(DateTimeOffset occurredAtUtc)
    {
        if (_current.Phase == GameDungeonEntryPhase.Idle)
        {
            _current.Phase = GameDungeonEntryPhase.Active;
            _current.StartedAtUtc = occurredAtUtc;
        }
    }

    internal void SetDungeonState(int state)
    {
        PublishWork? work;
        lock (_sync)
        {
            _current.DungeonState = state;
            work = CreatePublishWorkNoLock();
        }

        Publish(work);
    }

    internal bool HasRecordedCombatStatistics()
    {
        lock (_sync)
        {
            return HasRecordedCombatStatisticsNoLock();
        }
    }

    internal void StartNewDungeonEntry(bool force, DateTimeOffset occurredAtUtc)
    {
        PublishWork? work;
        lock (_sync)
        {
            StartNewDungeonEntryNoLock(force, occurredAtUtc);
            work = CreatePublishWorkNoLock();
        }

        Publish(work);
    }

    internal void CompleteDungeonEntry(DateTimeOffset completedAtUtc)
    {
        PublishWork? work;
        lock (_sync)
        {
            CompleteCurrentDungeonEntryNoLock(completedAtUtc);
            work = CreatePublishWorkNoLock();
        }

        Publish(work);
    }

    private bool HasRecordedCombatStatisticsNoLock()
    {
        return _current.Entities.Values.Any(entity => entity.DamageDone > 0 || entity.HealingDone > 0 || entity.DamageTaken > 0);
    }

    private void StartNewDungeonEntryNoLock(bool force, DateTimeOffset occurredAtUtc)
    {
        var hasStatistics = HasRecordedCombatStatisticsNoLock();
        if (force || (_current.Phase != GameDungeonEntryPhase.Completed && hasStatistics))
        {
            CompleteCurrentDungeonEntryNoLock(occurredAtUtc);
            _current = MutableDungeonEntry.CreateNew(_current.LevelMapId, _current.MapName);
            return;
        }

        if (_current.Phase == GameDungeonEntryPhase.Completed)
        {
            _current = MutableDungeonEntry.CreateNew(_current.LevelMapId, _current.MapName);
            return;
        }

        _current.StartedAtUtc = occurredAtUtc;
        _current.CompletedAtUtc = null;
        _current.Phase = GameDungeonEntryPhase.Idle;
    }

    private void CompleteCurrentDungeonEntryNoLock(DateTimeOffset completedAtUtc)
    {
        if (_current.Phase != GameDungeonEntryPhase.Active)
        {
            return;
        }

        _current.Phase = GameDungeonEntryPhase.Completed;
        _current.CompletedAtUtc = completedAtUtc;
        _history.Add(CreateSnapshotNoLock(_current));
    }

    private PublishWork CreatePublishWorkNoLock()
    {
        return new PublishWork(
            CreateSnapshotNoLock(_current),
            CreateRosterNoLock(),
            _current.MapName);
    }

    private GameCombatSnapshot CreateSnapshotNoLock(MutableDungeonEntry dungeonEntry)
    {
        var combatants = dungeonEntry.Entities.Values
            .OrderByDescending(entity => entity.DamageDone)
            .ThenBy(entity => entity.Name, StringComparer.Ordinal)
            .Select(entity => entity.CreateSnapshot())
            .ToArray();

        return new GameCombatSnapshot(
            dungeonEntry.DungeonEntryId,
            dungeonEntry.Phase,
            dungeonEntry.CreatedAtUtc,
            dungeonEntry.StartedAtUtc,
            dungeonEntry.CompletedAtUtc,
            dungeonEntry.LevelMapId,
            dungeonEntry.MapName,
            dungeonEntry.DungeonState,
            combatants.Sum(combatant => combatant.DamageDone),
            combatants.Sum(combatant => combatant.HealingDone),
            combatants.Sum(combatant => combatant.DamageTaken),
            combatants,
            dungeonEntry.Events.ToArray(),
            dungeonEntry.DungeonDamageTotals.ToDictionary(pair => pair.Key, pair => pair.Value));
    }

    private IReadOnlyList<PlayerRosterEntry> CreateRosterNoLock()
    {
        return _current.Entities.Values
            .Where(entity => entity.IsRosterVisible)
            .Where(IsCharacterEntity)
            .Where(entity => entity.CharacterId != 0)
            .Where(entity => !string.IsNullOrWhiteSpace(entity.Name) || IsSelfNoLock(entity))
            .OrderByDescending(entity => IsSelfNoLock(entity))
            .ThenBy(entity => entity.Name, StringComparer.Ordinal)
            .Select(entity => new PlayerRosterEntry(
                entity.CharacterId,
                entity.Name,
                entity.ProfessionId,
                entity.CombatPower,
                entity.SeasonStrength,
                entity.CurrentHp,
                entity.MaxHp,
                entity.ClassSpec,
                IsSelfNoLock(entity),
                entity.CombatAttributes))
            .ToArray();
    }

    private void Publish(PublishWork? work)
    {
        if (work is null)
        {
            return;
        }

        _playerRosterStore.Replace(work.Roster, work.MapName);
        SnapshotChanged?.Invoke(this, new GameCombatSnapshotChangedEventArgs(work.Snapshot));
    }

    public static long UuidToEntityId(long uuid) => uuid >> 16;

    public static int GetEntityTypeFromUuid(long uuid) => unchecked((int)((uuid >> 6) & 31));

    public static long EntityIdToUuid(long entityId, int entityType) => (entityId << 16) | ((long)entityType << 6);

    private sealed record PublishWork(
        GameCombatSnapshot Snapshot,
        IReadOnlyList<PlayerRosterEntry> Roster,
        string MapName);

    private sealed class MutableDungeonEntry
    {
        public Guid DungeonEntryId { get; init; }

        public GameDungeonEntryPhase Phase { get; set; }

        public DateTimeOffset CreatedAtUtc { get; init; }

        public DateTimeOffset? StartedAtUtc { get; set; }

        public DateTimeOffset? CompletedAtUtc { get; set; }

        public uint LevelMapId { get; set; }

        public string MapName { get; set; } = string.Empty;

        public uint ChannelLine { get; set; }

        public int DungeonState { get; set; }

        public Dictionary<long, CombatantState> Entities { get; } = [];

        public Dictionary<long, long> DungeonDamageTotals { get; set; } = [];

        public List<GameCombatEvent> Events { get; } = [];

        public Dictionary<string, byte[]> ProtocolFrames { get; } = [];

        public CharSerialize? ContainerData { get; set; }

        public DirtyContainer? ContainerDirtyData { get; set; }

        public DungeonSyncData? DungeonData { get; set; }

        public DirtyDungeon? DungeonDirtyData { get; set; }

        public static MutableDungeonEntry CreateNew(uint levelMapId, string mapName)
        {
            return new MutableDungeonEntry
            {
                DungeonEntryId = Guid.NewGuid(),
                Phase = GameDungeonEntryPhase.Idle,
                CreatedAtUtc = DateTimeOffset.UtcNow,
                LevelMapId = levelMapId,
                MapName = mapName
            };
        }
    }

    private sealed class CombatantState(long entityUuid, int entityType)
    {
        private readonly Dictionary<int, MutableSkillStatistics> _skills = [];

        public long EntityUuid { get; } = entityUuid;

        public long CharacterId { get; set; }

        public int EntityType { get; set; } = entityType;

        public bool IsSelf { get; set; }

        public bool IsRosterVisible { get; set; } = true;

        public string Name { get; set; } = string.Empty;

        public int ProfessionId { get; set; }

        public PlayerClassSpec ClassSpec { get; set; }

        public int CombatPower { get; set; }

        public int SeasonStrength { get; set; }

        public long CurrentHp { get; set; }

        public long MaxHp { get; set; }

        public PlayerCombatAttributes CombatAttributes { get; set; }

        public Dictionary<int, byte[]> RawAttributes { get; } = [];

        public Dictionary<int, int> TempAttributes { get; } = [];

        public Entity? ProtocolEntity { get; set; }

        public AoiSyncDelta? LastDelta { get; set; }

        public long DamageDone { get; set; }

        public long HealingDone { get; set; }

        public long DamageTaken { get; set; }

        public long HealingTaken { get; set; }

        public long EffectiveDamageDone { get; set; }

        public long EffectiveHealingDone { get; set; }

        public long EffectiveDamageTaken { get; set; }

        public long ServerDungeonDamageTotal { get; set; }





        public void RecordSkill(
            int skillId,
            long damageDone,
            long healingDone,
            long damageTaken,
            bool isCritical,
            bool isLucky,
            bool isMiss)
        {
            if (skillId == 0)
            {
                return;
            }

            if (!_skills.TryGetValue(skillId, out var skill))
            {
                skill = new MutableSkillStatistics(skillId);
                _skills.Add(skillId, skill);
            }

            skill.DamageDone += damageDone;
            skill.HealingDone += healingDone;
            skill.DamageTaken += damageTaken;
            skill.HitCount++;
            if (isCritical)
            {
                skill.CriticalHitCount++;
            }

            if (isLucky)
            {
                skill.LuckyHitCount++;
            }

            if (isMiss)
            {
                skill.MissCount++;
            }
        }

        public GameCombatantSnapshot CreateSnapshot()
        {
            var skills = _skills.Values
                .OrderByDescending(skill => skill.DamageDone)
                .ThenByDescending(skill => skill.HealingDone)
                .ThenBy(skill => skill.SkillId)
                .Select(skill => skill.CreateSnapshot())
                .ToArray();

            return new GameCombatantSnapshot(
                EntityUuid,
                CharacterId,
                EntityType,
                Name,
                ProfessionId,
                DamageDone,
                HealingDone,
                DamageTaken,
                HealingTaken,
                EffectiveDamageDone,
                EffectiveHealingDone,
                EffectiveDamageTaken,
                ServerDungeonDamageTotal,
                skills.Sum(skill => skill.HitCount),
                skills.Sum(skill => skill.CriticalHitCount),
                skills.Sum(skill => skill.LuckyHitCount),
                skills.Sum(skill => skill.MissCount),
                skills);
        }
    }

    private sealed class MutableSkillStatistics(int skillId)
    {
        public int SkillId { get; } = skillId;

        public long DamageDone { get; set; }

        public long HealingDone { get; set; }

        public long DamageTaken { get; set; }

        public int HitCount { get; set; }

        public int CriticalHitCount { get; set; }

        public int LuckyHitCount { get; set; }

        public int MissCount { get; set; }

        public void MergeFrom(MutableSkillStatistics source)
        {
            DamageDone += source.DamageDone;
            HealingDone += source.HealingDone;
            DamageTaken += source.DamageTaken;
            HitCount += source.HitCount;
            CriticalHitCount += source.CriticalHitCount;
            LuckyHitCount += source.LuckyHitCount;
            MissCount += source.MissCount;
        }

        public GameSkillStatistics CreateSnapshot()
        {
            return new GameSkillStatistics(
                SkillId,
                DamageDone,
                HealingDone,
                DamageTaken,
                HitCount,
                CriticalHitCount,
                LuckyHitCount,
                MissCount);
        }
    }
}
