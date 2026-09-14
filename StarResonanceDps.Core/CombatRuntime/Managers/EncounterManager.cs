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

        /// <summary>
        /// チャンネル(回線)番号。チャンネルの無い場所では 0。出所は <c>SceneData.LineId</c>。
        /// エンカウンターは頻繁に作り直されるため、Encounter ではなくここで保持する。
        /// </summary>
        public static uint ChannelLineId { get; private set; }


        /// <summary>
        /// チャンネル番号を反映する。
        ///
        /// <para>
        /// <b><c>SceneData.SceneGuid</c> はシーンのキーに使えない</b> — 移動元の guid を
        /// 載せたまま遅れて届いたり、同じマップが別々の guid で届いたりする。
        /// シーン変化の判定は <c>LevelMapId</c> と <c>LineId</c> で行うこと。
        /// </para>
        /// </summary>
        public static void SetChannelLineId(uint lineId)
        {
            ChannelLineId = lineId;
        }
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

        public static void EnterDungeon(
            bool force = false,
            EncounterStartReason reason = EncounterStartReason.None,
            [System.Runtime.CompilerServices.CallerMemberName] string enterDungeonCaller = "")
        {
            if (AppState.IsBenchmarkMode
                && (AppState.IsBenchmarkCompleting || AppState.IsBenchmarkCompleted)
                && reason != EncounterStartReason.BenchmarkEnd)
            {
                return;
            }

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

                // 終了時点の秒間値を確定させる。回復だけのエンカウンターも対象。
                if (Current.TotalDamage > 0 || Current.TotalHealing > 0)
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
                // 戦闘データの無いエンカウンターは保存もせず、採番も進めない。
                // 実測(728件)で65%が空で、履歴の一覧がそれで埋まるため 2026-09-12 に固定した。
                nextEncounterIdModifier = Current.HasStatsBeenRecorded() ? 1UL : 0UL;
            }

            if (MessageManager.currentUserUuid != 0)
            {
                Entity? priorSelf = null;
                priorEncounter?.Entities.TryGetValue(MessageManager.currentUserUuid, out priorSelf);
            }

            Current = new Encounter(CurrentBattleId);
            Current.EncounterId = DB.GetNextEncounterId() + nextEncounterIdModifier;
            System.Diagnostics.Debug.WriteLine($"Created new encounter for EncounterId {Current.EncounterId} + ({nextEncounterIdModifier})");
            // **Force も引き継ぐ。** 素性を捨ててよい区切りは無い。
            // 区切りの強制は reason ではなく別引数の force が担う。
            if (priorEncounter != null && reason != EncounterStartReason.None)
            {

                var priorCharacters = priorEncounter.Entities.AsValueEnumerable().Where(x => x.Value.EntityType == EEntityType.EntChar);
                foreach (var priorChar in priorCharacters)
                {
                    var newChar = Current.GetOrCreateEntity(priorChar.Key);
                    newChar.SetHpValuesNoUpdate(priorChar.Value.Hp, priorChar.Value.MaxHp);
                    newChar.Attributes = priorChar.Value.Attributes.ToDictionary();

                    // 素性(名前・職業・特化・マーカー等)を丸ごと引き継ぐ。統計は新品のまま。
                    // **Attributes のコピーだけでは足りない** — 特化とマーカーはそこに入らない。
                    newChar.CopyIdentityFrom(priorChar.Value);
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

                    // 全滅リセットの直後に目標が切り替わると、ボスはまだキャッシュのままで実体に戻っていない。
                    // 実体だけを運ぶとキャッシュごと失われ、出現時にしか届かない AttrId が無くなるので、残ったキャッシュも渡す。
                    foreach (var (uuid, bossCache) in priorEncounter.PreviousBossCache)
                    {
                        if (bossCache.Hp > 0 && !Current.Entities.ContainsKey(uuid))
                        {
                            Current.PreviousBossCache[uuid] = bossCache;
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
                if (nextEncounterIdModifier != 0)
                {
                    ApplyDisplayedIdentitiesForRecord(priorEncounter);
                    DB.InsertEncounter(priorEncounter);
                    GC.Collect();
                }
            }

            AllowSceneUpdate = true;

            // エンカウンターの作り直しも「次のイベント」。3分計測・リセット・マップ移動・
            // フェーズ分割は全部ここを通るので、ボタン側に専用の解除を書かない。
            EncounterHistoryProvider.NotifyLiveEncounterEvent();

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
                Current.SetEndTime(
                    AppState.IsBenchmarkCompleted && AppState.BenchmarkCompletionTime is { } completionTime
                        ? completionTime
                        : DateTime.Now);
            }

            UpdateTruePerValuesCTS.Cancel();

            if (Current != null && Current.HasStatsBeenRecorded())
            {
                DB.InsertEncounter(Current);
            }

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

        /// <summary>
        /// 保存の直前に、<b>いま画面に出している素性をエンティティへ焼き付ける。</b>
        ///
        /// <para>
        /// エンティティの生の値は表示値と一致しない。AOI退出で特化は消され
        /// (<c>ClearTransientHumanSubProfession</c>)、PT外の人の名前・職業・戦闘力は
        /// PT補完や各キャッシュが埋めている。**どちらも blob に載らない**ので、
        /// そのまま保存すると履歴が Unknown だらけになる。
        /// </para>
        ///
        /// <para>
        /// 出所は <see cref="PlayerRosterStore"/> のスナップショット。合成済みの表示値がそのまま入っている。
        /// この時点ではまだ生きている — マップ移動でも <c>StartNewMap</c> → <c>EnterDungeon</c>(ここ) の後に
        /// <c>PlayerRosterProjection.BeginMap()</c> がロスターとキャッシュを捨てる。
        /// </para>
        ///
        /// <para>
        /// ロスターに載らない人(統計ゼロ)はどこにも表示されないので埋めない。
        /// </para>
        /// </summary>
        private static void ApplyDisplayedIdentitiesForRecord(Encounter encounter)
        {
            var roster = Services.PlayerRosterStore.Instance.Current.Entries;
            if (roster.Count == 0)
            {
                return;
            }

            var displayedByCharacterId = roster
                .Where(entry => entry.CharacterId != 0)
                .GroupBy(entry => entry.CharacterId)
                .ToDictionary(group => group.Key, group => group.Last());

            foreach (var entity in encounter.Entities.Values)
            {
                if (entity.EntityType != EEntityType.EntChar)
                {
                    continue;
                }

                var characterId = entity.UID != 0
                    ? entity.UID
                    : Utils.UuidToEntityId(entity.UUID);
                if (characterId == 0
                    || !displayedByCharacterId.TryGetValue(characterId, out var displayed))
                {
                    continue;
                }

                // 特化と「未装着」は同じ行から揃えて渡す。Rank1(アビリティ未装着)は
                // 特化0 + スナップショット受信済み で表される。
                entity.ApplyDisplayedIdentityForRecord(
                    displayed.Name,
                    displayed.ProfessionId,
                    displayed.SubProfessionId,
                    displayed.ClassSpec == Models.PlayerClassSpec.Rank1,
                    displayed.CombatPower,
                    displayed.Level,
                    displayed.SeasonLevel,
                    displayed.SeasonStrength,
                    displayed.MaxHp);
            }
        }

        /// <summary>
        /// <b>マップ移動のときだけ呼ぶ。</b>持ち越されたバリアを落とす。
        ///
        /// <para>
        /// 作り直しで属性の辞書ごと運ぶので <c>AttrShieldList</c> も引き継がれるが、
        /// <b>ゲーム側はマップ移動でバリアを消しても、消えたことを伝える属性更新を送ってこない。</b>
        /// 上書きされないまま表示が残るため、ここで空にする。
        /// </para>
        ///
        /// <para>
        /// 属性の持ち越しは外せない(特化とマーカーが失われる)。
        /// <c>Force</c> はダンジョン開始と手動リセットからも来て、そちらではバリアは消えないので、
        /// <b>区切りの理由では分けられない。</b>マップ移動だと確定している呼び出し元からだけ叩く。
        /// </para>
        /// </summary>
        public static void ClearCarriedOverShields()
        {
            var encounter = Current;
            if (encounter is null)
            {
                return;
            }

            foreach (var entity in encounter.Entities.Values)
            {
                if (entity.EntityType != EEntityType.EntChar)
                {
                    continue;
                }

                // 空の一覧を入れる。キーごと消すと「未観測」と区別が付かなくなる。
                entity.SetAttrKV("AttrShieldList", new List<ShieldInfo>());
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

        /// <param name="updateOpenRecords">
        /// いま開いている記録(<see cref="Current"/> と <see cref="CurrentBattleId"/> の battle 行)にも
        /// このシーンを押すか。<b>マップ移動の直後に <c>StartNewMap</c> → <c>EnterDungeon</c> が走る場面では
        /// false を渡すこと。</b> あの2つは「開いている記録＝移動前のマップで戦ったもの」を
        /// そのまま保存/終了するので、ここで移動先のシーンを押すと保存直前に書き換えてしまう。
        /// 新しい <see cref="Current"/> への押印は <c>EnterDungeon</c> の中の呼び出しが行うので抜けは出ない。
        /// </param>
        public static void SetSceneId(uint levelMapId, bool force = false, bool updateOpenRecords = true)
        {

            if (!AllowSceneUpdate && !force)
            {
                return;
            }

            LevelMapId = levelMapId;

            // 名前は翻訳テーブルだけが決める。生テーブルは持たない。
            SceneName = levelMapId > 0 ? CombatDataCatalog.GetSceneName(levelMapId) : "";

            if (!updateOpenRecords)
            {
                return;
            }

            Current.SceneId = LevelMapId;
            Current.SceneName = SceneName;
            DB.UpdateBattleInfo(CurrentBattleId, LevelMapId, SceneName);
        }

        /// <summary>
        /// シーン名を表示中の言語で引き直し、投影へ流し直す。<b>言語を切り替えたときに呼ぶ。</b>
        ///
        /// <para>
        /// <see cref="SceneName"/> は解決済みの文字列を持っているので、
        /// <c>CombatDataCatalog</c> の言語を変えただけでは追従しない。
        /// </para>
        ///
        /// <para>
        /// エンカウンター側(<c>Current.SceneName</c>)とDBは<b>履歴なので触らない</b>。
        /// 履歴は <c>SceneId</c> を持っていて、表示時に引き直す方針。
        /// </para>
        /// </summary>
        public static void RefreshSceneName()
        {
            if (LevelMapId == 0)
            {
                return;
            }

            SceneName = CombatDataCatalog.GetSceneName(LevelMapId);

            // 投影は internal なので App からは触れない。ここまでを1つの操作にする。
            PlayerRosterProjection.UpdateMapName();
            NearbyEntityProjection.UpdateMapName();
        }

        public static async void UpdateTruePerValues(CancellationTokenSource cancellationTokenSource)
        {
            var timer = new PeriodicTimer(TimeSpan.FromSeconds(1));

            while (!cancellationTokenSource.IsCancellationRequested && await timer.WaitForNextTickAsync())
            {
                // 回復だけのエンカウンターでも回す。ダメージだけを見ていると、
                // 毎秒の再計算が一度も走らず HPS が最後の回復イベントの値で凍る
                // (秒間値を時間とともに落とすのはこのループの仕事で、AddData は
                // イベントが届いた瞬間しか計算しない)。
                if (Current != null && (Current.TotalDamage > 0 || Current.TotalHealing > 0))
                {
                    RecalculateEncounterPerValues();
                }
            }
        }

        public static void FreezeCurrentBenchmarkMetrics(DateTime completionTime)
        {
            if (Current == null)
            {
                return;
            }

            UpdateTruePerValuesCTS.Cancel();
            RecalculateEncounterPerValues(completionTime.ToUniversalTime());
        }

        public static void SetCurrentBenchmarkEndTime(DateTime completionTime)
        {
            Current?.SetEndTime(completionTime);
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

        // 被ダメログの並び。記録した順に積むだけで、DB には入れない。
        // DB から読んだエンカウンターは空なので、最初に読まれたときにスナップショットの Sequence から組み直す。
        private readonly object _takenDamageLogGate = new();
        private readonly List<TakenDamageLogRecord> _takenDamageLog = [];
        private bool _takenDamageLogRebuiltFromSnapshots;
        private long _takenDamageLogSequence;

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

        /// <summary>
        /// 経過時間。<b>生存判定は <see cref="EndTime"/> だけで行う。</b>
        ///
        /// <para>
        /// <see cref="Duration"/> は DB に列が無く、書き手も <see cref="SetEndTime"/> だけなので、
        /// <b>DBから読んだエンカウンターでは必ず null になる</b>。これを「まだ終わっていない」と
        /// 読むと、履歴を開いている間ずっと <c>今 − StartTime</c> が返って時計が動き続ける
        /// (古い戦闘ほど大きな値になる)。値は <c>EndTime − StartTime</c> で導出できるので列は要らない。
        /// </para>
        /// </summary>
        public TimeSpan GetDuration(bool startAdjusted = false)
        {
            if (EndTime == DateTime.MinValue)
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
                return Duration ?? EndTime.Subtract(StartTime);
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
                    entity.SetName(CombatDataCatalog.GetMonsterName(Convert.ToInt64(attr_id)));
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
                    entity.SetName(CombatDataCatalog.GetMonsterName(Convert.ToInt64(value)));
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
                if (!IsBenchmarkMetricCaptureStopped())
                {

                    // AttrSkillId は詠唱中のスキルIDを持つ属性で、詠唱が終わると
                    // 「値なし」で飛んでくる。MessageManager がそれを 0 に変換しているので、
                    // 0 は「撃った」ではなく「詠唱が終わった」の合図。
                    // そのまま通すと SkillMetrics[0] が作られ TotalCasts も水増しされる
                    // (ダメージ側は skillId == 0 を弾いているのに、ここだけ弾いていなかった)。
                    var activatedSkillId = (int)value;
                    if (activatedSkillId > 0)
                    {
                        OnSkillActivated(new SkillActivatedEventArgs { CasterUuid = uuid, SkillId = activatedSkillId, ActivationDateTime = DateTime.Now });
                        entity.RegisterSkillActivation(activatedSkillId);
                    }
                }
            }
            else if (key == "AttrState")
            {
                if (!IsBenchmarkMetricCaptureStopped()
                    && (EActorState)value == EActorState.ActorStateDead)
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

        public void AddSceneEvent(Zproto.EventData sceneEvent, ExtraPacketData extraPacketData)
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

                AddSkillAnnouncement(skillId, extraPacketData);
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
                    foreach (var entry in GetOrCreateEntity((long)caster).SkillMetrics)
                    {
                        if (CombatDataCatalog.SourceKeyOwnerId(entry.Key) != (int)skillId)
                        {
                            continue;
                        }

                        entry.Value.Damage.SetSummonData(summoned.UUID, (int)level);
                        entry.Value.Healing.SetSummonData(summoned.UUID, (int)level);
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

        private static bool IsBenchmarkMetricCaptureStopped()
        {
            return AppState.IsBenchmarkMode
                && (AppState.IsBenchmarkCompleting || AppState.IsBenchmarkCompleted);
        }

        public object? GetAttrKV(long uuid, string key)
        {
            return GetOrCreateEntity(uuid).GetAttrKV(key);
        }

        /// <summary>
        /// このエンカウンターに記録すべき戦闘があったか。**ダメージか回復が1でもあれば真。**
        ///
        /// <para>
        /// <b>ダメージだけで判定しない</b> — 回復のみの戦闘が落ちる。
        /// <b>被ダメも数えない</b> — 被ダメメーターを作らない方針なので、表示しない値で真になる。
        /// 見るのは <c>TotalDamage</c> / <c>TotalNpcDamage</c> / <c>TotalHealing</c> /
        /// <c>TotalNpcHealing</c> の4本。
        /// </para>
        /// </summary>
        public bool HasStatsBeenRecorded()
        {
            return TotalDamage > 0 || TotalNpcDamage > 0 || TotalHealing > 0 || TotalNpcHealing > 0;
        }

        public void RegisterSkillActivation(long uuid, int skillId)
        {
            var entity = GetOrCreateEntity(uuid);
            entity.RegisterSkillActivation(skillId);
        }

        /// <summary>
        /// 畳めずバフIDのまま記録した行に印を付ける。
        /// 記録(<c>AddDamage</c> 等)の後に呼ぶこと。行が無ければ何もしない。
        /// </summary>
        public void MarkBuffSourcedSkill(long entityUuid, long skillId)
        {
            if (entityUuid == 0 || skillId == 0)
            {
                return;
            }

            if (Entities.TryGetValue(entityUuid, out var entity)
                && entity.SkillMetrics.TryGetValue(skillId, out var container))
            {
                container.IsBuffSource = true;
            }
        }

        /// <param name="identitySkillId">
        /// 特化判定に使う<b>ゲームが実際に発動したスキルID</b>。判定できないときは 0。
        ///
        /// <para>
        /// <b><paramref name="skillId"/> を渡してはいけない。</b>あちらは表示・集計用に
        /// 弾・バフ・コンボ段を1つへ畳んだIDで、置換後スキル表が想定していない入力まで当たる。
        /// 味方に発生させるスキル(安可など)が畳まれて表に当たると、その人の特化と職業を書き換える。
        /// </para>
        /// </param>
        public void AddDamage(
            long attackerUuid, long targetUuid, long skillId, int identitySkillId, int skillLevel, long damage, long hpLessen, long shieldBreak,
            EDamageProperty damageElement, EDamageType damageType, EDamageMode damageMode,
            bool isCrit, bool isLucky, bool isCauseLucky, bool isMiss, bool isDead, ExtraPacketData extraPacketData)
        {
            // 戦闘は「次のイベント」なので履歴表示を解除する。開いていなければ即戻る。
            EncounterHistoryProvider.NotifyLiveEncounterEvent();

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

            var attacker = GetOrCreateEntity(attackerUuid);

            // ダメージは RegisterSkillActivation を通らない別経路なので、ここでも引く。
            // 詠唱の属性を取りこぼした場合の受け皿。同じ特化なら中で何もしない。
            // 渡すのは畳む前の生のスキルID。理由は identitySkillId の説明を見ること。
            attacker.UpdateSubProfessionFromReplacedSkill(identitySkillId);
            attacker.AddDamage(targetUuid, skillId, skillLevel, damage, hpLessen, shieldBreak, damageElement, damageType, damageMode, isCrit, isLucky, isCauseLucky, isMiss, isDead, extraPacketData);

            if (damage > 0 && damageType != EDamageType.Immune)
            {
                attacker.DamageStats.AddPerSecondValue(ExData.FirstDamageTimeStamp.Value, extraPacketData.ArrivalTime, damage);
            }
        }

        public void AddHealing(
            long attackerUuid, long targetUuid, long skillId, int skillLevel, long damage, long hpLessen, long shieldBreak,
            EDamageProperty damageElement, EDamageType damageType, EDamageMode damageMode,
            bool isCrit, bool isLucky, bool isCauseLucky, bool isMiss, bool isDead, ExtraPacketData extraPacketData)
        {
            // ダメージが1件も無いと回復を丸ごと捨てる門がここにあったが、削除した。
            // FirstDamageTimeStamp は AddDamage でしか立たないため、回復だけを続けても
            // 永久に null のままで門を抜けられず、HPSメーターが起動しなかった。
            // メーターがDPS/HPSに分かれた今、HPS側だけを見ていると何も出ない不具合になる。

            // 戦闘は「次のイベント」なので履歴表示を解除する。開いていなければ即戻る。
            EncounterHistoryProvider.NotifyLiveEncounterEvent();

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

            entity.AddHealing(targetUuid, skillId, skillLevel, damage, overhealing, effectiveHealing, hpLessen, shieldBreak, damageElement, damageType, damageMode, isCrit, isLucky, isCauseLucky, isMiss, isDead, extraPacketData);

            if (damage > 0)
            {
                entity.HealingStats.AddPerSecondValue(ExData.FirstDamageTimeStamp.Value, extraPacketData.ArrivalTime, damage);
            }
        }

        /// <param name="ownerId">
        /// <c>SyncDamageInfo.OwnerId</c> の生の値。<paramref name="skillId"/> はメーター用に畳んだ鍵なので、
        /// 被ダメログの技名はこちらと <paramref name="damageSource"/> で引く。
        /// </param>
        /// <param name="buffSourceSkillId">
        /// バフ由来のとき、そのバフを付けた技(<see cref="Services.BuffInstanceIndex.TryResolveSourceSkill"/>)。決まらなければ 0。
        /// </param>
        /// <param name="summonSourceSkillId">
        /// ダメージを出した実体(仮想体など)を出した技(<see cref="Services.SummonSourceIndex"/>)。決まらなければ 0。
        /// </param>
        public void AddTakenDamage(
            long attackerUuid, long targetUuid, long skillId, int ownerId, EDamageSource damageSource, int buffSourceSkillId, int summonSourceSkillId, int skillLevel, long damage, long hpLessen, long shieldBreak,
            EDamageProperty damageElement, EDamageType damageType, EDamageMode damageMode,
            bool isCrit, bool isLucky, bool isCauseLucky, bool isMiss, bool isDead, ExtraPacketData extraPacketData)
        {
            LastUpdate = extraPacketData.ArrivalTime;

            var targetEntity = GetOrCreateEntity(targetUuid);

            // 記録するのは被ダメログに載る「プレイヤーがプレイヤー以外から受けた」ぶんだけ。
            // モンスターの被ダメとプレイヤー同士のぶんは読み手が無い。戦闘中かどうかの判定だけは全員に効かせる。
            if ((EEntityType)Utils.UuidToEntityType(targetUuid) != EEntityType.EntChar
                || (EEntityType)Utils.UuidToEntityType(attackerUuid) == EEntityType.EntChar)
            {
                targetEntity.RecalculateInactiveTime(extraPacketData.ArrivalTime);
                return;
            }

            var snapshot = targetEntity.AddTakenDamage(attackerUuid, skillId, skillLevel, damage, hpLessen, shieldBreak, damageElement, damageType, damageMode, isCrit, isLucky, isCauseLucky, isMiss, isDead, extraPacketData,
                new SkillSnapshotStamp(
                    NextTakenDamageLogSequence(),
                    targetEntity.GetAttrKV("AttrHp") as long?,
                    targetEntity.GetAttrKV("AttrMaxHp") as long?,
                    ownerId,
                    damageSource,
                    buffSourceSkillId,
                    summonSourceSkillId));

            AppendTakenDamageLog(targetUuid, snapshot);
        }

        /// <summary>
        /// 技の開始を被ダメログの詠唱として残す。<b>詠唱バーを持つ技</b>と<b>戦闘画面の警告の技</b>だけで、
        /// プレイヤーとプレイヤーの召喚物は対象外(被ダメと同じく、召喚物は大元の召喚者として扱う)。
        /// </summary>
        /// <param name="skillLevel">技の開始と同じ差分で届く <c>AttrSkillLevel</c>。届いていなければ 0。</param>
        public void AddSkillCast(long entityUuid, int skillId, int skillLevel, ExtraPacketData extraPacketData)
        {
            if (skillId <= 0)
            {
                return;
            }

            if (!CombatDataCatalog.HasSingOrGuideTime(skillId))
            {
                // 警告の技かは技レベルで決まるので、レベルが無ければ判定できない。
                if (skillLevel <= 0)
                {
                    Serilog.Log.Warning("AttrSkillLevel が届いていない技の開始 uuid={EntityUuid} skillId={SkillId}。警告の技かを判定できないので記録しない",
                        entityUuid, skillId);
                    return;
                }

                if (!CombatDataCatalog.IsWarningSkill(skillId, skillLevel))
                {
                    return;
                }
            }

            var casterUuid = GetOrCreateEntity(entityUuid).GetAttrKV("AttrTopSummonerId") is long topSummonerUuid
                && topSummonerUuid != 0
                    ? topSummonerUuid
                    : entityUuid;
            if ((EEntityType)Utils.UuidToEntityType(casterUuid) == EEntityType.EntChar)
            {
                return;
            }

            var cast = new SkillCastRecord
            {
                SkillId = skillId,
                Timestamp = extraPacketData.ArrivalTime,
                Sequence = NextTakenDamageLogSequence(),
            };
            GetOrCreateEntity(casterUuid).AddSkillCast(cast);

            lock (_takenDamageLogGate)
            {
                _takenDamageLog.Add(TakenDamageLogRecord.ForCast(casterUuid, cast));
            }
        }

        /// <summary>
        /// ボス大技の予告を被ダメログの予告行として残す。通知の番号(<c>DbmTable.Id</c>)から技を引き、
        /// 周囲にいるモンスターからその技を持つものを構えた側とする。
        /// </summary>
        public void AddSkillAnnouncement(int dbmId, ExtraPacketData extraPacketData)
        {
            if (dbmId <= 0)
            {
                return;
            }

            var ownerMonsterId = 0;
            if (CombatDataCatalog.TryResolveDbmSkillId(dbmId, out var skillId))
            {
                Services.NearbyMonsterIndex.Instance.TryFindMonsterId(
                    monsterId => CombatDataCatalog.MonsterHasSkill(monsterId, skillId),
                    out ownerMonsterId);
            }

            var announcement = new SkillAnnouncementRecord
            {
                SkillId = skillId,
                OwnerMonsterId = ownerMonsterId,
                Timestamp = extraPacketData.ArrivalTime,
                Sequence = NextTakenDamageLogSequence(),
            };

            lock (_takenDamageLogGate)
            {
                ExData.SkillAnnouncements.Add(announcement);
                _takenDamageLog.Add(TakenDamageLogRecord.ForAnnouncement(announcement));
            }
        }

        private long NextTakenDamageLogSequence()
        {
            return Interlocked.Increment(ref _takenDamageLogSequence);
        }

        private void AppendTakenDamageLog(long targetUuid, SkillSnapshot? snapshot)
        {
            // TakenStats は必ずスナップショットを作る。null はその前提が崩れたということ。
            if (snapshot is null)
            {
                throw new InvalidOperationException(
                    $"Taken damage log snapshot was not recorded (target={targetUuid}).");
            }

            lock (_takenDamageLogGate)
            {
                _takenDamageLog.Add(TakenDamageLogRecord.ForHit(targetUuid, snapshot));
            }
        }

        /// <summary>
        /// 被ダメログの <paramref name="startIndex"/> 番目以降を返す。ライブ中は差分の追記に使う。
        ///
        /// <para>
        /// <b>DB から読んだエンカウンターは並びを持たない</b>ので、実行中のエンカウンター以外で空なら
        /// 1回だけ、予告とプレイヤーの <c>TakenStats</c> と全エンティティの詠唱を通し番号順に並べ直す。
        /// </para>
        /// </summary>
        public TakenDamageLogRecord[] GetTakenDamageLogRecords(int startIndex)
        {
            lock (_takenDamageLogGate)
            {
                if (_takenDamageLog.Count == 0
                    && !_takenDamageLogRebuiltFromSnapshots
                    && !ReferenceEquals(this, EncounterManager.Current))
                {
                    _takenDamageLogRebuiltFromSnapshots = true;
                    RebuildTakenDamageLogFromSnapshots();
                }

                if (startIndex >= _takenDamageLog.Count)
                {
                    return [];
                }

                return _takenDamageLog.GetRange(startIndex, _takenDamageLog.Count - startIndex).ToArray();
            }
        }

        private void RebuildTakenDamageLogFromSnapshots()
        {
            var records = new List<TakenDamageLogRecord>();
            foreach (var announcement in ExData.SkillAnnouncements)
            {
                records.Add(TakenDamageLogRecord.ForAnnouncement(announcement));
            }

            foreach (var (uuid, entity) in Entities)
            {
                foreach (var cast in entity.GetSkillCastsCopy())
                {
                    records.Add(TakenDamageLogRecord.ForCast(uuid, cast));
                }

                if ((EEntityType)Utils.UuidToEntityType(uuid) != EEntityType.EntChar)
                {
                    continue;
                }

                foreach (var snapshot in entity.TakenStats.GetSkillSnapshotsCopy())
                {
                    // 0 は通し番号を持たない古い記録で、並べる順が決まらない。
                    if (snapshot.Sequence == 0)
                    {
                        continue;
                    }

                    records.Add(TakenDamageLogRecord.ForHit(uuid, snapshot));
                }
            }

            records.Sort((left, right) => left.Sequence.CompareTo(right.Sequence));
            _takenDamageLog.AddRange(records);
        }

        public void AddShieldGained(long entityUuid, long shieldBuffUuid, long value, long initialValue, long maxValue = 0)
        {

        }

        /// <param name="carriesBuffInfo">
        /// このイベントが <c>BuffInfo</c>(または <c>BuffChange</c>)を伴っていたか。
        /// <b><c>false</c> なら <c>baseId</c> 以下の値は「サーバが送ってこなかった」ことを表すゼロ</b>で、
        /// バフの実際の状態ではない。<c>LogicEffect</c> を持たない回は珍しくないので、
        /// ここで持続や層を書き込むと時限バフが持続0＝無期限に化けて消えなくなる。
        /// </param>
        public void NotifyBuffEvent(long entityUuid, EBuffEventType buffEventType, int buffUuid, int baseId, int level, long fireUuid, int layer, int duration, int sourceConfigId, DateTime? creationTime, ExtraPacketData extraPacketData, int fightSourceType = 0, bool carriesBuffInfo = true)
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
            GetOrCreateEntity(entityUuid).NotifyBuffEvent(buffEventType, buffUuid, baseId, level, fireUuid, entityCasterName, layer, duration, sourceConfigId, DateTime.Now.Subtract(EncounterManager.Current.StartTime), creationTime, extraPacketData, fightSourceType, carriesBuffInfo);

            // 強化する9特化は、特化アビリティ本体が付与するバフ(と、その実行時変種)で判定する。
            // 紐付け先は保持者ではなく術者(FireUuid)。味方に配られるバフは受け手が保持するので、
            // 保持者に付けると回復を受けた人まで同じ特化になる。
            //
            // 除去は見ない。「あることしか示さない」判定なので、消えても未装着の根拠にならない。
            if (buffEventType == EBuffEventType.BuffEventRemove)
            {
                ApplySpecMarkerRemoval(entityUuid, buffUuid);
            }
            else if (baseId > 0)
            {
                ApplySpecFromTalentBuff(baseId, buffUuid, fireUuid, fightSourceType, entityUuid, sourceConfigId);
            }
        }

        /// <summary>
        /// タレント由来のバフから、<b>そのバフを張った本人</b>の特化を確定する。全18特化ぶん。
        ///
        /// <para>
        /// <b>帰属先は保持者ではなく術者(<c>BuffInfo.FireUuid</c>)。</b>
        /// プロトコル上バフの術者を名乗るフィールドはこれ1つ。
        /// 味方に配られるバフでも、術者として出した本人が入る。
        /// </para>
        ///
        /// <para>
        /// <b><c>FightSourceType</c> を採用条件にしない。</b><c>srcType==6</c> を必須にすると
        /// 強化側のツリー副産物が全部落ちる。観測のためログには残す。
        /// </para>
        ///
        /// <para>あることしか示さない。除去は見ない。</para>
        /// </summary>
        public void ApplySpecFromTalentBuff(int observedBuffId, int buffUuid, long fireUuid, int fightSourceType, long holderUuid = 0, int sourceConfigId = 0)
        {
            if (!DataTypes.SpecDetectionTables.TryResolveSpecTalentBuff(
                    observedBuffId, out var grantedBuffId, out var spec, out var distanceFromRoot))
            {
                return;
            }

            // 術者がプレイヤーでないものは拾わない。モンスターやシーンオブジェクトが張った
            // バフのIDがたまたま一致したときに、そちらを特化持ちとして作らないため。
            if (fireUuid == 0 || (EEntityType)Utils.UuidToEntityType(fireUuid) != EEntityType.EntChar)
            {
                return;
            }

            // 変種バフ(+1〜+9)は、ワイヤが名乗る親で特化を引き直す。10刻み丸めは推定でしかなく、
            // 別ツリーの下流効果が隣の特化のバフに化けることがある。
            //
            // 書き換えるのは特化だけ。grantedBuffId / distanceFromRoot / isMarker は観測ID由来の
            // ままにする — 親がマーカー本体のことがあり(2203291 の親は鷹弓の根 2203290)、
            // 親の距離0を採ると派生バフでマーカーを latch してしまう。
            if (DataTypes.SpecDetectionTables.TryResolveSpecByParentBuff(
                    observedBuffId, fightSourceType, sourceConfigId, out var parentSpec, out _))
            {
                spec = parentSpec;
            }

            // 除去を突き合わせられるのはマーカー本体(ツリーの根)だけ。距離1以上の派生バフは
            // procで付いたり消えたりするので、それが消えても未装着の根拠にならない。
            var isMarker = distanceFromRoot == 0;
            var target = GetOrCreateEntity(fireUuid);

            // 常設の食い違い検知。枝タレント(距離1以上)が、控えてあるマーカーと違う特化を
            // 書こうとしたら記録する。鳴ったら判定表か10刻み丸めの誤り。
            //
            // マーカー自身(距離0)は除く。クラス変更でも、ビルドプリセットの一括配信でも
            // 別のマーカーが正当に来るため、食い違いの証拠にならない。
            if (!isMarker
                && target.SpecMarkerBuffId != 0
                && DataTypes.SpecDetectionTables.TryResolveSpecTalentBuff(
                    target.SpecMarkerBuffId, out _, out var markerSpec, out _)
                && markerSpec != spec)
            {
                Diagnostics.SpecConflictProbe.CaptureTalentConflict(
                    observedBuffId,
                    grantedBuffId,
                    spec.ToString(),
                    distanceFromRoot,
                    fightSourceType,
                    sourceConfigId,
                    target.SpecMarkerBuffId,
                    markerSpec.ToString(),
                    fireUuid,
                    holderUuid);
            }

            target.UpdateSubProfessionFromTalentBuff(
                (int)spec,
                isMarker ? grantedBuffId : 0,
                isMarker ? buffUuid : 0);
        }

        /// <summary>
        /// 特化マーカーバフの除去を受けて、特化を未装着(クラスR1)へ戻す。
        ///
        /// <para>
        /// 除去イベントは <c>BaseId</c> を運ばない(0 で届く)ので、付与時に控えた
        /// バフ実体UUIDとの一致でしか判定できない。控えてあるのはマーカー本体だけなので、
        /// 派生バフが消えただけで特化を落とすことはない。
        /// </para>
        ///
        /// <para>
        /// <b>保持者(<paramref name="entityUuid"/>)で突き合わせる。</b> 除去イベントは術者を
        /// 運ばないため、マーカーが「自分のアビリティが自分に付けるバフ」で保持者＝術者である
        /// ことを前提にしている。<b>この前提は未検証。</b>
        /// </para>
        /// </summary>
        private void ApplySpecMarkerRemoval(long entityUuid, int buffUuid)
        {
            if (entityUuid == 0 || buffUuid == 0)
            {
                return;
            }

            // ここでエンティティを作らない。見たことのない相手の除去は突き合わせようがない。
            if (!Entities.TryGetValue(entityUuid, out var entity)
                || !entity.ClearSubProfessionFromMarkerRemoval(buffUuid))
            {
                return;
            }

            // 保持している補完値も同時に落とす。落とさないと、次の解決でキャッシュが
            // 古い特化を返して元に戻ってしまう(自分はキャッシュを読まないので影響しない)。
            var characterId = Utils.UuidToEntityId(entityUuid);
            Services.PartyMemberCache.Instance.SetSpecAbilityUnequipped(characterId);
            Services.MeterPlayerSpecCache.Instance.SetSpecAbilityUnequipped(characterId);
            PlayerRosterProjection.AddOrUpdateNearbyPlayer(entityUuid);
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
        /// <summary>被ダメログの予告行(<see cref="Encounter.AddSkillAnnouncement"/>)。実体に属さないのでここに持つ。</summary>
        [ProtoMember(8)]
        public List<SkillAnnouncementRecord> SkillAnnouncements { get; set; } = [];

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

    /// <summary>
    /// エンカウンターをまたいでも変わらない素性。
    ///
    /// <para>
    /// <see cref="Entity"/> は戦闘統計の入れ物でもあり、フェーズ区切り(NewObjective)などで
    /// まるごと作り直される。そのとき統計は捨ててよいが素性は残す必要がある。
    /// 素性をこの型に集約し、引き継ぎは <see cref="Entity.CopyIdentityFrom"/> の1呼び出しで済ませる。
    /// </para>
    ///
    /// <para>
    /// <b>素性を足すときは必ずここに足すこと。</b> record のコピーで丸ごと運ばれるので、
    /// 引き継ぎ側に書き足す必要はない。<see cref="Entity"/> に直接プロパティを足すと運ばれず、
    /// 作り直しのたびに失われる。
    /// </para>
    ///
    /// <para>Hp / MaxHp は戦闘中に変わる値なので素性に含めない。従来どおり個別に引き継ぐ。</para>
    /// </summary>
    public sealed record EntityIdentity
    {
        public long UUID { get; set; }
        public long UID { get; set; }
        public EEntityType EntityType { get; set; }
        public EMonsterType MonsterType { get; set; } = EMonsterType.Unknown;
        public EEntityType SummonerEntityType { get; set; } = EEntityType.EntErrType;
        public string Name { get; set; } = null!;
        public int AbilityScore { get; set; }
        public int ProfessionId { get; set; }
        public int SubProfessionId { get; set; }

        /// <summary>いま乗っている特化マーカーバフのID。0 なら未観測。</summary>
        public int SpecMarkerBuffId { get; set; }

        /// <summary>上のマーカーバフの実体UUID。除去イベントは BaseId を運ばないため、これで突き合わせる。</summary>
        public int SpecMarkerBuffUuid { get; set; }

        /// <summary>
        /// 全バフスナップショットを受信済みか。「マーカーが無い＝未装着」と言えるのは、
        /// 全バフを一度に受け取った上でそこに無かったときだけ。
        /// </summary>
        public bool HasBuffSnapshot { get; set; }

        public int Level { get; set; }
        public long SeasonLevel { get; set; }
        public long SeasonStrength { get; set; }
    }

    public class Entity : System.ICloneable
    {
        private EntityIdentity _identity = new();

        /// <summary>
        /// 素性を丸ごと引き継ぐ。エンカウンター作り直しで統計だけ捨てたいときに使う。
        /// record のコピーなので <see cref="EntityIdentity"/> に足したものは自動で運ばれる。
        /// </summary>
        public void CopyIdentityFrom(Entity other)
        {
            ArgumentNullException.ThrowIfNull(other);
            _identity = other._identity with { };
        }

        public long UUID { get => _identity.UUID; set => _identity.UUID = value; }
        public long UID { get => _identity.UID; set => _identity.UID = value; }
        public EEntityType EntityType { get => _identity.EntityType; private set => _identity.EntityType = value; }
        public string Name { get => _identity.Name; private set => _identity.Name = value; }
        public int AbilityScore { get => _identity.AbilityScore; private set => _identity.AbilityScore = value; }
        public int ProfessionId { get => _identity.ProfessionId; private set => _identity.ProfessionId = value; }
        public int SubProfessionId { get => _identity.SubProfessionId; private set => _identity.SubProfessionId = value; }

        public int SpecMarkerBuffId { get => _identity.SpecMarkerBuffId; private set => _identity.SpecMarkerBuffId = value; }
        public int SpecMarkerBuffUuid { get => _identity.SpecMarkerBuffUuid; private set => _identity.SpecMarkerBuffUuid = value; }
        public bool HasBuffSnapshot { get => _identity.HasBuffSnapshot; private set => _identity.HasBuffSnapshot = value; }

        /// <summary>
        /// 特化が「アビリティ未装着」(ゲーム内呼称 クラスR1)と確定できる状態か。
        ///
        /// <para>
        /// 全バフスナップショットを受け取った上で、そこにタレント由来のバフが1つも無かったときだけ true。
        /// スナップショットは「いまこのエンティティが持っている全バフ」なので、
        /// そこに無いことは「持っていない」を意味する。
        /// </para>
        ///
        /// <para>
        /// <b>ID判定式が特化を確定させたら Rank1 は成立しない。</b>
        /// スナップショットは AOI出現とマップロードでしか来ないため、
        /// ロード済みマップで途中起動すると「マーカーが無い」状態から始まる。
        /// そこへ置換後スキルやタレントバフが到着したら、そちらを優先する。
        /// </para>
        /// </summary>
        public bool IsSpecAbilityUnequipped =>
            ProfessionId > 0 && HasBuffSnapshot && SubProfessionId == 0;
        public int Level { get => _identity.Level; set => _identity.Level = value; }

        public long SeasonLevel { get => _identity.SeasonLevel; set => _identity.SeasonLevel = value; }
        public long SeasonStrength { get => _identity.SeasonStrength; set => _identity.SeasonStrength = value; }

        public CombatStats DamageStats { get; set; } = new();
        public CombatStats HealingStats { get; set; } = new();

        private CombatStats _takenStats = null!;

        /// <summary>
        /// 被ダメログの材料。<b>スナップショットを溜めるのはこの統計だけ</b>で、
        /// 入るのは「プレイヤーがプレイヤー以外から受けた」ぶんだけ(<see cref="Encounter.AddTakenDamage"/>)。
        /// 記録の印はセッターで押すので、コンストラクタもセッターを通して入れる。
        /// </summary>
        public CombatStats TakenStats
        {
            get => _takenStats;
            set
            {
                value.EnableSkillSnapshotRecording();
                _takenStats = value;
            }
        }

        /// <summary>
        /// このエンティティが始めた詠唱。被ダメログの詠唱行の材料で、
        /// <see cref="Encounter.AddSkillCast"/> がプレイヤー以外の詠唱バーを持つ技だけを積む。
        /// </summary>
        public List<SkillCastRecord> SkillCasts { get; private set; } = new();
        private readonly object _skillCastsGate = new();

        public bool ShouldSerializeSkillCasts() => SkillCasts.Count > 0;

        public void AddSkillCast(SkillCastRecord cast)
        {
            lock (_skillCastsGate)
            {
                SkillCasts.Add(cast);
            }
        }

        public SkillCastRecord[] GetSkillCastsCopy()
        {
            lock (_skillCastsGate)
            {
                return SkillCasts.ToArray();
            }
        }

        public ConcurrentDictionary<long, CombatStats> SkillStats { get; set; } = new();
        public ConcurrentDictionary<long, MetricsContainer> SkillMetrics { get; set; } = new();

        public ulong TotalDamage { get; set; } = 0;
        public ulong TotalShieldBreak { get; set; } = 0;
        public ulong TotalHealing { get; set; } = 0;
        public ulong TotalOverhealing { get; set; } = 0;
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

        public EMonsterType MonsterType { get => _identity.MonsterType; set => _identity.MonsterType = value; }

        public long Hp { get; private set; } = 0;
        public long MaxHp { get; private set; } = 0;
        public ConcurrentQueue<long> RecentHpHistory { get; private set; } = new();

        public List<ThreatInfo> ThreatInfoList { get; private set; } = new();
        public ConcurrentQueue<List<ThreatInfo>> RecentThreatInfoListHistory { get; private set; } = new();

        private Dictionary<string, object> _attributes = new();

        public Dictionary<string, object> Attributes
        {
            get => _attributes;
            set
            {
                _attributes = value ?? new Dictionary<string, object>();
            }
        }

        [JsonIgnore]
        public Dictionary<int, TempAttributesContainer> TempAttributes { get; set; } = new();

        public EEntityType SummonerEntityType { get => _identity.SummonerEntityType; set => _identity.SummonerEntityType = value; }

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
                    Healing = (CombatStats)container.Value.Healing.Clone()
                };
                ((Entity)cloned).SkillMetrics.AddOrUpdate(container.Key, metrics, (key, value) => metrics);
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
            TakenStats = new CombatStats();

            if (encounter != null)
            {

                if (encounter.PreviousBossCache.Count > 0 && encounter.PreviousBossCache.TryGetValue(uuid, out var foundEnt))
                {
                    SetHpValuesNoUpdate(foundEnt.Hp, foundEnt.MaxHp);
                    Attributes = foundEnt.Attrs.ToDictionary();

                    encounter.PreviousBossCache.Remove(uuid);
                }

                // AttrId は出現時にしか届かず、区切りをまたいで運ばれない敵もいる。周囲にいる敵なら索引から戻す。
                // 索引はいまの周囲を表すので、実行中のエンカウンターの実体にだけ入れる。
                if (ReferenceEquals(encounter, EncounterManager.Current)
                    && (EEntityType)Utils.UuidToEntityType(uuid) == EEntityType.EntMonster
                    && !Attributes.ContainsKey("AttrId")
                    && Services.NearbyMonsterIndex.Instance.TryGetMonsterId(uuid, out var nearbyMonsterId))
                {
                    Attributes["AttrId"] = nearbyMonsterId;
                }
            }

            SetEntityType((EEntityType)Utils.UuidToEntityType(uuid));

            // キャッシュから初期値を流し込まない。ここで注入すると、その値が Entity のライブ値と
            // 見分けられなくなり、PlayerDataSourceResolver の優先順位(ライブ > パーティ情報 > 補完)を
            // 追い越して新しい情報を握り潰す。補完が要るものは PartyMemberCache から明示的に引く。

        }

        public void UpdateUID(long uid)
        {
            UID = uid;
        }

        public void SetName(string name)
        {
            Name = name;
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
                    SetName(CombatDataCatalog.GetMonsterName(Convert.ToInt64(attr_id)));
                }
            }
        }

        public void SetAbilityScore(int abilityScore)
        {
            AbilityScore = abilityScore;
        }

        public void SetProfessionId(int id)
        {
            ProfessionId = id;

            var subProfessionBaseId = Professions.GetProfessionIdFromSubProfessionId(SubProfessionId);
            var clearSubProfession = SubProfessionId != 0 && subProfessionBaseId != id;
            if (clearSubProfession)
            {
                SetSubProfessionUnknown();
                Services.PartyMemberCache.Instance.ClearSubProfession(Utils.UuidToEntityId(UUID));
            }
        }

        public void SetSubProfessionId(int id)
        {
            if (id <= 0)
            {
                SetSubProfessionUnknown();
                return;
            }

            // 変身クラス(8/14/15)の間は、いかなる特化変更も受け付けない。
            // これらは特化(クラスR2)を持たず、表示は職業IDから固定で決まる。
            //
            // ここで弾かないと職業ごと戻される。この直後の分岐が
            // 「特化が属する職」で ProfessionId を書き換えるため、変身中に
            // タレントバフや置換後スキルを1つ拾っただけで元のクラスに戻ってしまう。
            if (Models.PlayerClassSpecResolver.TryResolveTransformation(ProfessionId, out _))
            {
                return;
            }

            SubProfessionId = id;

            int profId = Professions.GetProfessionIdFromSubProfessionId(id);
            if (ProfessionId != profId)
            {
                ProfessionId = profId;
            }

            // AOI外に出ると観測できなくなるので、自分以外のパーティメンバーの分だけ控える。
            // 自分かどうか・パーティ内かどうかの判定はキャッシュ側が持つ。
            Services.PartyMemberCache.Instance.SetSubProfession(Utils.UuidToEntityId(UUID), id);
        }

        /// <summary>
        /// <b>保存の直前にだけ呼ぶ。いま画面に出している素性をそのまま記録へ焼き付ける。</b>
        ///
        /// <para>
        /// エンティティが持つ生の値は、AOI退出で <see cref="SetSubProfessionUnknown"/> に消される
        /// (消すこと自体は必要 — 次に現れたときの全バフスナップショットを権威にするため)。
        /// 表示はキャッシュとPT補完が補っているが、そちらは blob に載らない。
        /// だから保存するときは<b>合成後の値</b>を書く。
        /// </para>
        ///
        /// <para>
        /// <b><see cref="SetSubProfessionId"/> や <see cref="SetProfessionId"/> は使わない。</b>
        /// あれらは職業を導き直したり <c>PartyMemberCache</c> へ書いたりする副作用を持つ。
        /// ここが欲しいのは値の書き込みだけ。
        /// </para>
        ///
        /// <para>
        /// 特化と「未装着」は同じ1行から揃えて渡すこと。<see cref="IsSpecAbilityUnequipped"/> は
        /// <c>ProfessionId &gt; 0 &amp;&amp; HasBuffSnapshot &amp;&amp; SubProfessionId == 0</c> で導出されるので、
        /// 未装着を記録するには <paramref name="hasBuffSnapshot"/> を立てて特化を0にする。
        /// </para>
        /// </summary>
        public void ApplyDisplayedIdentityForRecord(
            string name,
            int professionId,
            int subProfessionId,
            bool hasBuffSnapshot,
            int abilityScore,
            int level,
            long seasonLevel,
            long seasonStrength,
            long maxHp)
        {
            if (!string.IsNullOrEmpty(name))
            {
                Name = name;
            }

            if (professionId > 0)
            {
                ProfessionId = professionId;
            }

            SubProfessionId = subProfessionId;
            HasBuffSnapshot = hasBuffSnapshot;

            if (abilityScore > 0)
            {
                AbilityScore = abilityScore;
            }

            if (level > 0)
            {
                Level = level;
            }

            if (seasonLevel > 0)
            {
                SeasonLevel = seasonLevel;
            }

            if (seasonStrength > 0)
            {
                SeasonStrength = seasonStrength;
            }

            if (maxHp > 0)
            {
                MaxHp = maxHp;
            }
        }

        public void SetSubProfessionUnknown()
        {
            SubProfessionId = 0;

            // 特化を「観測できていない」に戻すときは、その根拠だったマーカーと
            // スナップショット受信済みフラグも一緒に落とす。
            // スナップショットだけ残すと「受信済みなのにマーカーが無い」= 未装着確定、という
            // 実際には確かめていない結論が立ってしまう。
            SpecMarkerBuffId = 0;
            SpecMarkerBuffUuid = 0;
            HasBuffSnapshot = false;
        }

        public void SetLevel(int level)
        {
            Level = level;
        }

        public void SetSeasonLevel(int level)
        {
            SeasonLevel = level;
        }

        public void SetSeasonStrength(int strength)
        {
            SeasonStrength = strength;
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
            // 特化判定はここに置く。詠唱の AttrSkillId 経由でも AddDamage 経由でも必ず通るため、
            // 空振り(当たらなかった一撃)でも特定できる。
            UpdateSubProfessionFromReplacedSkill(skillId);

            if (!SkillMetrics.TryGetValue(skillId, out var container))
            {
                container = new();

                // 名前は翻訳テーブルだけが決める。生テーブルの Name は言語が混ざっていて追従しない。
                var registeredName = CombatDataCatalog.GetSkillName(skillId);
                container.Damage.SetName(registeredName);
                container.Healing.SetName(registeredName);

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

        public void RegisterSkillData(ESkillType skillType, long otherUuid, long skillId, int skillLevel, long value, bool isCrit, bool isLucky, long hpLessenValue, long shieldBreak, bool isCauseLucky, EDamageProperty damageElement, EDamageType damageType, EDamageMode damageMode, bool isDead, ExtraPacketData extraPacketData)
        {
            if(!SkillMetrics.TryGetValue(skillId, out var container))
            {
                var combatStats = new CombatStats();

                combatStats.SetSkillType(skillType);

                combatStats.SetName(CombatDataCatalog.GetSourceName(skillId));

                var ownerId = CombatDataCatalog.SourceKeyOwnerId(skillId);
                if (HelperMethods.DataTables.Skills.Data.TryGetValue(ownerId.ToString(), out var skill))
                {
                    var attrSkillLevelIdList = GetAttrKV("AttrSkillLevelIdList");
                    if (attrSkillLevelIdList != null)
                    {
                        if (attrSkillLevelIdList is List<DataTypes.Skills.SkillLevelInfo>)
                        {
                            var list = (List<DataTypes.Skills.SkillLevelInfo>)attrSkillLevelIdList;

                            int skillLevelGroup = ownerId;
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

                // **容器へ入れてから AddData を呼ぶ。** 順を戻すと、記録を止める印が押される前に
                // 1件目のスナップショットが溜まり、(エンティティ×鍵×種別)ごとに1件ずつ残る。
                container = new();
                if (skillType == ESkillType.Damage)
                {
                    container.Damage = combatStats;
                }
                else if (skillType == ESkillType.Healing)
                {
                    container.Healing = combatStats;
                }
                else
                {
                    Serilog.Log.Warning($"RegisterSkillData SkillId {skillId} was an Unknown skill type and was not registered.");
                }

                combatStats.AddData(otherUuid, skillId, skillLevel, value, isCrit, isLucky, hpLessenValue, shieldBreak, isCauseLucky, damageElement, damageType, damageMode, isDead, extraPacketData, GetInactiveTime(), FirstCombatActionTime, default);

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
                else
                {
                    combatStats = new();
                    Serilog.Log.Warning($"RegisterSkillData SkillId {skillId} was an Unknown skill type and was not updated.");
                }

                if (string.IsNullOrEmpty(combatStats.Name))
                {
                    combatStats.SetName(CombatDataCatalog.GetSourceName(skillId));
                }

                var ownerId = CombatDataCatalog.SourceKeyOwnerId(skillId);
                if (HelperMethods.DataTables.Skills.Data.TryGetValue(ownerId.ToString(), out var skill))
                {
                    var attrSkillLevelIdList = GetAttrKV("AttrSkillLevelIdList");
                    if (attrSkillLevelIdList != null)
                    {
                        if (attrSkillLevelIdList is List<DataTypes.Skills.SkillLevelInfo>)
                        {
                            var list = (List<DataTypes.Skills.SkillLevelInfo>)attrSkillLevelIdList;

                            int skillLevelGroup = ownerId;
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

                combatStats.AddData(otherUuid, skillId, skillLevel, value, isCrit, isLucky, hpLessenValue, shieldBreak, isCauseLucky, damageElement, damageType, damageMode, isDead, extraPacketData, GetInactiveTime(), FirstCombatActionTime, default);
            }
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

        public void AddDamage(long targetUuid, long skillId, int skillLevel, long damage, long hpLessen, long shieldBreak,
            EDamageProperty damageElement, EDamageType damageType, EDamageMode damageMode,
            bool isCrit, bool isLucky, bool isCauseLucky, bool isMiss, bool isDead, ExtraPacketData extraPacketData)
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

            DamageStats.AddData(targetUuid, skillId, skillLevel, damage, isCrit, isLucky, hpLessen, shieldBreak, isCauseLucky, damageElement, damageType, damageMode, isDead, extraPacketData, GetInactiveTime(), FirstCombatActionTime, default);

            RegisterSkillData(ESkillType.Damage, targetUuid, skillId, skillLevel, damage, isCrit, isLucky, hpLessen, shieldBreak, isCauseLucky, damageElement, damageType, damageMode, isDead, extraPacketData);
        }

        public void AddHealing(
            long targetUuid, long skillId, int skillLevel, long damage, long overhealing, long effectiveHealing, long hpLessen, long shieldBreak,
            EDamageProperty damageElement, EDamageType damageType, EDamageMode damageMode,
            bool isCrit, bool isLucky, bool isCauseLucky, bool isMiss, bool isDead, ExtraPacketData extraPacketData)
        {
            RecalculateInactiveTime(extraPacketData.ArrivalTime);

            TotalHealing += (ulong)damage;
            TotalOverhealing += (ulong)overhealing;

            HealingStats.AddData(targetUuid, skillId, skillLevel, damage, isCrit, isLucky, hpLessen, shieldBreak, isCauseLucky, damageElement, damageType, damageMode, isDead, extraPacketData, GetInactiveTime(), FirstCombatActionTime, default);

            RegisterSkillData(ESkillType.Healing, targetUuid, skillId, skillLevel, damage, isCrit, isLucky, overhealing, shieldBreak, isCauseLucky, damageElement, damageType, damageMode, isDead, extraPacketData);
        }

        /// <summary>
        /// 置換後スキルIDから特化を確定する。置換する9特化＋光砕。
        ///
        /// <para>
        /// 根ノードの <c>TalentEffect [6, 置換元, 置換先]</c> による置換なので、
        /// 置換後のIDが飛んだ時点で装着が確定する。素のIDは未装着と区別がつかないため見ない。
        /// 光砕だけは置換を持たないが、装着中のみ特殊攻撃に随伴する 2450 を同じ扱いにしている。
        /// </para>
        /// </summary>
        public void UpdateSubProfessionFromReplacedSkill(int skillId)
        {
            var resolved = DataTypes.SpecDetectionTables.GetSubProfessionIdByReplacedSkillId(skillId);
            var subProfessionId = (int)resolved;
            if (subProfessionId <= 0 || SubProfessionId == subProfessionId)
            {
                return;
            }

            // 常設の食い違い検知。置換後スキルが示す特化と、控えてあるマーカーが違うなら記録する。
            // 味方に発生させるスキル(安可など)を自分のものとして数えていると、この形で出る。
            if (SpecMarkerBuffId != 0
                && DataTypes.SpecDetectionTables.TryResolveSpecTalentBuff(
                    SpecMarkerBuffId, out _, out var markerSpec, out _)
                && markerSpec != resolved)
            {
                Diagnostics.SpecConflictProbe.CaptureReplacedSkillConflict(
                    skillId, resolved.ToString(), SpecMarkerBuffId, markerSpec.ToString(), UUID);
            }

            SetSubProfessionId(subProfessionId);
            PlayerRosterProjection.AddOrUpdateNearbyPlayer(UUID);
        }

        /// <summary>
        /// タレント由来のバフから特化を確定する。<b>このエンティティが術者であること</b>は
        /// 呼び出し側(<see cref="EncounterManager.ApplySpecFromTalentBuff"/>)が保証する。
        ///
        /// <para>
        /// <paramref name="markerBuffUuid"/> が 0 以外なら、それはマーカー本体(ツリーの根)。
        /// 除去を突き合わせるために実体UUIDを控える。
        /// <b>特化が変わらない再付与でも控え直す</b>ので、シーン切替の再付与にも追従する。
        /// </para>
        /// </summary>
        public void UpdateSubProfessionFromTalentBuff(int subProfessionId, int markerBuffId, int markerBuffUuid)
        {
            if (subProfessionId <= 0)
            {
                return;
            }

            if (markerBuffUuid != 0)
            {
                SpecMarkerBuffId = markerBuffId;
                SpecMarkerBuffUuid = markerBuffUuid;
            }

            if (SubProfessionId == subProfessionId)
            {
                return;
            }

            SetSubProfessionId(subProfessionId);
            PlayerRosterProjection.AddOrUpdateNearbyPlayer(UUID);
        }

        /// <summary>
        /// 控えてあるマーカーバフが除去されたら、特化を未装着(クラスR1)へ戻す。
        /// 一致しなければ何もしないで <c>false</c>。
        ///
        /// <para>
        /// <b><see cref="SetSubProfessionUnknown"/> は使えない。</b> あれは
        /// <see cref="HasBuffSnapshot"/> も落とすので <see cref="IsSpecAbilityUnequipped"/> が
        /// 成立せず、クラスR1ではなく「観測できていない」になる。ここは
        /// 「全部見えている状態でマーカーだけ消えた」と分かっているので、受信済みフラグは残す。
        /// </para>
        /// </summary>
        public bool ClearSubProfessionFromMarkerRemoval(int buffUuid)
        {
            if (buffUuid == 0 || SpecMarkerBuffUuid != buffUuid)
            {
                return false;
            }

            SubProfessionId = 0;
            SpecMarkerBuffId = 0;
            SpecMarkerBuffUuid = 0;
            return true;
        }

        /// <summary>
        /// AOI出現時に届く全バフスナップショットを特化判定に通す。
        ///
        /// <para>
        /// 1件ずつ術者へ帰属させるので、このエンティティが持っているだけの他人のバフは
        /// その術者の判定になり、ここには効かない。
        /// </para>
        ///
        /// <para>
        /// スナップショットは「いまこのエンティティが持っている全バフ」なので、
        /// 処理後もこのエンティティの特化が未確定なら
        /// <b>自分が張ったタレントバフを1つも持っていない＝未装着(Rank1)が確定する</b>。
        /// 差分だけでは「まだ観測していない」と区別できないため、この経路だけが Rank1 を成立させる。
        /// </para>
        /// </summary>
        public void ApplyBuffSnapshotForSpec(
            IReadOnlyList<(int BaseId, int BuffUuid, long FireUuid, int FightSourceType, int SourceConfigId)> buffs)
        {
            HasBuffSnapshot = true;
            var manager = EncounterManager.Current;
            for (var index = 0; index < buffs.Count; index++)
            {
                manager?.ApplySpecFromTalentBuff(
                    buffs[index].BaseId, buffs[index].BuffUuid, buffs[index].FireUuid, buffs[index].FightSourceType,
                    UUID, buffs[index].SourceConfigId);
            }

            // 自分が術者のタレントバフが1件も無かった＝未装着。PTメンバーぶんはキャッシュにも残す。
            if (SubProfessionId == 0 && ProfessionId > 0)
            {
                Services.PartyMemberCache.Instance.SetSpecAbilityUnequipped(Utils.UuidToEntityId(UUID));
                PlayerRosterProjection.AddOrUpdateNearbyPlayer(UUID);
            }
        }

        /// <summary>
        /// 自分のバフ集合を受信したとみなした瞬間を記録する。
        ///
        /// <para>
        /// <b>いまは計測専用で、表示には効かない。</b>
        /// <b>「全バフを受信した」の根拠に使ってはいけない</b> — 自分のバフ追加を含むデルタなら
        /// 何でも立つので、スタックバフ1件でも真になる。
        /// </para>
        /// </summary>
        public void MarkSelfBuffStreamReceived()
        {
            if (HasBuffSnapshot)
            {
                return;
            }

            HasBuffSnapshot = true;
        }

        public SkillSnapshot? AddTakenDamage(
            long attackerUuid, long skillId, int skillLevel, long damage, long hpLessen, long shieldBreak,
            EDamageProperty damageElement, EDamageType damageType, EDamageMode damageMode,
            bool isCrit, bool isLucky, bool isCauseLucky, bool isMiss, bool isDead, ExtraPacketData extraPacketData,
            SkillSnapshotStamp stamp)
        {
            RecalculateInactiveTime(extraPacketData.ArrivalTime);

            return TakenStats.AddData(attackerUuid, skillId, skillLevel, damage, isCrit, isLucky, hpLessen, shieldBreak, isCauseLucky, damageElement, damageType, damageMode, isDead, extraPacketData, GetInactiveTime(), FirstCombatActionTime, stamp);
        }

        /// <param name="carriesBuffInfo">
        /// このイベントが <c>BuffInfo</c>(または <c>BuffChange</c>)を伴っていたか。
        /// <c>false</c> のとき渡ってくる 0 は「送られてこなかった」という意味なので、
        /// 既に知っているバフの持続・層・付与時刻を上書きしない。
        /// </param>
        public void NotifyBuffEvent(EBuffEventType buffEventType, int buffUuid, int baseId, int level, long fireUuid, string entityCasterName, int layer, int duration, int sourceConfigId, TimeSpan encounterTime, DateTime? creationTime, ExtraPacketData extraPacketData, int fightSourceType = 0, bool carriesBuffInfo = true)
        {
            if (buffEventType == EBuffEventType.BuffEventRemove)
            {
                if (!BuffEvents.TryGetValue((ulong)buffUuid, out var buffEvent))
                {

                    buffEvent = new BuffEvent(buffUuid);
                }
                buffEvent.SetRemoveTime(encounterTime.Duration(), extraPacketData.ArrivalTime);

                BuffEvents[(ulong)buffUuid] = buffEvent;
                AddRecentBuffEventHistory(buffUuid, buffEvent);

                // ライブ表示用の状態はエンカウンター境界を跨いで保持する。
                Services.ActiveBuffStore.Instance.Remove(UUID, (ulong)buffUuid);
            }
            else
            {
                var isKnownBuff = BuffEvents.TryGetValue((ulong)buffUuid, out var buffEvent);
                if (!isKnownBuff)
                {
                    buffEvent = new BuffEvent(buffUuid, baseId, level, fireUuid, entityCasterName, layer, duration, sourceConfigId, fightSourceType);
                }
                else if (carriesBuffInfo)
                {
                    buffEvent.SetEvent(buffUuid, baseId, level, fireUuid, entityCasterName, layer, duration, sourceConfigId, fightSourceType);
                }

                // BuffInfo を伴わない回は、持続・層・付与時刻を「知らされていない」まま据え置く。
                // ここで書き込むと、10秒のバフが持続0(=無期限)に化けて除去が来るまで消えなくなり、
                // 付与時刻が毎回ずれることで ActiveBuffStore の同一実体判定も落ちて残り時間が巻き戻る。
                if (!isKnownBuff || carriesBuffInfo)
                {
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
                }

                BuffEvents[(ulong)buffUuid] = buffEvent;
                AddRecentBuffEventHistory(buffUuid, buffEvent);

                // ライブ表示用の状態はエンカウンター境界を跨いで保持する。
                // creationTime はサーバが名乗る付与時刻。ストア側が「もう何秒経ったか」を
                // 差し引くのに使う(AOI出現で受け取る、見る前から乗っているバフのため)。
                Services.ActiveBuffStore.Instance.AddOrUpdate(UUID, (ulong)buffUuid, buffEvent, creationTime);
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

        private bool HasPositiveAttribute(string key)
        {
            return GetAttrKV(key) switch
            {
                int value => value > 0,
                long value => value > 0,
                uint value => value > 0,
                ulong value => value > 0,
                short value => value > 0,
                ushort value => value > 0,
                byte value => value > 0,
                sbyte value => value > 0,
                _ => false
            };
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
                SetAttrKV(newAttr.Key, newAttr.Value);
            }

            foreach (var newSkillMetrics in newEntity.SkillMetrics)
            {
                SkillMetrics.TryGetValue(newSkillMetrics.Key, out var foundSkill);
                if (foundSkill != null)
                {
                    foundSkill.Damage.MergeCombatStats(newSkillMetrics.Value.Damage);
                    foundSkill.Healing.MergeCombatStats(newSkillMetrics.Value.Healing);
                }
                else
                {
                    var metrics = new MetricsContainer
                    {
                        Damage = (CombatStats)newSkillMetrics.Value.Damage.Clone(),
                        Healing = (CombatStats)newSkillMetrics.Value.Healing.Clone(),
                    };
                    SkillMetrics.TryAdd(newSkillMetrics.Key, metrics);
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
        /// <summary>スキル単位の与ダメージ統計。</summary>
        public CombatStats Damage { get; set; } = new();

        /// <summary>スキル単位の回復統計。</summary>
        public CombatStats Healing { get; set; } = new();

        /// <summary>
        /// この鍵がスキルIDではなくバフIDであることを示す。
        /// 畳み先が決まらなかったバフは生のIDのまま行になるので、名前を
        /// <c>GetSkillName</c> ではなく <c>GetBuffName</c> で引く必要がある。
        /// <c>SkillTable</c> と <c>BuffTable</c> は90IDが重複するため、種別を持たないと取り違える。
        /// </summary>
        public bool IsBuffSource { get; set; }
    }

    public class CombatStats : System.ICloneable
    {
        public string Name { get; private set; } = null!;
        public ESkillType SkillType { get; private set; } = ESkillType.Unknown;

        /// <summary>
        /// メーターの行のキー。<c>ownerId</c> と <c>HitEventId</c> を詰めた値で、
        /// <c>CombatDataCatalog.FormatSourceKey</c> で <c>ownerId:枝番</c> に戻る。
        /// </summary>
        public long Id { get; private set; }
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
        private readonly object _skillSnapshotsGate = new();
        private bool _recordsSkillSnapshots;

        /// <summary>
        /// この統計でスナップショットを溜める。<b><see cref="Entity.TakenStats"/> 専用</b>で、
        /// あちらのセッターからしか呼ばれない。<b>既定は溜めない</b> —
        /// 与ダメ・回復のグラフは <see cref="PerSecondTotals"/> で足り、スキル単位の統計には読み手が無い。
        /// 印は <see cref="Clone"/>(MemberwiseClone)でそのまま複製される。
        /// </summary>
        public void EnableSkillSnapshotRecording()
        {
            _recordsSkillSnapshots = true;
        }

        /// <summary>
        /// Newtonsoft の条件付き直列化。記録しない統計では <c>SkillSnapshots</c> を
        /// <b>項目ごと書かない</b>(空配列も残さない)。
        /// </summary>
        public bool ShouldSerializeSkillSnapshots() => _recordsSkillSnapshots;

        public SkillSnapshot[] GetSkillSnapshotsCopy()
        {
            lock (_skillSnapshotsGate)
            {
                return SkillSnapshots.ToArray();
            }
        }

        /// <summary>
        /// 瞬間DPS/HPSグラフの材料。キーは <see cref="EncounterExData.FirstDamageTimeStamp"/> からの
        /// 1秒の区切り(<c>max(ceil(経過秒) - 1, 0)</c>)、値はその1秒に入った合計。
        ///
        /// <para>
        /// グラフは1秒ごとの合計しか使わないので、1件ずつは持たない。
        /// 溜めるのはエンティティ直下の <c>DamageStats</c> / <c>HealingStats</c> だけで、
        /// 足す条件(値が正、ダメージなら <c>Immune</c> 以外)は呼び出し側が決める。
        /// </para>
        /// </summary>
        public Dictionary<int, long> PerSecondTotals { get; private set; } = new();

        /// <summary>
        /// <see cref="PerSecondTotals"/> に足したイベントのうち、最も遅い到着時刻。
        /// グラフの終端がこれより前なら、ここまで伸ばす。
        /// </summary>
        public DateTime? LastPerSecondTimestamp { get; private set; }

        private readonly object _perSecondTotalsGate = new();

        public bool ShouldSerializePerSecondTotals() => PerSecondTotals.Count > 0;

        public bool ShouldSerializeLastPerSecondTimestamp() => LastPerSecondTimestamp.HasValue;

        public void AddPerSecondValue(DateTime timelineStart, DateTime timestamp, long value)
        {
            var seconds = Math.Max((timestamp - timelineStart).TotalSeconds, 0d);
            var second = Math.Max((int)Math.Ceiling(seconds) - 1, 0);

            lock (_perSecondTotalsGate)
            {
                PerSecondTotals[second] = PerSecondTotals.GetValueOrDefault(second) + value;
                if (LastPerSecondTimestamp is not { } last || timestamp > last)
                {
                    LastPerSecondTimestamp = timestamp;
                }
            }
        }

        public KeyValuePair<int, long>[] GetPerSecondTotalsCopy(out DateTime? lastTimestamp)
        {
            lock (_perSecondTotalsGate)
            {
                lastTimestamp = LastPerSecondTimestamp;
                return PerSecondTotals.ToArray();
            }
        }

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

        public SkillSnapshot? AddData(long otherUuid, long skillId, int level, long value, bool isCrit, bool isLucky, long hpLessenValue, long shieldBreak, bool isCauseLucky, EDamageProperty damageElement, EDamageType damageType, EDamageMode damageMode, bool isDead, ExtraPacketData extraPacketData, double inactiveTime, DateTime? startTime, SkillSnapshotStamp stamp)
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

            return AddSnapshot(otherUuid, skillId, level, value, isCrit, isLucky, hpLessenValue, shieldBreak, isCauseLucky, damageElement, damageType, damageMode, isDead, now, stamp);
        }

        public void SetSummonData(long uuid, int level)
        {
            SummonUUID = uuid;
            TierLevel = level;
        }

        public SkillSnapshot? AddSnapshot(long otherUuid, long id, int level, long value, bool isCrit, bool isLucky, long hpLessenValue, long shieldBreak, bool isCauseLucky, EDamageProperty damageElement, EDamageType damageType, EDamageMode damageMode, bool isDead, DateTime timestamp, SkillSnapshotStamp stamp)
        {
            // 記録しない統計(TakenStats 以外)では作りもしない。
            // 実行中のメモリと blob の両方に効く。
            if (!_recordsSkillSnapshots)
            {
                return null;
            }

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
                Sequence = stamp.Sequence,
                TargetHp = stamp.TargetHp,
                TargetMaxHp = stamp.TargetMaxHp,
                OwnerId = stamp.OwnerId,
                DamageSource = stamp.DamageSource,
                BuffSourceSkillId = stamp.BuffSourceSkillId,
                SummonSourceSkillId = stamp.SummonSourceSkillId,
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

            lock (_skillSnapshotsGate)
            {
                SkillSnapshots.Add(snapshot);
            }

            return snapshot;
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

            // 記録しない統計(TakenStats 以外)には取り込まない。
            // 突き合わせは同種同士(entity 直下 ↔ entity 直下 / SkillMetrics[k].X ↔ SkillMetrics[k].X)
            // なので相手も空だが、一覧への書き手はここと AddSnapshot の2つだけなので両方で閉じる。
            if (_recordsSkillSnapshots)
            {
                var snapshots = newCombatStats.GetSkillSnapshotsCopy();
                lock (_skillSnapshotsGate)
                {
                    foreach (var newSnapshot in snapshots)
                    {
                        SkillSnapshots.Add((SkillSnapshot)newSnapshot.Clone());
                    }
                }
            }
        }
    }

    public class SkillSnapshot : System.ICloneable
    {

        public long OtherUUID { get; set; }

        /// <summary>メーターの行のキー。<see cref="CombatStats.Id"/> と同じ形。</summary>
        public long Id { get; set; }
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

        /// <summary>
        /// エンカウンター内の通し番号。被ダメログがエンティティをまたいで並べ直すのに使う。
        /// <b>0 は通し番号を持たない古い記録</b>で、ログには載らない。
        /// </summary>
        public long Sequence { get; set; }

        /// <summary>イベント後の対象の HP。被ダメログの被弾行に出す。</summary>
        public long? TargetHp { get; set; }

        /// <summary>
        /// <c>SyncDamageInfo.OwnerId</c> の生の値。<see cref="DamageSource"/> が <c>Buff</c> ならバフID、
        /// <c>Bullet</c> / <c>FakeBullet</c> なら弾ID、それ以外はスキルID。
        /// <see cref="Id"/> はメーター用に畳んだ鍵なので、被ダメログの技名はこちらで引く。
        /// </summary>
        public int OwnerId { get; set; }

        public EDamageSource DamageSource { get; set; }

        /// <summary>
        /// バフ由来のとき、記録した瞬間に生きていた実体から引いたそのバフの付与元の技ID。決まらなければ 0。
        /// 実体は後から残らないので、表示時には引き直せない。
        /// </summary>
        public int BuffSourceSkillId { get; set; }

        /// <summary>
        /// ダメージを出した実体(仮想体など)を出した技ID。実体の出現時に決めて控えたもの。決まらなければ 0。
        /// バフ経由のものは被弾の頃にはバフが除去されているので、表示時には引き直せない。
        /// </summary>
        public int SummonSourceSkillId { get; set; }

        public long? TargetMaxHp { get; set; }

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

        /// <summary>
        /// この実体を作ったものの種別(<c>EFightSource</c>)。<c>SourceConfigId</c> の中身が
        /// スキルIDかバフIDかはこれで決まるので、片方だけでは発生源を辿れない。
        /// </summary>
        public int FightSourceType { get; private set; }
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
        public BuffEvent(int uuid, int baseId, int level, long fireUuid, string entityCasterName, int layer, int duration, int sourceConfigId, int fightSourceType = 0)
        {
            FightSourceType = fightSourceType;
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

        public void SetEvent(int uuid, int baseId, int level, long fireUuid, string entityCasterName, int layer, int duration, int sourceConfigId, int fightSourceType = 0)
        {
            FightSourceType = fightSourceType;
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
