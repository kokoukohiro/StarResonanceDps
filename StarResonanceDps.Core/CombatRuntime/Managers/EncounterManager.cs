using StarResonanceDps.Core.CombatRuntime.Protocols;
using StarResonanceDps.Core.CombatRuntime.DataTypes;
using Newtonsoft.Json;
using ProtoBuf;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Runtime.Serialization;
using System.Text;
using System.Threading.Tasks;
using ZLinq;
using Zproto;

namespace StarResonanceDps.Core.CombatRuntime
{
    public static class EncounterManager
    {
        public static int SelectedEncounter = -1;

        public static Encounter Current = null!;

        public static int CurrentBattleId = 0;
        public static uint LevelMapId { get; private set; }
        public static bool AllowSceneUpdate = true;

        public static string SceneName { get; private set; } = null!;
        public delegate void BattleStartEventHandler(EventArgs e);
        public static event BattleStartEventHandler? BattleStart;
        public delegate void EncounterStartEventHandler(EncounterStartEventArgs e);
        public static event EncounterStartEventHandler? EncounterStart;
        public delegate void EncounterEndEventHandler(EventArgs e);
        public static event EncounterEndEventHandler? EncounterEnd;
        public delegate void EncounterEndFinalEventHandler(EncounterEndFinalData e);
        public static event EncounterEndFinalEventHandler? EncounterEndFinal;

        public static CancellationTokenSource UpdateTruePerValuesCTS = new CancellationTokenSource();

        static EncounterManager()
        {

            StartNewMap();
            EnterDungeon();
        }

        public static void EnterDungeon(bool force = false, EncounterStartReason reason = EncounterStartReason.None)
        {
            string priorBossName = "";
            int priorEncounterPhase = 0;

            if (Current != null)
            {
                bool hasStatsBeenRecorded = Current.HasStatsBeenRecorded();
                if (force || (Current.EndTime == DateTime.MinValue && hasStatsBeenRecorded))
                {

                    StopEncounter(true);
                }
                else if (Current.EndTime == DateTime.MinValue && !hasStatsBeenRecorded)
                {

                    Current.SetStartTime(DateTime.Now);
                    Current.SetEndTime(DateTime.MinValue);
                    if (LevelMapId > 0)
                    {
                        SetSceneId(LevelMapId, true);
                    }
                    return;
                }

                if (reason == EncounterStartReason.Wipe)
                {

                    if (Current.Entities.TryGetValue(Current.BossUUID, out var bossEntity))
                    {
                        long lowestHp = bossEntity.MaxHp;

                        int stackSize = bossEntity.RecentHpHistory.Count > 8 ? 8 : bossEntity.RecentHpHistory.Count;
                        for (int i = 0; i < stackSize; i++)
                        {
                            long historicalHp = bossEntity.RecentHpHistory.ElementAt(i);
                            if (historicalHp < lowestHp)
                            {
                                lowestHp = historicalHp;
                            }
                        }

                        bossEntity.SetHpNoUpdate(lowestHp);
                    }
                }

                if ((reason == EncounterStartReason.NewObjective) && hasStatsBeenRecorded)
                {

                    priorBossName = Current.BossName;

                    if (Current.ExData.EncounterPhase > 0)
                    {
                        priorEncounterPhase = Current.ExData.EncounterPhase;
                    }
                    else
                    {

                        Current.SceneSubName = "Phase 1";
                        Current.ExData.EncounterPhase = 1;
                        priorEncounterPhase = 1;
                    }
                }

                CheckTimeOutStatus(reason);

                if (Current.TotalDamage > 0)
                {

                    RecalculateEncounterPerValues(Current.EndTime.ToUniversalTime());
                }

                BattleStateMachine.SetDeferredEncounterEndFinalData(DateTime.Now.Subtract(new TimeSpan(0, 0, 1)), new EncounterEndFinalData() { EncounterId = Current.EncounterId, BattleId = Current.BattleId, Reason = reason, Encounter = Current });
                BattleStateMachine.CheckDeferredCalls();
            }

            int currentDifficulty = 0;
            Encounter? priorEncounter = Current;
            ulong nextEncounterIdModifier = 0;
            if (Current != null)
            {
                currentDifficulty = Current.ExData.DungeonDifficulty;
                if (Settings.Instance.SkipSavingEncountersWithNoCombatData && !Current.HasStatsBeenRecorded())
                {
                    nextEncounterIdModifier = 0;
                }
                else
                {
                    nextEncounterIdModifier = 1;
                }
            }

            Current = new Encounter(CurrentBattleId);
            Current.EncounterId = DB.GetNextEncounterId() + nextEncounterIdModifier;
            System.Diagnostics.Debug.WriteLine($"Created new encounter for EncounterId {Current.EncounterId} + ({nextEncounterIdModifier})");
            if (priorEncounter != null && (reason != EncounterStartReason.None && reason != EncounterStartReason.Force))
            {

                var priorCharacters = priorEncounter.Entities.AsValueEnumerable().Where(x => x.Value.EntityType == EEntityType.EntChar);
                foreach (var priorChar in priorCharacters)
                {
                    var newChar = Current.GetOrCreateEntity(priorChar.Key);
                    newChar.SetHpValuesNoUpdate(priorChar.Value.Hp, priorChar.Value.MaxHp);
                    newChar.Attributes = priorChar.Value.Attributes.ToDictionary();
                }

                if (reason == EncounterStartReason.NewObjective)
                {

                    var priorBosses = priorEncounter.Entities.AsValueEnumerable().Where(x => x.Value.EntityType == EEntityType.EntMonster && x.Value.MonsterType == EMonsterType.Boss);
                    foreach (var priorBoss in priorBosses)
                    {
                        if (priorBoss.Value.Hp > 0)
                        {
                            var newBoss = Current.GetOrCreateEntity(priorBoss.Key);
                            newBoss.SetHpValuesNoUpdate(priorBoss.Value.Hp, priorBoss.Value.MaxHp);
                            newBoss.Attributes = priorBoss.Value.Attributes.ToDictionary();
                            var attrId = newBoss.GetAttrKV("AttrId");
                            if (attrId != null)
                            {
                                Current.SetAttrKV(priorBoss.Key, "AttrId", (int)attrId);
                            }
                        }
                    }
                }
                else if (reason == EncounterStartReason.Wipe)
                {
                    var priorBosses = priorEncounter.Entities.AsValueEnumerable().Where(x => x.Value.EntityType == EEntityType.EntMonster && x.Value.MonsterType == EMonsterType.Boss);
                    foreach (var priorBoss in priorBosses)
                    {
                        var bossCache = new EncounterBossDataCache()
                        {
                            UUID = priorBoss.Key,
                            Name = priorBoss.Value.Name,
                            Hp = priorBoss.Value.Hp,
                            MaxHp = priorBoss.Value.MaxHp,
                            Attrs = priorBoss.Value.Attributes.ToDictionary()
                        };
                        Current.PreviousBossCache[priorBoss.Key] = bossCache;
                    }
                }
            }
            if (reason == EncounterStartReason.NewObjective)
            {
                if (!string.IsNullOrEmpty(priorBossName))
                {
                    Current.BossName = priorBossName;
                }
                if (priorEncounterPhase > 0)
                {
                    Current.SceneSubName = $"Phase {priorEncounterPhase + 1}";
                    Current.ExData.EncounterPhase = priorEncounterPhase + 1;
                }
            }
            Current.SetWipeState(false);

            if (LevelMapId > 0)
            {
                SetSceneId(LevelMapId, true);
                Current.SetDungeonDifficulty(currentDifficulty);
            }

            if (AppState.IsBenchmarkMode)
            {
                Current.ExData.BenchmarkTime = AppState.BenchmarkTime;
            }

            UpdateTruePerValuesCTS = new();
            {

                Task.Run(() =>
                {
                    UpdateTruePerValues(UpdateTruePerValuesCTS);
                });
            }

            if (priorEncounter != null)
            {
                if (nextEncounterIdModifier != 0 && !AppState.IsEncounterSavingPaused)
                {
                    DB.InsertEncounter(priorEncounter);
                    GC.Collect();
                }
            }

            AllowSceneUpdate = true;
            Serilog.Log.Debug("EncounterManager sending OnEncounterStart event");
            OnEncounterStart(new EncounterStartEventArgs() { Reason = reason });
        }

        public static void StopEncounter(bool isKnownFinal = false, EncounterStartReason reason = EncounterStartReason.None)
        {
            if (Current != null && Current.EndTime == DateTime.MinValue)
            {
                Current.SetEndTime(DateTime.Now);
            }

            UpdateTruePerValuesCTS.Cancel();

            CheckTimeOutStatus(reason);

            OnEncounterEnd(new EventArgs());

            if (isKnownFinal)
            {

                BattleStateMachine.SetDeferredEncounterEndFinalData(DateTime.Now.AddSeconds(2), new EncounterEndFinalData() { EncounterId = Current.EncounterId, BattleId = Current.BattleId, Reason = reason, Encounter = Current });
            }
            else
            {
                BattleStateMachine.SetDeferredEncounterEndFinalData(DateTime.Now.AddSeconds(5), new EncounterEndFinalData() { EncounterId = Current.EncounterId, BattleId = Current.BattleId, Reason = reason, Encounter = Current });
            }

            EntityCache.Instance.Save();
        }

        static void CheckTimeOutStatus(EncounterStartReason reason)
        {
            if (!Current.ExData.IsTimedOut && reason == EncounterStartReason.DungeonStateEnd && Current.SceneId > 0)
            {
                if (HelperMethods.DataTables.SceneEventDungeonConfigs.Data.TryGetValue(Current.SceneId.ToString(), out var sceneEventDungeonConfig))
                {
                    if (sceneEventDungeonConfig.LimitTime > 0 && Current.BossUUID == 0)
                    {
                        if (Current.GetDuration().TotalSeconds >= (sceneEventDungeonConfig.LimitTime + Current.ExData.DungeonTimeDeathChange))
                        {
                            Current.SetTimedOutState(true);
                        }
                    }
                }
            }
        }

        public static void ShutdownManager()
        {
            if (Current != null && Current.EndTime == DateTime.MinValue)
            {
                Current.SetEndTime(DateTime.Now);
            }

            UpdateTruePerValuesCTS.Cancel();

            if (Current != null && Current.HasStatsBeenRecorded(true))
            {
                DB.InsertEncounter(Current);
            }

            EntityCache.Instance.FinalSave();
        }

        public static void SignalEncounterEndFinal(EncounterEndFinalData data)
        {
            OnEncounterEndFinal(data);
            if (Current != null)
            {
                Current.RemoveEventHandlers();
            }
        }

        public static void UpdateEncounterState()
        {

            double combatTimeout = 15.0;
            if (Current != null && DateTime.Now.Subtract(Current.LastUpdate).TotalSeconds > combatTimeout)
            {

                EnterDungeon();
            }
        }

        public static void StartNewMap()
        {

            if (CurrentBattleId != 0)
            {
                DB.UpdateBattleEnd(CurrentBattleId);
            }

            var battleId = DB.StartBattle(LevelMapId, SceneName);
            CurrentBattleId = battleId;

            OnBattleStart(new EventArgs());
        }

        public static void SetSceneId(uint levelMapId, bool force = false)
        {
            if (!AllowSceneUpdate && !force)
            {
                return;
            }

            LevelMapId = levelMapId;
            if (levelMapId > 0)
            {
                HelperMethods.DataTables.Dungeons.Data.TryGetValue(LevelMapId.ToString(), out var dungeon);

                if (dungeon != null && dungeon.PlayType == 17)
                {
                    SceneName = dungeon.Name;
                }
                else
                {
                    if (HelperMethods.DataTables.Scenes.Data.TryGetValue(levelMapId.ToString(), out var scene))
                    {
                        SceneName = scene.Name;
                    }
                    else
                    {
                        SceneName = "";
                    }
                }
            }
            else
            {
                SceneName = "";
            }

            Current.SceneId = LevelMapId;
            Current.SceneName = SceneName;
            DB.UpdateBattleInfo(CurrentBattleId, LevelMapId, SceneName);
        }

        public static async void UpdateTruePerValues(CancellationTokenSource cancellationTokenSource)
        {
            var timer = new PeriodicTimer(TimeSpan.FromSeconds(1));

            while (!cancellationTokenSource.IsCancellationRequested && await timer.WaitForNextTickAsync())
            {
                if (Current != null && Current.TotalDamage > 0)
                {
                    RecalculateEncounterPerValues();
                }
            }
        }

        public static void RecalculateEncounterPerValues(DateTime? nowTime = null)
        {
            DateTime now = nowTime ?? DateTime.UtcNow;
            var entities = Current.Entities.AsValueEnumerable();
            foreach (var entity in entities)
            {
                if (nowTime != null)
                {
                    entity.Value.RecalculateInactiveTime(now, true);
                }
                else
                {

                    entity.Value.RecalculateInactiveTime(now, true);
                }
                double inactiveTime = entity.Value.GetInactiveTime();

                entity.Value.DamageStats.InactiveTime = inactiveTime;
                if (entity.Value.DamageStats.ValueTotal > 0)
                {
                    entity.Value.DamageStats.RecalculatePerSecond(now);
                }

                entity.Value.HealingStats.InactiveTime = inactiveTime;
                if (entity.Value.HealingStats.ValueTotal > 0)
                {
                    entity.Value.HealingStats.RecalculatePerSecond(now);
                }

                entity.Value.TakenStats.InactiveTime = inactiveTime;
                if (entity.Value.TakenStats.ValueTotal > 0)
                {
                    entity.Value.TakenStats.RecalculatePerSecond(now);
                }

                foreach (var skill in entity.Value.SkillMetrics)
                {
                    skill.Value.Damage.InactiveTime = inactiveTime;
                    if (skill.Value.Damage.ValueTotal > 0)
                    {
                        skill.Value.Damage.RecalculatePerSecond(now);
                    }

                    skill.Value.Healing.InactiveTime = inactiveTime;
                    if (skill.Value.Healing.ValueTotal > 0)
                    {
                        skill.Value.Healing.RecalculatePerSecond(now);
                    }

                    skill.Value.Taken.InactiveTime = inactiveTime;
                    if (skill.Value.Taken.ValueTotal > 0)
                    {
                        skill.Value.Taken.RecalculatePerSecond(now);
                    }
                }
            }
        }

        static void OnBattleStart(EventArgs e)
        {
            BattleStart?.Invoke(e);
        }

        static void OnEncounterStart(EncounterStartEventArgs e)
        {
            EncounterStart?.Invoke(e);
        }

        static void OnEncounterEnd(EventArgs e)
        {
            EncounterEnd?.Invoke(e);
        }

        static void OnEncounterEndFinal(EncounterEndFinalData e)
        {
            EncounterEndFinal?.Invoke(e);
        }
    }

    public enum EncounterStartReason : int
    {
        None = 0,
        NewObjective = 1,
        Wipe = 2,
        Force = 3,
        Restart = 4,
        TimedOut = 5,
        BenchmarkStart = 6,
        BenchmarkEnd = 7,
        DungeonStateEnd = 8,
    }

    public class EncounterStartEventArgs : EventArgs
    {
        public EncounterStartReason Reason;
    }

    public class Encounter
    {
        public ulong EncounterId { get; set; }
        public int BattleId { get; set; }
        public uint SceneId { get; set; }
        public string SceneName { get; set; } = null!;
        public string SceneSubName { get; set; } = null!;
        public long BossUUID { get; set; }
        public long BossAttrId { get; set; }
        public string BossName { get; set; } = null!;
        public int BossHpPct { get; set; }
        public string Note { get; set; } = null!;

        public DateTime StartTime { get; private set; }
        public DateTime EndTime { get; private set; }
        private TimeSpan? Duration { get; set; }
        public DateTime LastUpdate { get; set; }
        public ConcurrentDictionary<long, Entity> Entities { get; set; } = [];

        public ulong TotalDamage { get; set; } = 0;
        public ulong TotalNpcDamage { get; set; } = 0;
        public ulong TotalShieldBreak { get; set; } = 0;
        public ulong TotalNpcShieldBreak { get; set; } = 0;
        public ulong TotalHealing { get; set; } = 0;
        public ulong TotalNpcHealing { get; set; } = 0;
        public ulong TotalOverhealing { get; set; } = 0;
        public ulong TotalNpcOverhealing { get; set; } = 0;
        public ulong TotalTakenDamage { get; set; } = 0;
        public ulong TotalNpcTakenDamage { get; set; } = 0;
        public ulong TotalDeaths { get; set; } = 0;
        public ulong TotalNpcDeaths { get; set; } = 0;
        public bool IsWipe { get; set; } = false;

        public delegate void SkillActivatedEventHandler(object sender, SkillActivatedEventArgs e);
        public event SkillActivatedEventHandler? SkillActivated;
        public delegate void HpUpdatedEventHandler(object sender, HpUpdatedEventArgs e);
        public event HpUpdatedEventHandler? BossHpUpdated;
        public event HpUpdatedEventHandler? EntityHpUpdated;
        public delegate void ThreatListUpdatedEventHandler(object sender, ThreatListUpdatedEventArgs e);
        public event ThreatListUpdatedEventHandler? EntityThreatListUpdated;
        public delegate void BuffUpdatedEventHandler(object sender, BuffUpdatedEventArgs e);
        public event BuffUpdatedEventHandler? BuffUpdated;
        public delegate void AttributeUpdatedEventHandler(object sender, AttributeUpdatedEventArgs e);
        public event AttributeUpdatedEventHandler? AttributeUpdated;
        public delegate void SceneEventEventHandler(object sender, SceneEventEventArgs e);
        public event SceneEventEventHandler? SceneEvent;

        public EncounterExData ExData { get; set; } = new();
        public byte[] ExDataBlob { get; set; } = null!;

        public List<long> BossUUIDs { get; set; } = new();
        public EDungeonState DungeonState { get; set; } = EDungeonState.DungeonStateNull;
        public uint ChannelLine { get; set; } = 0;
        public Dictionary<long, EncounterBossDataCache> PreviousBossCache = new();

        public Encounter()
        {

        }

        public Encounter(int battleId = 0)
        {
            SetStartTime(DateTime.Now);
            Entities = new();
            BattleId = battleId;
        }

        public Encounter(DateTime startTime, int battleId = 0)
        {
            SetStartTime(startTime);
            Entities = new();
            BattleId = battleId;
        }

        public void SetStartTime(DateTime start)
        {
            StartTime = start;
        }

        public void SetEndTime(DateTime end)
        {
            EndTime = end;

            Duration = EndTime.Subtract(StartTime);
        }

        public TimeSpan GetDuration(bool startAdjusted = false)
        {
            if (EndTime == DateTime.MinValue || Duration == null)
            {
                if (startAdjusted && ExData.FirstDamageTimeStamp != null)
                {
                    return DateTime.UtcNow.Subtract((DateTime)ExData.FirstDamageTimeStamp).Duration();
                }
                return DateTime.Now.Subtract(StartTime).Duration();
            }
            else
            {
                if (startAdjusted && ExData.FirstDamageTimeStamp != null)
                {
                    return EndTime.ToUniversalTime().Subtract((DateTime)ExData.FirstDamageTimeStamp);
                }
                return (TimeSpan)Duration;
            }
        }

        public Entity GetOrCreateEntity(long uuid)
        {
            if (Entities.TryGetValue(uuid, out var entity))
            {
                return entity;
            }
            else
            {
                entity = new Entity(uuid, null, this);
                Entities.TryAdd(uuid, entity);
                return entity;
            }
        }

        public void SetName(long uuid, string name)
        {
            GetOrCreateEntity(uuid).SetName(name);
        }

        public void SetAbilityScore(long uuid, int power)
        {
            GetOrCreateEntity(uuid).SetAbilityScore(power);
        }

        public void SetProfessionId(long uuid,  int professionId)
        {
            GetOrCreateEntity(uuid).SetProfessionId(professionId);
        }

        public void SetEntityType(long uuid, EEntityType etype)
        {
            var entity = GetOrCreateEntity(uuid);
            entity.SetEntityType(etype);

            var attr_id = entity.GetAttrKV("AttrId");
            if (attr_id != null && etype != EEntityType.EntChar)
            {

                entity.UpdateUID((int)attr_id);

                if (etype == EEntityType.EntMonster)
                {
                    if (HelperMethods.DataTables.Monsters.Data.TryGetValue(attr_id.ToString()!, out var monsterEntry))
                    {
                        entity.SetName(monsterEntry.Name);
                        entity.SetMonsterType(monsterEntry.MonsterType);
                        UpdateEncounterBossData(entity, (int)attr_id);
                    }
                }
                else if (entity.EntityType == EEntityType.EntDummy)
                {
                    if (HelperMethods.DataTables.Dummys.Data.TryGetValue(attr_id.ToString()!, out var dummyEntry))
                    {
                        entity.SetName(dummyEntry.Name);
                    }
                }
            }
        }

        public void SetAttrKV(long uuid, string key, object value)
        {
            var entity = GetOrCreateEntity(uuid);
            entity.SetAttrKV(key, value);

            if (key == "AttrId" && entity.EntityType != EEntityType.EntChar)
            {
                entity.UpdateUID((int)value);

                if (entity.EntityType == EEntityType.EntMonster)
                {
                    if (HelperMethods.DataTables.Monsters.Data.TryGetValue(value.ToString()!, out var monsterEntry))
                    {
                        entity.SetName(monsterEntry.Name);
                        entity.SetMonsterType(monsterEntry.MonsterType);
                        UpdateEncounterBossData(entity, (int)value);
                    }
                }
                else if (entity.EntityType == EEntityType.EntDummy)
                {
                    if (HelperMethods.DataTables.Dummys.Data.TryGetValue(value.ToString()!, out var dummyEntry))
                    {
                        entity.SetName(dummyEntry.Name);
                    }
                }
            }
            else if (key == "AttrName")
            {
                entity.SetName((string)value);
            }
            else if (key == "AttrProfessionId")
            {
                entity.SetProfessionId((int)value);
            }
            else if (key == "AttrFightPoint")
            {
                entity.SetAbilityScore((int)value);
            }
            else if (key == "AttrLevel")
            {
                entity.SetLevel((int)value);
            }
            else if (key == "AttrSeasonLevel")
            {
                entity.SetSeasonLevel((int)value);
            }
            else if (key == "AttrSeasonStrength")
            {
                entity.SetSeasonStrength((int)value);
            }
            else if (key == "AttrSkillId")
            {
                OnSkillActivated(new SkillActivatedEventArgs { CasterUuid = uuid, SkillId = (int)value, ActivationDateTime = DateTime.Now });
                entity.RegisterSkillActivation((int)value);
            }
            else if (key == "AttrState")
            {
                if ((EActorState)value == EActorState.ActorStateDead)
                {
                    entity.IncrementDeaths();
                    if (entity.EntityType == EEntityType.EntChar)
                    {
                        IncrementDeaths();
                    }
                    else if (entity.EntityType == EEntityType.EntMonster)
                    {
                        IncrementNpcDeaths();
                    }

                    SetAttrKV(uuid, "AttrHp", 0L);
                }
            }
            else if (key == "AttrShieldList")
            {
                var shieldList = (List<ShieldInfo>)value;
                foreach (var shield in shieldList)
                {
                    entity.AddBuffEventAttribute((int)shield.Uuid, "AttrShieldList", shield);
                }
            }
            else if (key == "AttrHp")
            {
                if (!entity.IsHpUpdatedHandlerSubscribed(OnEntityHpUpdated))
                {
                    UpdateEncounterBossData(entity, -1);
                    entity.HpUpdated += OnEntityHpUpdated;
                }
                entity.SetHpValues((long)value, -1);
            }
            else if (key == "AttrMaxHp")
            {
                entity.SetHpValues(-1, (long)value);
            }
            else if (key == "AttrSkillRemodelLevel")
            {

                if (entity.EntityType == EEntityType.EntChar || entity.EntityType == EEntityType.EntMonster)
                {
                    UpdateCasterSkillTierLevel(uuid, entity, (int)value);
                }
                else
                {

                    UpdateCasterSkillTierLevel(0, entity, (int)value);
                }
            }
            else if (key == "AttrPos")
            {
                entity.SetPosition(((Zproto.Vec3)value).ToVector3());
            }
            else if (key == "AttrHateList")
            {
                if(!entity.IsThreatListUpdatedHandlerSubscribed(OnEntityThreatListUpdated))
                {
                    entity.ThreatListUpdated += OnEntityThreatListUpdated;
                }

                var hateInfoList = (List<HateInfo>)value;
                var threatInfoList = new List<ThreatInfo>();
                foreach (var hateInfo in hateInfoList)
                {
                    ThreatInfo threatInfo = new() { EntityUuid = hateInfo.Uuid, ThreatValue = hateInfo.HateVal };
                    threatInfoList.Add(threatInfo);
                }

                entity.SetThreatList(threatInfoList);
            }
            else if (key == "AttrTopSummonerId")
            {
                if (entity.EntityType != EEntityType.EntChar)
                {
                    var summoner = GetOrCreateEntity((long)value);
                    entity.SummonerEntityType = summoner.EntityType;
                }
            }

            OnAttributeUpdated(this, new AttributeUpdatedEventArgs() { EntityUuid = uuid, Entity = entity, AttributeName = key, AttributeValue = value });
        }

        public void SetTempAttrKV(long uuid, int key, TempAttributesContainer value)
        {
            var entity = GetOrCreateEntity(uuid);
            entity.SetTempAttrKV(key, value);
        }

        public void AddSceneEvent(Zproto.EventData sceneEvent)
        {
            if (sceneEvent.EventType == (int)WorldEventType.BossDbm)
            {

                int skillId = 0;
                int duration = 0;
                int insertion = 0;
                if (sceneEvent.IntParams != null)
                {
                    for (int i = 0; i < sceneEvent.IntParams.Count; i++)
                    {
                        switch (i)
                        {
                            case 0:
                                skillId = sceneEvent.IntParams[i];
                                break;
                            case 1:
                                duration = sceneEvent.IntParams[i];
                                break;
                            case 2:
                                insertion = sceneEvent.IntParams[i];
                                break;
                            default:
                                Serilog.Log.Debug($"Unexpected item({i}) in SceneEvent[BossDbm] IntParams = {sceneEvent.IntParams[i]}");
                                break;
                        }
                    }
                }

                long timestamp = 0;
                if (sceneEvent.LongParams != null)
                {
                    for (int i = 0; i < sceneEvent.LongParams.Count; ++i)
                    {
                        switch (i)
                        {
                            case 0:
                                timestamp = sceneEvent.LongParams[i];
                                break;
                            default:
                                Serilog.Log.Debug($"Unexpected item({i}) in SceneEvent[BossDbm] LongParams = {sceneEvent.LongParams[i]}");
                                break;
                        }
                    }
                }

                OnSceneEvent(this, new SceneEventBossDbmEventArgs() { EventType = WorldEventType.BossDbm, SkillId = skillId, Duration = duration, Insertion = insertion, Timestamp = timestamp });
            }
            else if (sceneEvent.EventType == (int)WorldEventType.NoticeTip)
            {

                string messageId = "";
                string extraId = "";
                if (sceneEvent.StrParams != null)
                {
                    for (int i = 0; i < sceneEvent.StrParams.Count; i++)
                    {
                        switch (i)
                        {
                            case 0:
                                messageId = sceneEvent.StrParams[i];
                                break;
                            case 1:
                                extraId = sceneEvent.StrParams[i];
                                break;
                            default:
                                Serilog.Log.Debug($"Unexpected item({i}) in SceneEvent[NoticeTip] StrParams = {sceneEvent.StrParams[i]}");
                                break;
                        }
                    }
                }

                OnSceneEvent(this, new SceneEventNoticeTipEventArgs() { EventType = WorldEventType.NoticeTip, MessageId = messageId, ExtraId = extraId });
            }
        }

        public void UpdateCasterSkillTierLevel(long casterUuid, Entity summoned, int skillTierLevel = -1)
        {
            long caster = casterUuid;

            if (caster == 0)
            {
                var summonerId = summoned.GetAttrKV("AttrSummonerId");
                if (summonerId == null)
                {
                    summonerId = summoned.GetAttrKV("AttrTopSummonerId");
                }
                if (summonerId != null)
                {
                    caster = (long)summonerId;
                }
            }

            if (caster != 0)
            {
                int level = skillTierLevel;
                if (level == -1)
                {
                    var attrSkillRemodelLevel = summoned.GetAttrKV("AttrSkillRemodelLevel");
                    if (attrSkillRemodelLevel != null)
                    {
                        level = (int)attrSkillRemodelLevel;
                    }
                }

                var skillId = summoned.GetAttrKV("AttrSkillId");
                if (skillId != null)
                {
                    if (GetOrCreateEntity((long)caster).SkillMetrics.TryGetValue((int)skillId, out var container))
                    {
                        container.Damage.SetSummonData(summoned.UUID, (int)level);
                        container.Healing.SetSummonData(summoned.UUID, (int)level);
                    }
                }
            }
        }

        public void UpdateEncounterBossData(Entity entity, int attr_id)
        {
            if (entity.MonsterType == EMonsterType.Boss)
            {
                if (!BossUUIDs.Contains(entity.UUID))
                {
                    BossUUIDs.Add(entity.UUID);
                    if (!entity.IsHpUpdatedHandlerSubscribed(OnBossHpUpdated))
                    {
                        entity.HpUpdated += OnBossHpUpdated;
                    }
                }

                if (BossUUID == 0)
                {

                    BossUUID = entity.UUID;
                    BossName = entity.Name;
                    BossAttrId = (long)attr_id;
                }
            }
        }

        public void SetChannelLineNumber(uint line)
        {
            ChannelLine = line;
        }

        public void SetDungeonDifficulty(int difficulty)
        {
            ExData.DungeonDifficulty = difficulty;
        }

        public void IncrementDeaths()
        {
            TotalDeaths++;
        }

        public void IncrementNpcDeaths()
        {
            TotalNpcDeaths++;
        }

        public void SetWipeState(bool state)
        {
            IsWipe = state;
        }

        public void SetTimedOutState(bool state)
        {
            ExData.IsTimedOut = state;
        }

        public object? GetAttrKV(long uuid, string key)
        {
            return GetOrCreateEntity(uuid).GetAttrKV(key);
        }

        public bool HasStatsBeenRecorded(bool includeHealingAndTaken = false)
        {
            if (!includeHealingAndTaken)
            {
                return TotalDamage > 0 || TotalNpcDamage > 0;
            }

            return TotalDamage > 0 || TotalHealing > 0 || TotalTakenDamage > 0 || TotalNpcTakenDamage > 0 || TotalNpcDamage > 0 || TotalNpcShieldBreak > 0 || TotalNpcHealing > 0;
        }

        public void RegisterSkillActivation(long uuid, int skillId)
        {
            var entity = GetOrCreateEntity(uuid);
            entity.RegisterSkillActivation(skillId);
        }

        public void AddDamage(
            long attackerUuid, long targetUuid, int skillId, int skillLevel, long damage, long hpLessen, long shieldBreak,
            EDamageProperty damageElement, EDamageType damageType, EDamageMode damageMode,
            bool isCrit, bool isLucky, bool isCauseLucky, bool isMiss, bool isDead, Vec3 damagePos, ExtraPacketData extraPacketData)
        {
            LastUpdate = extraPacketData.ArrivalTime;

            ExData.FirstDamageTimeStamp ??= LastUpdate;

            var attackerType = (EEntityType)Utils.UuidToEntityType(attackerUuid);
            var targetType = (EEntityType)Utils.UuidToEntityType(targetUuid);

            if (attackerType == EEntityType.EntMonster)
            {
                if (damageType != EDamageType.Immune)
                {
                    TotalNpcDamage += (ulong)damage;
                    if (damageType == EDamageType.Absorbed)
                    {
                        TotalNpcShieldBreak += (ulong)shieldBreak;
                    }
                }
            }
            else
            {
                if (damageType != EDamageType.Immune)
                {
                    TotalDamage += (ulong)damage;
                    if (damageType == EDamageType.Absorbed)
                    {
                        TotalShieldBreak += (ulong)shieldBreak;
                    }
                }
            }

            GetOrCreateEntity(attackerUuid).AddDamage(targetUuid, skillId, skillLevel, damage, hpLessen, shieldBreak, damageElement, damageType, damageMode, isCrit, isLucky, isCauseLucky, isMiss, isDead, damagePos, extraPacketData);
        }

        public void AddHealing(
            long attackerUuid, long targetUuid, int skillId, int skillLevel, long damage, long hpLessen, long shieldBreak,
            EDamageProperty damageElement, EDamageType damageType, EDamageMode damageMode,
            bool isCrit, bool isLucky, bool isCauseLucky, bool isMiss, bool isDead, Vec3 damagePos, ExtraPacketData extraPacketData)
        {
            if (!AppState.IsBenchmarkMode && ExData.FirstDamageTimeStamp == null && !Settings.Instance.IncludeHealEventsOutsideOfCombat)
            {
                return;
            }

            LastUpdate = extraPacketData.ArrivalTime;

            ExData.FirstDamageTimeStamp ??= LastUpdate;

            var attackerType = (EEntityType)Utils.UuidToEntityType(attackerUuid);

            if (attackerType == EEntityType.EntMonster)
            {
                TotalNpcHealing += (ulong)damage;
            }
            else
            {
                TotalHealing += (ulong)damage;
            }

            var entity = GetOrCreateEntity(attackerUuid);
            var targetEntity = GetOrCreateEntity(targetUuid);

            long? currentHp = targetEntity.GetAttrKV("AttrHp") as long?;
            long? maxHp = targetEntity.GetAttrKV("AttrMaxHp") as long?;

            long overhealing = 0;
            long effectiveHealing = 0;

            if ((currentHp != null && maxHp != null && maxHp > 0 && currentHp >= 0 && currentHp <= maxHp) && (currentHp + damage > maxHp))
            {
                effectiveHealing = (long)(maxHp - currentHp);
                if (damage >= effectiveHealing)
                {
                    overhealing = damage - effectiveHealing;
                }
            }

            if (attackerType == EEntityType.EntMonster)
            {
                TotalNpcOverhealing += (ulong)overhealing;
            }
            else
            {
                TotalOverhealing += (ulong)overhealing;
            }

            entity.AddHealing(targetUuid, skillId, skillLevel, damage, overhealing, effectiveHealing, hpLessen, shieldBreak, damageElement, damageType, damageMode, isCrit, isLucky, isCauseLucky, isMiss, isDead, damagePos, extraPacketData);
        }

        public void AddTakenDamage(
            long attackerUuid, long targetUuid, int skillId, int skillLevel, long damage, long hpLessen, long shieldBreak,
            EDamageProperty damageElement, EDamageType damageType, EDamageMode damageMode,
            bool isCrit, bool isLucky, bool isCauseLucky, bool isMiss, bool isDead, Vec3 damagePos, ExtraPacketData extraPacketData)
        {
            LastUpdate = extraPacketData.ArrivalTime;

            var targetType = (EEntityType)Utils.UuidToEntityType(targetUuid);
            if (targetType == EEntityType.EntMonster)
            {
                if (damageType != EDamageType.Immune)
                {
                    TotalNpcTakenDamage += (ulong)damage;
                }
            }
            else
            {
                if (damageType != EDamageType.Immune)
                {
                    TotalTakenDamage += (ulong)damage;
                }
            }

            GetOrCreateEntity(targetUuid).AddTakenDamage(attackerUuid, skillId, skillLevel, damage, hpLessen, shieldBreak, damageElement, damageType, damageMode, isCrit, isLucky, isCauseLucky, isMiss, isDead, damagePos, extraPacketData);
        }

        public void AddShieldGained(long entityUuid, long shieldBuffUuid, long value, long initialValue, long maxValue = 0)
        {

        }

        public void NotifyBuffEvent(long entityUuid, EBuffEventType buffEventType, int buffUuid, int baseId, int level, long fireUuid, int layer, int duration, int sourceConfigId, DateTime? creationTime, ExtraPacketData extraPacketData)
        {
            string entityCasterName = "";
            if (fireUuid > 0)
            {
                var caster = GetOrCreateEntity(fireUuid);
                if (!string.IsNullOrEmpty(caster.Name))
                {
                    entityCasterName = caster.Name;
                }
            }

            OnBuffUpdated(this, new BuffUpdatedEventArgs()
            {
                EntityUuid = entityUuid,
                BuffEventType = buffEventType,
                BuffUuid = buffUuid,
                BaseId = baseId,
                Level = level,
                FireUuid = fireUuid,
                Layer = layer,
                Duration = duration,
                SourceConfigId = sourceConfigId,
                EntityCasterName = entityCasterName,
                UpdateDateTime = extraPacketData.ArrivalTime,
                CreationDateTime = creationTime,
            });
            GetOrCreateEntity(entityUuid).NotifyBuffEvent(buffEventType, buffUuid, baseId, level, fireUuid, entityCasterName, layer, duration, sourceConfigId, DateTime.Now.Subtract(EncounterManager.Current.StartTime), creationTime, extraPacketData);
        }

        protected virtual void OnSkillActivated(SkillActivatedEventArgs e)
        {
            SkillActivated?.Invoke(this, e);
        }

        protected virtual void OnBossHpUpdated(object sender, HpUpdatedEventArgs e)
        {
            Entity entity = (Entity)sender;

            if (entity.Hp > -1 && entity.MaxHp > 0 && entity.Hp <= entity.MaxHp)
            {
                long attrId = 0;
                var attr_id = entity.GetAttrKV("AttrId");
                if (attr_id != null)
                {
                    attrId = (long)(int)attr_id;
                }

                if (BossUUID != entity.UUID || BossAttrId != attrId)
                {
                    BossUUID = entity.UUID;
                    BossName = entity.Name;
                    BossAttrId = attrId;

                }

                BossHpPct = (int)(((double)entity.Hp / (double)entity.MaxHp) * 100000.0);
            }

            BossHpUpdated?.Invoke(sender, e);
        }

        protected virtual void OnEntityHpUpdated(object sender, HpUpdatedEventArgs e)
        {
            EntityHpUpdated?.Invoke(sender, e);
        }

        protected virtual void OnEntityThreatListUpdated(object sender, ThreatListUpdatedEventArgs e)
        {
            EntityThreatListUpdated?.Invoke(sender, e);
        }

        protected virtual void OnBuffUpdated(object sender, BuffUpdatedEventArgs e)
        {
            BuffUpdated?.Invoke(sender, e);
        }

        protected virtual void OnAttributeUpdated(object sender, AttributeUpdatedEventArgs e)
        {
            AttributeUpdated?.Invoke(sender, e);
        }

        protected virtual void OnSceneEvent(object sender, SceneEventEventArgs e)
        {
            SceneEvent?.Invoke(sender, e);
        }

        public void RemoveEntityHandlers()
        {
            foreach (var entity in Entities)
            {
                entity.Value.RemoveEventHandlers();
            }
        }

        public void RemoveEventHandlers()
        {
            SkillActivated = null;
            BossHpUpdated = null;
            EntityHpUpdated = null;
            BuffUpdated = null;
            AttributeUpdated = null;
            SceneEvent = null;

            RemoveEntityHandlers();
        }
    }

    [ProtoContract]
    public class EncounterExData
    {
        [ProtoMember(2)]
        public int DungeonDifficulty { get; set; } = 0;
        [ProtoMember(3)]
        public bool IsTimedOut { get; set; } = false;
        [ProtoMember(4)]
        public int DungeonTimeDeathChange { get; set; } = 0;
        [ProtoMember(5)]
        public int EncounterPhase { get; set; } = 0;
        [ProtoMember(6)]
        public int BenchmarkTime { get; set; } = 0;
        [ProtoMember(7)]
        public DateTime? FirstDamageTimeStamp { get; set; } = null;

        public EncounterExData() { }
    }

    public class EncounterBossDataCache
    {
        public long UUID;
        public string Name = null!;
        public long Hp;
        public long MaxHp;
        public Dictionary<string, object> Attrs = null!;
    }

    public class TempAttributesContainer
    {
        public int Id;
        public int Value;
        public DataTypes.TempAttr TempAttr = null!;
    }

    public class SceneEventEventArgs : EventArgs
    {
        public Zproto.WorldEventType EventType;
    }

    public class SceneEventBossDbmEventArgs : SceneEventEventArgs
    {
        public int SkillId;
        public int Duration;
        public int Insertion;
        public long Timestamp;
    }

    public class SceneEventNoticeTipEventArgs : SceneEventEventArgs
    {
        public string MessageId = null!;
        public string ExtraId = null!;
    }

    public class Entity : System.ICloneable
    {
        public long UUID { get; set; }
        public long UID { get; set; }
        public EEntityType EntityType { get; private set; }
        public string Name { get; private set; } = null!;
        public int AbilityScore { get; private set; } = 0;
        public int ProfessionId { get; private set; } = 0;
        public string Profession { get; private set; } = null!;
        public int SubProfessionId { get; private set; } = 0;
        public string SubProfession { get; private set; } = null!;
        public int Level { get; set; } = 0;
        public Vector3 Position { get; private set; } = new();

        public long SeasonLevel { get; set; } = 0;
        public long SeasonStrength { get; set; } = 0;

        public CombatStats DamageStats { get; set; } = new();
        public CombatStats HealingStats { get; set; } = new();
        public CombatStats TakenStats { get; set; } = new();

        public ConcurrentDictionary<int, CombatStats> SkillStats { get; set; } = new();
        public ConcurrentDictionary<int, MetricsContainer> SkillMetrics { get; set; } = new();
        public ConcurrentDictionary<long, StatTracker> InteractedEntities { get; set; } = new();

        public ulong TotalDamage { get; set; } = 0;
        public ulong TotalShieldBreak { get; set; } = 0;
        public ulong TotalHealing { get; set; } = 0;
        public ulong TotalOverhealing { get; set; } = 0;
        public ulong TotalTakenDamage { get; set; } = 0;
        public ulong TotalShield { get; set; } = 0;
        public ulong TotalCasts { get; set; } = 0;
        public ulong TotalDeaths { get; set; } = 0;
        public double TotalInactiveTime { get; set; } = 0.0;
        public double InactiveTime { get; set; } = 0.0;

        public DateTime? FirstCombatActionTime { get; set; } = null;
        public DateTime? LastCombatActionTime { get; set; } = null;

        public ThreadSafeOrderedDictionary<ulong, BuffEvent> BuffEvents { get; set; } = new();
        [JsonIgnore]
        public ThreadSafeOrderedDictionary<ulong, BuffEvent> RecentBuffEventHistory { get; private set; } = new(6);

        public EMonsterType MonsterType { get; set; } = EMonsterType.Unknown;

        public long Hp { get; private set; } = 0;
        public long MaxHp { get; private set; } = 0;
        public ConcurrentQueue<long> RecentHpHistory { get; private set; } = new();

        public List<ThreatInfo> ThreatInfoList { get; private set; } = new();
        public ConcurrentQueue<List<ThreatInfo>> RecentThreatInfoListHistory { get; private set; } = new();

        public Dictionary<string, object> Attributes { get; set; } = new();

        [JsonIgnore]
        public Dictionary<int, TempAttributesContainer> TempAttributes { get; set; } = new();

        public EEntityType SummonerEntityType { get; set; } = EEntityType.EntErrType;

        public delegate void SkillActivatedEventHandler(object sender, SkillActivatedEventArgs e);
        public event SkillActivatedEventHandler? SkillActivated;
        public delegate void HpUpdatedEventHandler(object sender, HpUpdatedEventArgs e);
        public event HpUpdatedEventHandler? HpUpdated;
        public delegate void ThreatListUpdatedEventHandler(object sender, ThreatListUpdatedEventArgs e);
        public event ThreatListUpdatedEventHandler? ThreatListUpdated;

        public object Clone()
        {
            var cloned = this.MemberwiseClone();
            ((Entity)cloned).DamageStats = (CombatStats)this.DamageStats.Clone();
            ((Entity)cloned).HealingStats = (CombatStats)this.HealingStats.Clone();
            ((Entity)cloned).TakenStats = (CombatStats)this.TakenStats.Clone();

            foreach (var container in this.SkillMetrics)
            {
                var metrics = new MetricsContainer
                {
                    Damage = (CombatStats)container.Value.Damage.Clone(),
                    Healing = (CombatStats)container.Value.Healing.Clone(),
                    Taken = (CombatStats)container.Value.Taken.Clone()
                };
                ((Entity)cloned).SkillMetrics.AddOrUpdate(container.Key, metrics, (key, value) => metrics);
            }

            foreach (var tracker in this.InteractedEntities)
            {
                var statTracker = new StatTracker
                {
                    UUID = tracker.Value.UUID,
                    UID = tracker.Value.UID,
                    Name = tracker.Value.Name,
                    ProfessionId = tracker.Value.ProfessionId,
                    SubProfessionId = tracker.Value.SubProfessionId,
                    DidDamage = tracker.Value.DidDamage,
                    DidHealing = tracker.Value.DidHealing,
                    DidTaken = tracker.Value.DidTaken,
                    Damage = (TrackedStats)tracker.Value.Damage.Clone(),
                    Healing = (TrackedStats)tracker.Value.Healing.Clone(),
                    Taken = (TrackedStats)tracker.Value.Taken.Clone(),
                };
                ((Entity)cloned).InteractedEntities.AddOrUpdate(tracker.Key, statTracker, (key, value) => statTracker);
            }
            return cloned;
        }

        public void RemoveEventHandlers()
        {
            SkillActivated = null;
            HpUpdated = null;
            ThreatListUpdated = null;
        }

        [JsonConstructor]
        public Entity(long uuid, string? name = null, Encounter? encounter = null)
        {
            UUID = uuid;
            UID = Utils.UuidToEntityId(uuid);
            Name = name!;

            if (encounter != null)
            {

                if (encounter.PreviousBossCache.Count > 0 && encounter.PreviousBossCache.TryGetValue(uuid, out var foundEnt))
                {
                    SetHpValuesNoUpdate(foundEnt.Hp, foundEnt.MaxHp);
                    Attributes = foundEnt.Attrs.ToDictionary();

                    encounter.PreviousBossCache.Remove(uuid);
                }
            }

            SetEntityType((EEntityType)Utils.UuidToEntityType(uuid));

            var cached = EntityCache.Instance.GetOrCreate(uuid);
            if (cached != null)
            {
                if (UID > 0)
                {

                    UpdateUID(UID, cached);
                }

                if (string.IsNullOrEmpty(name) && !string.IsNullOrEmpty(cached.Name))
                {
                    SetName(cached.Name);
                }

                if (AbilityScore == 0 && cached.AbilityScore != 0)
                {
                    SetAbilityScore(cached.AbilityScore);
                }

                if (Level == 0 && cached.Level != 0)
                {
                    SetLevel(cached.Level);
                }

                if (ProfessionId == 0 && cached.ProfessionId != 0)
                {
                    SetProfessionId(cached.ProfessionId);
                }

                if (SubProfessionId == 0 && cached.SubProfessionId != 0)
                {
                    SetSubProfessionId(cached.SubProfessionId);
                }

                if (SeasonLevel == 0 && cached.SeasonLevel != 0)
                {
                    SetSeasonLevel(cached.SeasonLevel);
                }

                if (SeasonStrength == 0 && cached.SeasonStrength != 0)
                {
                    SetSeasonStrength(cached.SeasonStrength);
                }
            }

        }

        public void UpdateUID(long uid, EntityCacheLine? cached = null)
        {
            UID = uid;
            if (cached != null)
            {
                cached.UID = uid;
            }
            else
            {
                var newCached = EntityCache.Instance.GetOrCreate(UUID);
                if (newCached != null)
                {
                    newCached.UID = uid;
                }
            }
        }

        public void SetName(string name)
        {
            Name = name;

            var cached = EntityCache.Instance.GetOrCreate(UUID);
            if (cached != null && !string.IsNullOrEmpty(name))
            {
                cached.Name = name;
            }
        }

        public void SetEntityType(EEntityType type)
        {
            EntityType = type;

            if (type != EEntityType.EntChar)
            {

            }

            if (type == EEntityType.EntMonster)
            {

                var attr_id = GetAttrKV("AttrId");
                if (attr_id != null)
                {
                    UID = (int)attr_id;
                    if (HelperMethods.DataTables.Monsters.Data.TryGetValue(attr_id.ToString()!, out var monsterEntry))
                    {
                        SetName(monsterEntry.Name);
                        SetMonsterType(monsterEntry.MonsterType);
                    }
                }
            }
            else if (type == EEntityType.EntDummy)
            {
                var attr_id = GetAttrKV("AttrId");
                if (attr_id != null)
                {
                    UID = (int)attr_id;
                    if (HelperMethods.DataTables.Dummys.Data.TryGetValue(attr_id.ToString()!, out var dummyEntry))
                    {
                        SetName(dummyEntry.Name);
                    }
                }
            }
        }

        public void SetAbilityScore(int abilityScore)
        {
            AbilityScore = abilityScore;

            var cached = EntityCache.Instance.GetOrCreate(UUID);
            if (cached != null && abilityScore != 0)
            {
                cached.AbilityScore = abilityScore;
            }
        }

        public void SetProfessionId(int id)
        {
            ProfessionId = id;
            Profession = Professions.GetProfessionNameFromId(id);

            var cached = EntityCache.Instance.GetOrCreate(UUID);
            if (cached != null && id != 0)
            {
                cached.ProfessionId = id;
            }
        }

        public void SetSubProfessionId(int id)
        {
            SubProfessionId = id;
            SubProfession = Professions.GetSubProfessionNameFromId(id);

            int profId = Professions.GetProfessionIdFromSubProfessionId(id);
            if (ProfessionId != profId)
            {
                ProfessionId = profId;
                Profession = Professions.GetProfessionNameFromId(profId);
            }

            var cached = EntityCache.Instance.GetOrCreate(UUID);
            if (cached != null && id != 0)
            {
                cached.SubProfessionId = id;
                if (cached.ProfessionId != profId)
                {
                    cached.ProfessionId = profId;
                }
            }
        }

        public void SetLevel(int level)
        {
            Level = level;

            var cached = EntityCache.Instance.GetOrCreate(UUID);
            if (cached != null && level != 0)
            {
                cached.Level = level;
            }
        }

        public void SetSeasonLevel(int level)
        {
            SeasonLevel = level;

            var cached = EntityCache.Instance.GetOrCreate(UUID);
            if (cached != null && level != 0)
            {
                cached.SeasonLevel = level;
            }
        }

        public void SetSeasonStrength(int strength)
        {
            SeasonStrength = strength;

            var cached = EntityCache.Instance.GetOrCreate(UUID);
            if (cached != null && strength != 0)
            {
                cached.SeasonStrength = strength;
            }
        }

        public void SetPosition(Vector3 position)
        {
            Position = position;
        }

        public void SetHpNoUpdate(long hp)
        {
            Hp = hp;
        }

        public void SetHpValuesNoUpdate(long hp, long maxHp)
        {
            Hp = hp;
            MaxHp = maxHp;
        }

        public void SetHpValues(long hp = -1, long maxHp = -1)
        {
            if (hp != -1)
            {
                Hp = hp;

                if (RecentHpHistory.Count > 10)
                {
                    RecentHpHistory.TryDequeue(out _);
                }
                RecentHpHistory.Enqueue(hp);
            }

            if (maxHp != -1)
            {
                MaxHp = maxHp;
            }

            if ((Hp > -1) && (MaxHp > -1))
            {
                OnHpUpdated(new HpUpdatedEventArgs() { EntityUuid = UUID, Hp = Hp, MaxHp = MaxHp, UpdateDateTime = DateTime.Now });
            }
        }

        protected virtual void OnHpUpdated(HpUpdatedEventArgs e)
        {
            HpUpdated?.Invoke(this, e);
        }

        public void SetThreatList(List<ThreatInfo> threatInfoList)
        {
            if (RecentThreatInfoListHistory.Count > 5)
            {
                RecentThreatInfoListHistory.TryDequeue(out _);
            }
            RecentThreatInfoListHistory.Enqueue(threatInfoList);

            ThreatInfoList = threatInfoList;

            OnThreatListUpdated(new ThreatListUpdatedEventArgs() { EntityUuid = UUID, ThreatInfoList = ThreatInfoList });
        }

        protected virtual void OnThreatListUpdated(ThreatListUpdatedEventArgs e)
        {
            ThreatListUpdated?.Invoke(this, e);
        }

        public void IncrementDeaths()
        {
            TotalDeaths++;
        }

        public void SetDeaths(ulong deaths)
        {
            TotalDeaths = deaths;
        }

        public void SetMonsterType(int type)
        {
            MonsterType = (EMonsterType)type;
        }

        public void AddRecentBuffEventHistory(int uuid, BuffEvent buffEvent)
        {
            if (RecentBuffEventHistory.Count > 20)
            {
                RecentBuffEventHistory.Remove(RecentBuffEventHistory.AsValueEnumerable().First().Key);
            }
            RecentBuffEventHistory[(ulong)uuid] = buffEvent;
        }

        public void RegisterSkillActivation(int skillId)
        {
            if (!SkillMetrics.TryGetValue(skillId, out var container))
            {
                container = new();

                if (HelperMethods.DataTables.Skills.Data.TryGetValue(skillId.ToString(), out var skill))
                {
                    container.Damage.SetName(skill.Name);
                    container.Healing.SetName(skill.Name);
                }

                container.Damage.RegisterActivation();
                container.Healing.RegisterActivation();
            }
            else
            {
                container.Damage.RegisterActivation();
                container.Healing.RegisterActivation();
            }

            TotalCasts++;
            OnSkillActivated(new SkillActivatedEventArgs { CasterUuid = UUID, SkillId = skillId, ActivationDateTime = DateTime.Now });
        }

        protected virtual void OnSkillActivated(SkillActivatedEventArgs e)
        {
            SkillActivated?.Invoke(this, e);
        }

        public void RegisterSkillData(ESkillType skillType, long otherUuid, int skillId, int skillLevel, long value, bool isCrit, bool isLucky, long hpLessenValue, long shieldBreak, bool isCauseLucky, EDamageProperty damageElement, EDamageType damageType, EDamageMode damageMode, bool isDead, Vec3 damagePos, Vector3? instigatorPos, Vector3? targetPos, ExtraPacketData extraPacketData)
        {
            if(!SkillMetrics.TryGetValue(skillId, out var container))
            {
                var combatStats = new CombatStats();

                combatStats.SetSkillType(skillType);

                if (HelperMethods.DataTables.Skills.Data.TryGetValue(skillId.ToString(), out var skill))
                {
                    combatStats.SetName(skill.Name);

                    var attrSkillLevelIdList = GetAttrKV("AttrSkillLevelIdList");
                    if (attrSkillLevelIdList != null)
                    {
                        if (attrSkillLevelIdList is List<DataTypes.Skills.SkillLevelInfo>)
                        {
                            var list = (List<DataTypes.Skills.SkillLevelInfo>)attrSkillLevelIdList;

                            int skillLevelGroup = skillId;
                            if (skill.SkillLevelGroup != 0)
                            {
                                skillLevelGroup = skill.SkillLevelGroup;
                            }

                            var knownSkill = list.Where(x => x.SkillId == skillLevelGroup).FirstOrDefault();
                            if (knownSkill != null && knownSkill != default)
                            {
                                combatStats.SetSummonData(combatStats.SummonUUID, knownSkill.Tier);
                            }
                        }
                    }
                }

                combatStats.AddData(otherUuid, skillId, skillLevel, value, isCrit, isLucky, hpLessenValue, shieldBreak, isCauseLucky, damageElement, damageType, damageMode, isDead, damagePos, instigatorPos, targetPos, extraPacketData, GetInactiveTime(), FirstCombatActionTime);

                container = new();
                if (skillType == ESkillType.Damage)
                {
                    container.Damage = combatStats;
                }
                else if (skillType == ESkillType.Healing)
                {
                    container.Healing = combatStats;
                }
                else if (skillType == ESkillType.Taken)
                {
                    container.Taken = combatStats;
                }
                else
                {
                    Serilog.Log.Warning($"RegisterSkillData SkillId {skillId} was an Unknown skill type and was not registered.");
                }

                SkillMetrics.TryAdd(skillId, container);
            }
            else
            {
                CombatStats combatStats;
                if (skillType == ESkillType.Damage)
                {
                    combatStats = container.Damage;
                }
                else if (skillType == ESkillType.Healing)
                {
                    combatStats = container.Healing;
                }
                else if (skillType == ESkillType.Taken)
                {
                    combatStats = container.Taken;
                }
                else
                {
                    combatStats = new();
                    Serilog.Log.Warning($"RegisterSkillData SkillId {skillId} was an Unknown skill type and was not updated.");
                }

                if (string.IsNullOrEmpty(combatStats.Name) && HelperMethods.DataTables.Skills.Data.TryGetValue(skillId.ToString(), out var skill))
                {
                    combatStats.SetName(skill.Name);

                    var attrSkillLevelIdList = GetAttrKV("AttrSkillLevelIdList");
                    if (attrSkillLevelIdList != null)
                    {
                        if (attrSkillLevelIdList is List<DataTypes.Skills.SkillLevelInfo>)
                        {
                            var list = (List<DataTypes.Skills.SkillLevelInfo>)attrSkillLevelIdList;

                            int skillLevelGroup = skillId;
                            if (skill.SkillLevelGroup != 0)
                            {
                                skillLevelGroup = skill.SkillLevelGroup;
                            }

                            var knownSkill = list.Where(x => x.SkillId == skillLevelGroup).FirstOrDefault();
                            if (knownSkill != null && knownSkill != default)
                            {
                                combatStats.SetSummonData(combatStats.SummonUUID, knownSkill.Tier);
                            }
                        }
                    }
                }

                combatStats.AddData(otherUuid, skillId, skillLevel, value, isCrit, isLucky, hpLessenValue, shieldBreak, isCauseLucky, damageElement, damageType, damageMode, isDead, damagePos, instigatorPos, targetPos, extraPacketData, GetInactiveTime(), FirstCombatActionTime);
            }

            if (!InteractedEntities.TryGetValue(otherUuid, out var statTracker))
            {
                statTracker = new();
            }

            statTracker.UUID = otherUuid;

            if (EntityCache.Instance.Cache.Lines.TryGetValue(otherUuid, out var cachedEntityLine))
            {
                statTracker.UID = cachedEntityLine.UID;
                statTracker.Name = cachedEntityLine.Name;
                statTracker.ProfessionId = cachedEntityLine.ProfessionId;
                statTracker.SubProfessionId = cachedEntityLine.SubProfessionId;
            }

            TrackedStats trackers;
            if (skillType == ESkillType.Damage)
            {
                statTracker.DidDamage = true;
                trackers = statTracker.Damage;
            }
            else if (skillType == ESkillType.Healing)
            {
                statTracker.DidHealing = true;
                trackers = statTracker.Healing;
            }
            else if (skillType == ESkillType.Taken)
            {
                statTracker.DidTaken = true;
                trackers = statTracker.Taken;
            }
            else
            {
                trackers = new();
            }

            if (damageType == EDamageType.Immune)
            {
                trackers.ImmuneCount++;
            }
            else if (damageType == EDamageType.Miss)
            {
                trackers.MissCount++;
            }
            else
            {
                trackers.TotalValue += (ulong)value;

                if (trackers.MinValue > value)
                {
                    trackers.MinValue = value;
                }
                if (trackers.MaxValue < (ulong)value)
                {
                    trackers.MaxValue = (ulong)value;
                }

                trackers.HitCount++;

                if (isCrit)
                {
                    trackers.CritCount++;
                    trackers.TotalCritValue += (ulong)value;
                }

                if (isCauseLucky)
                {
                    trackers.LuckyCount++;
                }
                if (isLucky)
                {
                    trackers.TotalLuckyValue += (ulong)value;
                }

                if (!isCrit && !isLucky)
                {
                    trackers.NormalCount++;
                }
            }

            InteractedEntities[otherUuid] = statTracker;
        }

        public void RecalculateInactiveTime(DateTime now, bool skipSave = false)
        {
            if (!skipSave)
            {
                FirstCombatActionTime ??= now;
            }

            DateTime? lastCombatAction = FirstCombatActionTime;

            if (LastCombatActionTime != null)
            {
                lastCombatAction = LastCombatActionTime.Value;
            }

            if (lastCombatAction == null)
            {
                return;
            }

            double inactiveSeconds = now.Subtract(lastCombatAction.Value).TotalSeconds;
            if (inactiveSeconds > 10.0)
            {
                InactiveTime = inactiveSeconds - 10.0;
            }

            if (!skipSave)
            {
                TotalInactiveTime += InactiveTime;
                InactiveTime = 0.0;
                LastCombatActionTime = now;
                FirstCombatActionTime ??= now;
            }
        }

        public double GetInactiveTime()
        {
            return TotalInactiveTime + InactiveTime;
        }

        public void AddDamage(long targetUuid, int skillId, int skillLevel, long damage, long hpLessen, long shieldBreak,
            EDamageProperty damageElement, EDamageType damageType, EDamageMode damageMode,
            bool isCrit, bool isLucky, bool isCauseLucky, bool isMiss, bool isDead, Vec3 damagePos, ExtraPacketData extraPacketData)
        {
            RecalculateInactiveTime(extraPacketData.ArrivalTime);

            if (damageType != EDamageType.Immune)
            {
                TotalDamage += (ulong)damage;
            }

            if (damageType == EDamageType.Absorbed)
            {
                TotalShieldBreak += (ulong)shieldBreak;
            }

            Vector3? instigatorPos = Position;
            Vector3? targetPos = null;
            var targetEntity = EncounterManager.Current.GetOrCreateEntity(targetUuid);
            if (targetEntity != null)
            {
                targetPos = targetEntity.Position;
            }

            DamageStats.AddData(targetUuid, skillId, skillLevel, damage, isCrit, isLucky, hpLessen, shieldBreak, isCauseLucky, damageElement, damageType, damageMode, isDead, damagePos, instigatorPos, targetPos, extraPacketData, GetInactiveTime(), FirstCombatActionTime);

            RegisterSkillData(ESkillType.Damage, targetUuid, skillId, skillLevel, damage, isCrit, isLucky, hpLessen, shieldBreak, isCauseLucky, damageElement, damageType, damageMode, isDead, damagePos, instigatorPos, targetPos, extraPacketData);

            var subProfessionId = Professions.GetSubProfessionIdBySkillId(skillId);
            if (subProfessionId != 0)
            {
                SetSubProfessionId((int)subProfessionId);
            }
        }

        public void AddHealing(
            long targetUuid, int skillId, int skillLevel, long damage, long overhealing, long effectiveHealing, long hpLessen, long shieldBreak,
            EDamageProperty damageElement, EDamageType damageType, EDamageMode damageMode,
            bool isCrit, bool isLucky, bool isCauseLucky, bool isMiss, bool isDead, Vec3 damagePos, ExtraPacketData extraPacketData)
        {
            RecalculateInactiveTime(extraPacketData.ArrivalTime);

            TotalHealing += (ulong)damage;
            TotalOverhealing += (ulong)overhealing;

            Vector3? instigatorPos = Position;
            Vector3? targetPos = null;
            var targetEntity = EncounterManager.Current.GetOrCreateEntity(targetUuid);
            if (targetEntity != null)
            {
                targetPos = targetEntity.Position;
            }

            HealingStats.AddData(targetUuid, skillId, skillLevel, damage, isCrit, isLucky, hpLessen, shieldBreak, isCauseLucky, damageElement, damageType, damageMode, isDead, damagePos, instigatorPos, targetPos, extraPacketData, GetInactiveTime(), FirstCombatActionTime);

            RegisterSkillData(ESkillType.Healing, targetUuid, skillId, skillLevel, damage, isCrit, isLucky, overhealing, shieldBreak, isCauseLucky, damageElement, damageType, damageMode, isDead, damagePos, instigatorPos, targetPos, extraPacketData);

            var subProfessionId = Professions.GetSubProfessionIdBySkillId(skillId);
            if (subProfessionId != 0)
            {
                SetSubProfessionId((int)subProfessionId);
            }
        }

        public void AddTakenDamage(
            long attackerUuid, int skillId, int skillLevel, long damage, long hpLessen, long shieldBreak,
            EDamageProperty damageElement, EDamageType damageType, EDamageMode damageMode,
            bool isCrit, bool isLucky, bool isCauseLucky, bool isMiss, bool isDead, Vec3 damagePos, ExtraPacketData extraPacketData)
        {
            RecalculateInactiveTime(extraPacketData.ArrivalTime);

            if (damageType != EDamageType.Immune)
            {
                TotalTakenDamage += (ulong)damage;
            }

            Vector3? victimPos = Position;
            Vector3? instigatorPos = null;
            var instigatorEntity = EncounterManager.Current.GetOrCreateEntity(attackerUuid);
            if (instigatorEntity != null)
            {
                instigatorPos = instigatorEntity.Position;
            }

            TakenStats.AddData(attackerUuid, skillId, skillLevel, damage, isCrit, isLucky, hpLessen, shieldBreak,isCauseLucky, damageElement, damageType, damageMode, isDead, damagePos, instigatorPos, victimPos, extraPacketData, GetInactiveTime(), FirstCombatActionTime);
            RegisterSkillData(ESkillType.Taken, attackerUuid, skillId, skillLevel, damage, isCrit, isLucky, hpLessen, shieldBreak, isCauseLucky, damageElement, damageType, damageMode, isDead, damagePos, instigatorPos, victimPos, extraPacketData);
        }

        public void NotifyBuffEvent(EBuffEventType buffEventType, int buffUuid, int baseId, int level, long fireUuid, string entityCasterName, int layer, int duration, int sourceConfigId, TimeSpan encounterTime, DateTime? creationTime, ExtraPacketData extraPacketData)
        {
            if (buffEventType == EBuffEventType.BuffEventRemove)
            {
                if (!BuffEvents.TryGetValue((ulong)buffUuid, out var buffEvent))
                {

                    buffEvent = new BuffEvent(buffUuid);
                }
                buffEvent.SetRemoveTime(encounterTime.Duration(), extraPacketData.ArrivalTime);

                if (Settings.Instance.LimitEncounterBuffTrackingInOpenWorld && BattleStateMachine.IsInOpenWorld() && BuffEvents.Count > 99)
                {
                    BuffEvents.Remove(BuffEvents.AsValueEnumerable().First().Key);
                }

                BuffEvents[(ulong)buffUuid] = buffEvent;
                AddRecentBuffEventHistory(buffUuid, buffEvent);
            }
            else
            {
                if (!BuffEvents.TryGetValue((ulong)buffUuid, out var buffEvent))
                {
                    buffEvent = new BuffEvent(buffUuid, baseId, level, fireUuid, entityCasterName, layer, duration, sourceConfigId);
                }
                else
                {
                    buffEvent.SetEvent(buffUuid, baseId, level, fireUuid, entityCasterName, layer, duration, sourceConfigId);
                }

                if (creationTime != null)
                {
                    var diff = extraPacketData.ArrivalTime.Subtract(creationTime.Value).TotalSeconds;
                    if (diff < -5 || diff > 0)
                    {
                        buffEvent.SetAddTime(encounterTime.Duration(), creationTime.Value);
                    }
                    else
                    {
                        buffEvent.SetAddTime(encounterTime.Duration(), extraPacketData.ArrivalTime);
                    }
                }
                else
                {
                    buffEvent.SetAddTime(encounterTime.Duration(), extraPacketData.ArrivalTime);
                }

                if (Settings.Instance.LimitEncounterBuffTrackingInOpenWorld && BattleStateMachine.IsInOpenWorld() && BuffEvents.Count > 99)
                {
                    BuffEvents.Remove(BuffEvents.AsValueEnumerable().First().Key);
                }

                BuffEvents[(ulong)buffUuid] = buffEvent;
                AddRecentBuffEventHistory(buffUuid, buffEvent);
            }
        }

        public void AddBuffEventAttribute(int buffUuid, string attributeName, object? attributeValue)
        {
            if (!BuffEvents.TryGetValue((ulong)buffUuid, out var buffEvent))
            {
                buffEvent = new BuffEvent(buffUuid);

                if (attributeName == "AttrShieldList" && attributeValue != null)
                {
                    var shieldInfo = (ShieldInfo)attributeValue;
                    TotalShield += (ulong)shieldInfo.InitialValue;
                }
            }
            buffEvent.AddData(attributeName, attributeValue);

            if (Settings.Instance.LimitEncounterBuffTrackingInOpenWorld && BattleStateMachine.IsInOpenWorld() && BuffEvents.Count > 99)
            {
                BuffEvents.Remove(BuffEvents.AsValueEnumerable().First().Key);
            }

            BuffEvents[(ulong)buffUuid] = buffEvent;
        }

        public void SetAttrKV(string key, object value)
        {
            Attributes[key] = value;
        }

        public object? GetAttrKV(string key)
        {
            var value = Attributes.TryGetValue(key, out var val) ? val : null;
            return value;
        }

        public void SetTempAttrKV(int key, TempAttributesContainer value)
        {
            TempAttributes[key] = value;
        }

        public TempAttributesContainer? GetTempAttrKV(int key)
        {
            var value = TempAttributes.TryGetValue(key, out var val) ? val : null;
            return value;
        }

        public bool IsHpUpdatedHandlerSubscribed(HpUpdatedEventHandler handler)
        {
            Delegate[]? invocationList = HpUpdated?.GetInvocationList();
            return invocationList != null && invocationList.Contains(handler);
        }

        public bool IsThreatListUpdatedHandlerSubscribed(ThreatListUpdatedEventHandler handler)
        {
            Delegate[]? invocationList = ThreatListUpdated?.GetInvocationList();
            return invocationList != null && invocationList.Contains(handler);
        }

        public void MergeEntity(Entity newEntity)
        {
            Name = newEntity.Name;
            Level = newEntity.Level;
            if (newEntity.AbilityScore > 0)
            {
                SetAbilityScore(newEntity.AbilityScore);
            }
            if (newEntity.ProfessionId > 0)
            {
                SetProfessionId(newEntity.ProfessionId);
            }
            if (newEntity.SubProfessionId > 0)
            {
                SetSubProfessionId(newEntity.SubProfessionId);
            }

            TotalDamage += newEntity.TotalDamage;
            TotalShieldBreak += newEntity.TotalShieldBreak;
            TotalHealing += newEntity.TotalHealing;
            TotalOverhealing += newEntity.TotalOverhealing;
            TotalTakenDamage += newEntity.TotalTakenDamage;
            TotalShield += newEntity.TotalShield;
            TotalCasts += newEntity.TotalCasts;
            TotalDeaths += newEntity.TotalDeaths;
            TotalInactiveTime += newEntity.TotalInactiveTime;
            InactiveTime += newEntity.InactiveTime;

            if (newEntity.FirstCombatActionTime.HasValue)
            {
                if (FirstCombatActionTime.HasValue)
                {
                    if (newEntity.FirstCombatActionTime.Value < FirstCombatActionTime.Value)
                    {
                        FirstCombatActionTime = newEntity.FirstCombatActionTime.Value;
                    }
                }
                else
                {
                    FirstCombatActionTime = newEntity.FirstCombatActionTime.Value;
                }
            }

            if (newEntity.LastCombatActionTime.HasValue)
            {
                if (LastCombatActionTime.HasValue)
                {
                    if (newEntity.LastCombatActionTime.Value > LastCombatActionTime.Value)
                    {
                        LastCombatActionTime = newEntity.LastCombatActionTime.Value;
                    }
                }
                else
                {
                    LastCombatActionTime = newEntity.LastCombatActionTime.Value;
                }
            }

            DamageStats.MergeCombatStats(newEntity.DamageStats);
            HealingStats.MergeCombatStats(newEntity.HealingStats);
            TakenStats.MergeCombatStats(newEntity.TakenStats);

            foreach (var newAttr in newEntity.Attributes)
            {
                if (Attributes.ContainsKey(newAttr.Key))
                {
                    Attributes[newAttr.Key] = newAttr.Value;
                }
                else
                {
                    Attributes.Add(newAttr.Key, newAttr.Value);
                }
            }

            foreach (var newSkillMetrics in newEntity.SkillMetrics)
            {
                SkillMetrics.TryGetValue(newSkillMetrics.Key, out var foundSkill);
                if (foundSkill != null)
                {
                    foundSkill.Damage.MergeCombatStats(newSkillMetrics.Value.Damage);
                    foundSkill.Healing.MergeCombatStats(newSkillMetrics.Value.Healing);
                    foundSkill.Taken.MergeCombatStats(newSkillMetrics.Value.Taken);
                }
                else
                {
                    var metrics = new MetricsContainer
                    {
                        Damage = (CombatStats)newSkillMetrics.Value.Damage.Clone(),
                        Healing = (CombatStats)newSkillMetrics.Value.Healing.Clone(),
                        Taken = (CombatStats)newSkillMetrics.Value.Taken.Clone(),
                    };
                    SkillMetrics.TryAdd(newSkillMetrics.Key, metrics);
                }
            }

            foreach (var newInteractedEntities in newEntity.InteractedEntities)
            {
                InteractedEntities.TryGetValue(newInteractedEntities.Key, out var foundInteracted);
                if (foundInteracted != null)
                {
                    if (!foundInteracted.DidDamage)
                    {
                        foundInteracted.DidDamage = newInteractedEntities.Value.DidDamage;
                    }
                    if (!foundInteracted.DidHealing)
                    {
                        foundInteracted.DidHealing = newInteractedEntities.Value.DidHealing;
                    }
                    if (!foundInteracted.DidTaken)
                    {
                        foundInteracted.DidTaken = newInteractedEntities.Value.DidTaken;
                    }
                    foundInteracted.Damage.MergeTrackedStats(newInteractedEntities.Value.Damage);
                    foundInteracted.Healing.MergeTrackedStats(newInteractedEntities.Value.Healing);
                    foundInteracted.Taken.MergeTrackedStats(newInteractedEntities.Value.Taken);
                }
                else
                {
                    var statTracker = new StatTracker
                    {
                        UUID = newInteractedEntities.Value.UUID,
                        UID = newInteractedEntities.Value.UID,
                        Name = newInteractedEntities.Value.Name,
                        ProfessionId = newInteractedEntities.Value.ProfessionId,
                        SubProfessionId = newInteractedEntities.Value.SubProfessionId,
                        DidDamage = newInteractedEntities.Value.DidDamage,
                        DidHealing = newInteractedEntities.Value.DidHealing,
                        DidTaken = newInteractedEntities.Value.DidTaken,
                        Damage = (TrackedStats)newInteractedEntities.Value.Damage.Clone(),
                        Healing = (TrackedStats)newInteractedEntities.Value.Healing.Clone(),
                        Taken = (TrackedStats)newInteractedEntities.Value.Taken.Clone(),
                    };
                    InteractedEntities.TryAdd(newInteractedEntities.Key, statTracker);
                }
            }
        }
    }

    public enum EMonsterType : int
    {
        Unknown = -1,
        Monster = 0,
        Elite = 1,
        Boss = 2
    }

    public class SkillActivatedEventArgs : EventArgs
    {
        public long CasterUuid { get; set; }
        public int SkillId { get; set; }
        public DateTime ActivationDateTime { get; set; }
    }

    public class HpUpdatedEventArgs : EventArgs
    {
        public long EntityUuid { get; set; }
        public long Hp { get; set; }
        public long MaxHp { get; set; }
        public DateTime UpdateDateTime { get; set; }
    }

    public class ThreatInfo
    {
        public long EntityUuid { get; set; }
        public long ThreatValue { get; set; }
    }

    public class ThreatListUpdatedEventArgs : EventArgs
    {
        public long EntityUuid { get; set; }
        public List<ThreatInfo> ThreatInfoList { get; set; } = new();
    }

    public class BuffUpdatedEventArgs : EventArgs
    {
        public long EntityUuid { get; set; }
        public EBuffEventType BuffEventType { get; set; }
        public int BuffUuid { get; set; }
        public int BaseId { get; set; }
        public int Level { get; set; }
        public long FireUuid { get; set; }
        public int Layer { get; set; }
        public int Duration { get; set; }
        public int SourceConfigId { get; set; }
        public string EntityCasterName { get; set; } = null!;
        public DateTime UpdateDateTime { get; set; }
        public DateTime? CreationDateTime { get; set; }
    }

    public class AttributeUpdatedEventArgs : EventArgs
    {
        public long EntityUuid { get; set; }
        public Entity? Entity { get; set; }
        public string AttributeName { get; set; } = null!;
        public object AttributeValue { get; set; } = null!;
    }

    public enum ESkillType : int
    {
        Unknown = 0,
        Damage = 1,
        Healing = 2,
        Taken = 3
    }

    public class MetricsContainer
    {
        public CombatStats Damage = new();
        public CombatStats Healing = new();
        public CombatStats Taken = new();
    }

    public class StatTracker
    {
        public long UUID { get; set; } = 0;
        public long UID { get; set; } = 0;
        public string Name { get; set; } = "";
        public int ProfessionId { get; set; } = 0;
        public int SubProfessionId { get; set; } = 0;
        public bool DidDamage { get; set; } = false;
        public bool DidHealing { get; set; } = false;
        public bool DidTaken { get; set; } = false;
        public TrackedStats Damage { get; set; } = new();
        public TrackedStats Healing { get; set; } = new();
        public TrackedStats Taken { get; set; } = new();
    }

    public class TrackedStats : System.ICloneable
    {
        public ulong TotalValue { get; set; }
        public ulong TotalCritValue { get; set; }
        public ulong TotalLuckyValue { get; set; }
        public ulong HitCount { get; set; }
        public ulong NormalCount { get; set; }
        public ulong CritCount { get; set; }
        public ulong MissCount { get; set; }
        public ulong LuckyCount { get; set; }
        public ulong ImmuneCount { get; set; }
        public ulong MaxValue { get; set; }
        public long MinValue { get; set; }

        public object Clone()
        {
            return this.MemberwiseClone();
        }

        public void MergeTrackedStats (TrackedStats newTrackedStats)
        {
            TotalValue += newTrackedStats.TotalValue;
            TotalCritValue += newTrackedStats.TotalCritValue;
            TotalLuckyValue += newTrackedStats.TotalLuckyValue;
            HitCount += newTrackedStats.HitCount;
            NormalCount += newTrackedStats.NormalCount;
            CritCount += newTrackedStats.CritCount;
            MissCount += newTrackedStats.MissCount;
            LuckyCount += newTrackedStats.LuckyCount;
            ImmuneCount += newTrackedStats.ImmuneCount;

            if (newTrackedStats.MaxValue > MaxValue)
            {
                MaxValue = newTrackedStats.MaxValue;
            }

            if (newTrackedStats.MinValue < MinValue)
            {
                MinValue = newTrackedStats.MinValue;
            }
        }
    }

    public class CombatStats : System.ICloneable
    {
        public string Name { get; private set; } = null!;
        public ESkillType SkillType { get; private set; } = ESkillType.Unknown;
        public int Id { get; private set; }
        public int Level { get; private set; }
        public int TierLevel { get; private set; }
        public long SummonUUID { get; private set; }

        public EDamageProperty DamageElement { get; private set; }
        public EDamageMode DamageMode { get; private set; }

        public ulong ValueTotal { get; private set; }
        public ulong ValueNormalTotal { get; private set; }
        public ulong ValueCritTotal { get; private set; }
        public ulong ValueLuckyTotal { get; private set; }
        public ulong ValueCritLuckyTotal { get; private set; }
        public ulong ValueImmuneTotal { get; private set; }
        public long ValueMax { get; private set; }
        public long ValueMin { get; private set; }
        public double ValueAverage { get; private set; }
        public double ValuePerSecond { get; set; }
        public double ValuePerSecondActive { get; set; }
        public double TrueValuePerSecond { get; set; }

        public ulong HpLessenTotal { get; private set; }
        public ulong ShieldBreakTotal { get; private set; }

        public uint MissCount { get; private set; }
        public double MissRate { get; private set; }

        public uint CritCount { get; private set; }
        public double CritRate { get; private set; }

        public uint LuckyCount { get; private set; }
        public uint LuckyHitCount { get; private set; }
        public double LuckyRate { get; private set; }

        public uint CritLuckyCount { get; private set; }

        public uint NormalCount { get; private set; }
        public uint KillCount { get; private set; }
        public ulong HitsCount { get; private set; }
        public uint CastsCount { get; private set; }
        public ulong ImmuneCount { get; private set; }

        public DateTime? StartTime = null;
        public DateTime? EndTime = null;
        public DateTime? EntityStartTime = null;
        public double InactiveTime = 0.0;

        public List<SkillSnapshot> SkillSnapshots { get; private set; } = new();

        public object Clone()
        {
            return this.MemberwiseClone();
        }

        public void SetName(string name)
        {
            Name = name;
        }

        public void SetSkillType(ESkillType skillType)
        {
            this.SkillType = skillType;
        }

        public void RegisterActivation()
        {
            CastsCount++;
        }

        private void AddValue(long value)
        {
            ValueTotal += (ulong)value;

            if (value > 0)
            {
                if (value < ValueMin)
                {
                    ValueMin = value;
                }
                if (value > ValueMax)
                {
                    ValueMax = value;
                }
            }
        }

        private void AddNormalValue(long value)
        {
            ValueNormalTotal += (ulong)value;
        }

        private void AddCritValue(long value)
        {
            ValueCritTotal += (ulong)value;
        }

        private void AddLuckyValue(long value)
        {
            ValueLuckyTotal += (ulong)value;
        }

        private void AddImmuneValue(long value)
        {
            ValueImmuneTotal += (ulong)value;
        }

        public void RecalculatePerSecond(DateTime? endTime)
        {
            DateTime? end = EndTime;
            if (endTime != null)
            {
                end = endTime;
            }

            if (StartTime != null && end != null && StartTime <= end)
            {
                var seconds = (end.Value - StartTime.Value).TotalSeconds;
                if (seconds >= 1.0)
                {
                    ValuePerSecond = seconds > 0 ? Math.Round((double)ValueTotal / seconds, 0) : 0;
                }
                else
                {
                    ValuePerSecond = ValueTotal;
                }
            }

            if (EntityStartTime != null && end != null && EntityStartTime <= end)
            {
                var seconds = (end.Value - EntityStartTime.Value).TotalSeconds - InactiveTime;
                if (seconds >= 1.0)
                {
                    ValuePerSecondActive = seconds > 0 ? Math.Round((double)ValueTotal / seconds, 0) : 0;
                }
                else
                {
                    ValuePerSecondActive = ValueTotal;
                }
            }
        }

        public void AddData(long otherUuid, int skillId, int level, long value, bool isCrit, bool isLucky, long hpLessenValue, long shieldBreak, bool isCauseLucky, EDamageProperty damageElement, EDamageType damageType, EDamageMode damageMode, bool isDead, Vec3 damagePos, Vector3? instigatorPos, Vector3? targetPos, ExtraPacketData extraPacketData, double inactiveTime, DateTime? startTime)
        {
            DateTime now = extraPacketData.ArrivalTime;
            InactiveTime = inactiveTime;
            StartTime ??= EncounterManager.Current.ExData.FirstDamageTimeStamp;
            EntityStartTime ??= startTime;
            EndTime = now;

            Id = skillId;
            Level = level;

            DamageElement = damageElement;
            DamageMode = damageMode;

            if (damageType == EDamageType.Immune)
            {
                ImmuneCount++;
                AddImmuneValue(value);
            }
            else if (damageType == EDamageType.Miss)
            {
                MissCount++;
                if (MissCount > 0 && HitsCount == 0)
                {
                    MissRate = 100.0;
                }
                else
                {
                    MissRate = MissCount > 0 ? Math.Round(((double)MissCount / (double)HitsCount) * 100.0, 0) : 0.0;
                }
            }
            else
            {
                AddValue(value);

                HpLessenTotal += (ulong)hpLessenValue;
                ShieldBreakTotal += (ulong)shieldBreak;

                if (isCrit)
                {
                    CritCount++;
                    AddCritValue(value);
                }

                if (isCauseLucky)
                {
                    LuckyCount++;
                }
                if (isLucky)
                {
                    LuckyHitCount++;
                    AddLuckyValue(value);
                }

                if (!isCrit && !isLucky)
                {
                    NormalCount++;
                    AddNormalValue(value);
                }
                else if (isCrit && isLucky)
                {
                    CritLuckyCount++;
                    ValueCritLuckyTotal += (ulong)value;
                }

                if (isDead)
                {
                    KillCount++;
                }

                HitsCount++;
            }

            ValueAverage = HitsCount > 0 ? Math.Round(((double)ValueTotal / (double)HitsCount), 0) : 0.0;
            CritRate = HitsCount > 0 ? Math.Round(((double)CritCount / (double)HitsCount) * 100.0, 0) : 0.0;
            LuckyRate = HitsCount > 0 && HitsCount >= LuckyHitCount ? Math.Round(((double)LuckyHitCount / Math.Clamp((double)(HitsCount - LuckyHitCount), 1, double.MaxValue)) * 100.0, 0) : 0.0;

            if (StartTime != null && EndTime != null && StartTime <= EndTime)
            {
                var seconds = (EndTime.Value - StartTime.Value).TotalSeconds;
                if (seconds >= 1.0)
                {
                    ValuePerSecond = seconds > 0 ? Math.Round((double)ValueTotal / seconds, 0) : 0;
                }
                else
                {
                    ValuePerSecond = ValueTotal;
                }
            }

            if (EntityStartTime != null && EndTime != null && EntityStartTime <= EndTime)
            {
                var seconds = (EndTime.Value - EntityStartTime.Value).TotalSeconds - InactiveTime;
                if (seconds >= 1.0)
                {
                    ValuePerSecondActive = seconds > 0 ? Math.Round((double)ValueTotal / seconds, 0) : 0;
                }
                else
                {
                    ValuePerSecondActive = ValueTotal;
                }
            }

            if (Settings.Instance.SkipSkillSnapshotSavingInOpenWorld && BattleStateMachine.IsInOpenWorld())
            {
                return;
            }

            AddSnapshot(otherUuid, skillId, level, value, isCrit, isLucky, hpLessenValue, shieldBreak, isCauseLucky, damageElement, damageType, damageMode, isDead, damagePos, instigatorPos, targetPos, now);
        }

        public void SetSummonData(long uuid, int level)
        {
            SummonUUID = uuid;
            TierLevel = level;
        }

        public void AddSnapshot(long otherUuid, int id, int level, long value, bool isCrit, bool isLucky, long hpLessenValue, long shieldBreak, bool isCauseLucky, EDamageProperty damageElement, EDamageType damageType, EDamageMode damageMode, bool isDead, Vec3 damagePos, Vector3? instigatorPos, Vector3? targetPos, DateTime timestamp)
        {
            var snapshot = new SkillSnapshot()
            {
                OtherUUID = otherUuid,
                Id = id,
                Level = level,
                Value = value,
                HpLessen = hpLessenValue,
                ShieldBreak = shieldBreak,
                IsCrit = isCrit,
                IsLucky = isLucky,
                IsCritLucky = isCrit && isLucky,
                IsCauseLucky = isCauseLucky,
                DamageElement = damageElement,
                DamageType = damageType,
                DamageMode = damageMode,
                IsKill = isDead,
                Timestamp = timestamp,
                HitPosition = damagePos.ToVector3(),
                InstigatorPos = instigatorPos,
                TargetPos = targetPos,
            };

            if (damageType == EDamageType.Miss)
            {
                snapshot.IsMiss = true;
            }
            else
            {
                snapshot.IsHit = true;
            }

            if (damageType == EDamageType.Immune)
            {
                snapshot.IsImmune = true;
            }

            SkillSnapshots.Add(snapshot);
        }

        public void MergeCombatStats(CombatStats newCombatStats)
        {
            if (!string.IsNullOrEmpty(newCombatStats.Name))
            {
                SetName(newCombatStats.Name);
            }
            SetSkillType(newCombatStats.SkillType);

            ValueTotal += newCombatStats.ValueTotal;
            ValueNormalTotal += newCombatStats.ValueNormalTotal;
            ValueCritTotal += newCombatStats.ValueCritTotal;
            ValueLuckyTotal += newCombatStats.ValueLuckyTotal;
            ValueCritLuckyTotal += newCombatStats.ValueCritLuckyTotal;
            ValueImmuneTotal += newCombatStats.ValueImmuneTotal;
            ValueMax = newCombatStats.ValueMax > ValueMax ? newCombatStats.ValueMax : ValueMax;
            ValueMin = newCombatStats.ValueMin < ValueMin ? newCombatStats.ValueMin : ValueMin;

            HpLessenTotal += newCombatStats.HpLessenTotal;
            ShieldBreakTotal += newCombatStats.ShieldBreakTotal;

            MissCount += newCombatStats.MissCount;
            CritCount += newCombatStats.CritCount;
            LuckyCount += newCombatStats.LuckyCount;
            LuckyHitCount += newCombatStats.LuckyHitCount;
            CritLuckyCount += newCombatStats.CritLuckyCount;
            NormalCount += newCombatStats.NormalCount;
            KillCount += newCombatStats.KillCount;
            HitsCount += newCombatStats.HitsCount;
            CastsCount += newCombatStats.CastsCount;
            ImmuneCount += newCombatStats.ImmuneCount;

            ValueAverage = HitsCount > 0 ? Math.Round(((double)ValueTotal / (double)HitsCount), 0) : 0.0;
            CritRate = HitsCount > 0 ? Math.Round(((double)CritCount / (double)HitsCount) * 100.0, 0) : 0.0;
            LuckyRate = HitsCount > 0 && HitsCount >= LuckyHitCount ? Math.Round(((double)LuckyHitCount / Math.Clamp((double)(HitsCount - LuckyHitCount), 1, double.MaxValue)) * 100.0, 0) : 0.0;

            if (MissCount > 0 && HitsCount == 0)
            {
                MissRate = 100.0;
            }
            else
            {
                MissRate = MissCount > 0 ? Math.Round(((double)MissCount / (double)HitsCount) * 100.0, 0) : 0.0;
            }

            if (newCombatStats.StartTime.HasValue)
            {
                if (StartTime.HasValue)
                {
                    if (newCombatStats.StartTime.Value < StartTime.Value)
                    {
                        StartTime = newCombatStats.StartTime.Value;
                    }
                }
                else
                {
                    StartTime = newCombatStats.StartTime.Value;
                }
            }

            if (newCombatStats.EndTime.HasValue)
            {
                if (EndTime.HasValue)
                {
                    if (newCombatStats.EndTime.Value > EndTime.Value)
                    {
                        EndTime = newCombatStats.EndTime.Value;
                    }
                }
                else
                {
                    EndTime = newCombatStats.EndTime.Value;
                }
            }

            if (newCombatStats.EntityStartTime.HasValue)
            {
                if (EntityStartTime.HasValue)
                {
                    if (newCombatStats.EntityStartTime.Value < EntityStartTime.Value)
                    {
                        EntityStartTime = newCombatStats.EntityStartTime.Value;
                    }
                }
                else
                {
                    EntityStartTime = newCombatStats.EntityStartTime.Value;
                }
            }

            InactiveTime += newCombatStats.InactiveTime;

            if (StartTime != null && EndTime != null && StartTime < EndTime)
            {
                var seconds = (EndTime.Value - StartTime.Value).TotalSeconds;
                if (seconds >= 1.0)
                {
                    ValuePerSecond = seconds > 0 ? Math.Round((double)ValueTotal / seconds, 0) : 0;
                }
                else
                {
                    ValuePerSecond = ValueTotal;
                }
            }

            if (EntityStartTime != null && EndTime != null && EntityStartTime <= EndTime)
            {
                var seconds = (EndTime.Value - EntityStartTime.Value).TotalSeconds - InactiveTime;
                if (seconds >= 1.0)
                {
                    ValuePerSecondActive = seconds > 0 ? Math.Round((double)ValueTotal / seconds, 0) : 0;
                }
                else
                {
                    ValuePerSecondActive = ValueTotal;
                }
            }

            foreach (var newSnapshot in newCombatStats.SkillSnapshots)
            {
                SkillSnapshots.Add((SkillSnapshot)newSnapshot.Clone());
            }
        }
    }

    public class SkillSnapshot : System.ICloneable
    {

        public long OtherUUID { get; set; }

        public int Id { get; set; }
        public int Level { get; set; }

        public long Value { get; set; }
        public long HpLessen { get; set; }
        public long ShieldBreak { get; set; }

        public EDamageProperty DamageElement { get; set; }
        public EDamageType DamageType { get; set; }
        public EDamageMode DamageMode { get; set; }

        public bool IsCrit { get; set; }
        public bool IsLucky { get; set; }
        public bool IsCritLucky { get; set; }
        public bool IsCauseLucky { get; set; }
        public bool IsHit { get; set; }
        public bool IsMiss { get; set; }
        public bool IsKill { get; set; }
        public bool IsImmune { get; set; }

        public DateTime? Timestamp { get; set; } = null;
        public Vector3 HitPosition { get; set; }

        public Vector3? InstigatorPos { get; set; } = null;

        public Vector3? TargetPos { get; set; } = null;

        public object Clone()
        {
            return this.MemberwiseClone();
        }
    }

    public class BuffEvent
    {
        public DataTypes.Enum.EBuffType BuffType { get; private set; }
        public DataTypes.Enum.EBuffPriority BuffPriority { get; private set; }
        public int BuffVisibility { get; private set; }
        public long Uuid { get; private set; }
        public int BaseId { get; private set; }
        public int Level { get; private set; }
        public long FireUuid { get; private set; }
        public string EntityCasterName { get; private set; } = null!;
        public int Layer { get; private set; }
        public int Duration { get; private set; }
        public int SourceConfigId { get; private set; }
        public string Name { get; private set; } = null!;
        [JsonIgnore]
        public string Description { get; private set; } = "";
        public string Icon { get; private set; } = null!;
        public int BuffAbilityType { get; private set; }
        public int BuffAbilitySubType { get; private set; }
        public TimeSpan EventAddTime { get; private set; }
        public TimeSpan EventRemoveTime { get; private set; }
        public string AttributeName { get; private set; } = null!;
        public object? Data { get; private set; }
        public DateTime AddDateTime { get; private set; }
        public DateTime RemoveDateTime { get; private set; }

        public BuffEvent(long uuid)
        {
            Uuid = uuid;
        }

        [JsonConstructor]
        public BuffEvent(int uuid, int baseId, int level, long fireUuid, string entityCasterName, int layer, int duration, int sourceConfigId)
        {
            Uuid = uuid;
            BaseId = baseId;
            Level = level;
            FireUuid = fireUuid;
            EntityCasterName = entityCasterName;
            Layer = layer;
            Duration = duration;
            SourceConfigId = sourceConfigId;

            if (BaseId > 0)
            {
                if (HelperMethods.DataTables.Buffs.Data.TryGetValue(baseId.ToString(), out var buffTableData))
                {
                    Name = buffTableData.Name;
                    Icon = buffTableData.GetIconName();
                    BuffType = buffTableData.BuffType!.Value;
                    BuffPriority = buffTableData.BuffPriority!.Value;
                    BuffVisibility = buffTableData.Visible;
                    BuffAbilityType = buffTableData.BuffAbilityType;
                    BuffAbilitySubType = buffTableData.BuffAbilitySubType;
                }
            }

            if (sourceConfigId > 0)
            {
                if (HelperMethods.DataTables.Skills.Data.TryGetValue(sourceConfigId.ToString(), out var skillTableData))
                {

                    if (string.IsNullOrWhiteSpace(Icon))
                    {
                        Icon = skillTableData.GetIconName();
                    }
                }
            }
        }

        public void AddData(string attributeName, object? data)
        {
            AttributeName = attributeName;
            Data = data;
        }

        public void SetAddTime(TimeSpan time, DateTime dateTime)
        {
            EventAddTime = time;
            AddDateTime = dateTime;
        }

        public void SetRemoveTime(TimeSpan time, DateTime dateTime)
        {
            EventRemoveTime = time;
            RemoveDateTime = dateTime;
        }

        public void SetEntitySourceNameFromUuid(string name)
        {

        }

        public void SetDescription(string value)
        {
            Description = value;
        }

        public void SetEvent(int uuid, int baseId, int level, long fireUuid, string entityCasterName, int layer, int duration, int sourceConfigId)
        {
            Uuid = uuid;
            if (baseId > 0)
            {
                BaseId = baseId;
            }
            if (level > 0)
            {
                Level = level;
            }
            if (fireUuid > 0)
            {
                FireUuid = fireUuid;
            }
            if (!string.IsNullOrEmpty(entityCasterName))
            {
                EntityCasterName = entityCasterName;
            }
            Layer = layer;
            Duration = duration;
            if (sourceConfigId > 0)
            {
                SourceConfigId = sourceConfigId;
            }

            if (BaseId > 0)
            {
                if (HelperMethods.DataTables.Buffs.Data.TryGetValue(baseId.ToString(), out var buffTableData))
                {
                    Name = buffTableData.Name;
                    Icon = buffTableData.GetIconName();

                    BuffType = buffTableData.BuffType!.Value;
                    BuffPriority = buffTableData.BuffPriority!.Value;
                    BuffVisibility = buffTableData.Visible;
                    BuffAbilityType = buffTableData.BuffAbilityType;
                    BuffAbilitySubType = buffTableData.BuffAbilitySubType;
                }
            }
        }
    }
}
