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

        /// <summary>
        /// シーンを起動時の値(マップ 0・シーン名 null・チャンネル 0)に戻す。ログアウト(<c>ExitGame</c>)で使う。
        /// <b>開いている記録(<see cref="Current"/> と battle 行)には押さない。</b> 直後の保存がログアウト前のマップのまま残るように。
        /// マップ 0 は「まだシーン通知を一度も受け取っていない」状態で、入り直したときの最初のシーン通知は移動ではなく初回確定になる。
        /// </summary>
        internal static void ResetSceneToStartup()
        {
            LevelMapId = 0;
            SceneName = null!;
            ChannelLineId = 0;
        }

        // 予告のバーの終わりの行を、どの到着時刻まで残したか。到着時刻とともに進むだけなので消さない。
        private static DateTime _announcementBarsRecordedUntil = DateTime.MinValue;

        /// <summary>
        /// パケットを1つ処理する直前に呼ぶ(<c>NetCap.BeforeParsePacket</c>)。そのパケットの到着時刻までに終わった予告のバーを、
        /// 今のエンカウンターの被ダメログへ終わりの行として残す。パケットの出来事より前に入るので、並びはバーの終わりの時刻どおりになる。
        /// 途中で消えたバー・上書きされたバーは控え(<see cref="Services.BossDbmBarStore"/>)に無いので残らない。
        /// </summary>
        public static void RecordEndedAnnouncementBars(DateTime arrivalTime)
        {
            if (arrivalTime <= _announcementBarsRecordedUntil)
            {
                return;
            }

            var ended = Services.BossDbmBarStore.Instance.GetEndedBetween(_announcementBarsRecordedUntil, arrivalTime);
            _announcementBarsRecordedUntil = arrivalTime;
            foreach (var bar in ended)
            {
                Current.AddSkillAnnouncementBarEnd(bar);
            }
        }
        public delegate void BattleStartEventHandler(EventArgs e);
        public static event BattleStartEventHandler? BattleStart;
        public delegate void EncounterStartEventHandler(EncounterStartEventArgs e);
        public static event EncounterStartEventHandler? EncounterStart;
        public delegate void EncounterEndEventHandler(EventArgs e);
        public static event EncounterEndEventHandler? EncounterEnd;
        public delegate void EncounterEndFinalEventHandler(EncounterEndFinalData e);
        public static event EncounterEndFinalEventHandler? EncounterEndFinal;

        /// <summary>
        /// 走っている計測の長さ(秒)と、最初に攻撃した敵だけを数えるか。計測の開始のときに設定(<see cref="CombatRuntimeSettings"/>)から控え、
        /// 計測中に作る回へ写す。計測の途中で設定を保存しても、その計測は控えた値のまま。
        /// </summary>
        private static int _benchmarkDurationSeconds;
        private static bool _benchmarkFirstTargetOnly;

        /// <summary>
        /// 計測中か(押してから、停止・ログアウト・始まった後のマップ移動まで)。書くのは計測の開始・停止・中断だけ。
        /// 立っている間の <see cref="Current"/> は計測の回(<see cref="EncounterExData.BenchmarkTime"/> を持つ)。
        /// </summary>
        public static bool IsBenchmarkActive { get; private set; }

        /// <summary>計測が始まったか(計測の回に起点が立った)。立つ前は待機中。</summary>
        public static bool HasBenchmarkBegun => IsBenchmarkActive && Current?.ExData.FirstDamageTimeStamp != null;

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
            // 計測が始まった後は区切らない(窓の途中で回が割れないように)。終わらせる側(停止・ログアウト・マップ移動)は、
            // 先に印を下ろしてから呼ぶ。待機中は区切ってよい(記録が無く、作り直した回も計測の回になる)。
            if (HasBenchmarkBegun)
            {
                Serilog.Log.Information("Encounter split skipped during benchmark: reason={Reason} caller={Caller}", reason, enterDungeonCaller);
                return;
            }

            string priorBossName = "";
            int priorEncounterPhase = 0;

            if (Current != null)
            {
                bool hasStatsBeenRecorded = Current.HasStatsBeenRecorded();

                // 開いている回は、記録の有無に関係なく閉じてから作り直す。
                if (force || Current.EndTime == DateTime.MinValue)
                {
                    StopEncounter(true);
                }

                if (reason == EncounterStartReason.Wipe)
                {
                    // 全滅の印は、全滅で区切る回にだけ付ける。
                    Current.SetWipeState(true);

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
                // 保存すると履歴の一覧が空の記録で埋まる。
                nextEncounterIdModifier = Current.HasStatsBeenRecorded() ? 1UL : 0UL;
            }

            if (MessageManager.currentUserUuid != 0)
            {
                Entity? priorSelf = null;
                priorEncounter?.Entities.TryGetValue(MessageManager.currentUserUuid, out priorSelf);
            }

            // 画面に出している素性を焼き付ける。<b>作り直しで運ぶより前に行うこと</b> —
            // 後ろに置くと次のエンカウンターへ運ばれるのは焼き付ける前の値になり、
            // 保存した回と次の回で同じ人の素性が食い違う。とくに NPC の印は実体に残らないと、
            // 社交データが切れた回(ダンジョン退出・途中からの起動)で失われる。
            if (priorEncounter != null && nextEncounterIdModifier != 0)
            {
                ApplyDisplayedIdentitiesForRecord(priorEncounter);
            }

            Current = new Encounter(CurrentBattleId);
            Current.EncounterId = DB.GetNextEncounterId() + nextEncounterIdModifier;
            System.Diagnostics.Debug.WriteLine($"Created new encounter for EncounterId {Current.EncounterId} + ({nextEncounterIdModifier})");
            // 素性を捨ててよい区切りは、ログアウト(ExitGame)だけ。それ以外の理由(ダンジョン状態 Null・Playing の None を含む)は運ぶ。
            // 起動時は前の回が無いので何も運ばない。
            if (priorEncounter != null && reason != EncounterStartReason.ExitGame)
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
                // シーン名は難易度から難易度名を引くので、難易度を先に入れる。
                Current.SetDungeonDifficulty(currentDifficulty);
                SetSceneId(LevelMapId, true);
            }

            // 計測中に作った回は計測の回(待機中の作り直しも)。計測時間を写しておくと、履歴でも同じ窓で読める。
            if (IsBenchmarkActive)
            {
                Current.ExData.BenchmarkTime = _benchmarkDurationSeconds;
                Current.BenchmarkFirstTargetOnly = _benchmarkFirstTargetOnly;
            }

            if (priorEncounter != null)
            {
                if (nextEncounterIdModifier != 0)
                {
                    // 素性の焼き付けは作り直しの前に済ませてある(持ち越しへ載せるため)。
                    DB.InsertEncounter(priorEncounter);
                    priorEncounter.ReportUnresolvedBlankSources();
                    GC.Collect();
                }
            }

            AllowSceneUpdate = true;

            // 統計が0になった人(AOI外でメーターにだけ居た灰色の行)をプレイヤーリストからも外す。
            // 素性の焼き付け(ApplyDisplayedIdentitiesForRecord がリストの表示値を読む)より後に置く。
            PlayerRosterProjection.RebuildRoster();

            // エンカウンターの作り直しも「次のイベント」。計測・リセット・マップ移動・
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
                        // 制限時間はダンジョンの開始(回を作った時刻)から測る。呼ばれるのは終了時刻を入れた後。
                        if ((Current.EndTime - Current.StartTime).TotalSeconds >= (sceneEventDungeonConfig.LimitTime + Current.ExData.DungeonTimeDeathChange))
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

            if (Current != null)
            {
                SaveCurrentForRecordAndCloseBattle();
            }

        }

        /// <summary>
        /// 今のエンカウンターを記録として締める。<b>終了時刻は呼ぶ側で入れておくこと。</b>
        ///
        /// <para>
        /// マップ移動の保存(<see cref="EnterDungeon"/> → <see cref="StartEncounter"/>、battle 行は <see cref="StartNewMap"/>)と同じ結果にそろえる:
        /// 画面に出している素性を焼き付けてから保存し、battle 行を閉じる。
        /// マップ移動の側は流れの途中に保存が組み込まれているので、この関数を通らない。手順を変えるときは両方を直す。
        /// </para>
        /// </summary>
        internal static void SaveCurrentForRecordAndCloseBattle()
        {
            if (Current.HasStatsBeenRecorded())
            {
                ApplyDisplayedIdentitiesForRecord(Current);
                DB.InsertEncounter(Current);
                Current.ReportUnresolvedBlankSources();
            }

            if (CurrentBattleId != 0)
            {
                DB.UpdateBattleEnd(CurrentBattleId);
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

        /// <summary>
        /// 記録する回に、<b>いま画面に出している素性をエンティティへ焼き付ける。</b>
        /// <b>エンカウンターの作り直しより前に呼ぶこと</b> — 焼いた値をそのまま次の回へ持ち越すため。
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
                    displayed.SeasonTalentBuffId,
                    displayed.IsSeasonTalentInactive,
                    displayed.CombatPower,
                    displayed.Level,
                    displayed.SeasonLevel,
                    displayed.SeasonStrength,
                    displayed.MaxHp,
                    displayed.IsNpc);
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
            SceneName = levelMapId > 0 ? CombatDataCatalog.GetSceneName(levelMapId, Current.ExData.DungeonDifficulty) : "";

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

            SceneName = CombatDataCatalog.GetSceneName(LevelMapId, Current.ExData.DungeonDifficulty);

            // 投影は internal なので App からは触れない。ここまでを1つの操作にする。
            PlayerRosterProjection.UpdateMapName();
            NearbyEntityProjection.UpdateMapName();
        }

        /// <summary>
        /// ダンジョン同期で届いた難易度を現在のエンカウンターへ入れる。<b>変わったらマップ名を組み立て直す。</b>
        ///
        /// <para>
        /// 難易度はシーン切替の後に届くので、そのときのマップ名には難易度名が付いていない。
        /// 現在のエンカウンターのシーン名と投影のマップ名の両方を直す。
        /// </para>
        /// </summary>
        public static void ApplyDungeonDifficulty(int difficulty)
        {
            if (Current.ExData.DungeonDifficulty == difficulty)
            {
                return;
            }

            Current.SetDungeonDifficulty(difficulty);
            if (LevelMapId == 0)
            {
                return;
            }

            SetSceneId(LevelMapId, force: true);
            PlayerRosterProjection.UpdateMapName();
            NearbyEntityProjection.UpdateMapName();
        }

        /// <summary>
        /// 計測の開始と停止を切り替える。<b>パケットを処理するスレッドで呼ぶ</b>(<c>MeterSnapshotProvider.ToggleBenchmark</c>)。
        ///
        /// <para>
        /// 開始は計測の回を作って待つだけ。始まるのは自分の与ダメか自分が出した回復の通知が来て起点が立ったとき
        /// (<c>MessageManager</c> の起点の判定)。停止は、始まる前でも後でも印を下ろしてから回を区切る
        /// (記録があれば計測の注記つきで保存される)。
        /// </para>
        /// </summary>
        internal static void ToggleBenchmark()
        {
            if (IsBenchmarkActive)
            {
                Serilog.Log.Information("Benchmark stopped by the user (begun={HasBegun})", HasBenchmarkBegun);
                IsBenchmarkActive = false;
                EnterDungeon(true, EncounterStartReason.BenchmarkEnd);
                return;
            }

            _benchmarkDurationSeconds = CombatRuntimeSettings.BenchmarkDurationSeconds;
            _benchmarkFirstTargetOnly = CombatRuntimeSettings.BenchmarkFirstTargetOnly;
            Serilog.Log.Information(
                "Benchmark started: waiting for the player's first damage or healing ({Seconds} s window, first target only={FirstTargetOnly})",
                _benchmarkDurationSeconds,
                _benchmarkFirstTargetOnly);
            IsBenchmarkActive = true;
            EnterDungeon(true, EncounterStartReason.BenchmarkStart);
        }

        /// <summary>
        /// 今の回を区切り直す(リセット)。<b>パケットを処理するスレッドで呼ぶ</b>(<c>MeterSnapshotProvider.ResetCurrentEncounter</c>)。
        /// 計測が始まった後は計測の停止、待機中は計測の回の作り直し。
        /// </summary>
        internal static void ResetCurrentEncounter()
        {
            if (IsBenchmarkActive)
            {
                if (HasBenchmarkBegun)
                {
                    Serilog.Log.Information("Benchmark ended by a manual reset");
                    IsBenchmarkActive = false;
                    EnterDungeon(true, EncounterStartReason.BenchmarkEnd);
                }
                else
                {
                    EnterDungeon(true, EncounterStartReason.BenchmarkStart);
                }

                return;
            }

            EnterDungeon(true, BattleStateMachine.IsInOpenWorld() ? EncounterStartReason.Force : EncounterStartReason.NewObjective);
        }

        /// <summary>
        /// 計測の印だけを下ろす。回の保存と作り直しは呼び手のこの後の <see cref="EnterDungeon"/> が行う(計測の注記つきで保存される)。
        /// 呼ぶのはログアウト(待機中でも終える)と、始まった後のマップ移動(<paramref name="onlyIfBegun"/>。待機中のマップ移動は待ち続ける)。
        /// </summary>
        internal static void EndBenchmarkBeforeSplit(string cause, bool onlyIfBegun)
        {
            if (!IsBenchmarkActive || (onlyIfBegun && !HasBenchmarkBegun))
            {
                return;
            }

            Serilog.Log.Information("Benchmark ended by {Cause} (begun={HasBegun})", cause, HasBenchmarkBegun);
            IsBenchmarkActive = false;
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
        TimedOut = 5,
        BenchmarkStart = 6,
        BenchmarkEnd = 7,
        DungeonStateEnd = 8,

        /// <summary>ログアウト(ログイン画面へ戻った)。素性を次の回へ運ばない唯一の理由。</summary>
        ExitGame = 9,
    }

    public class EncounterStartEventArgs : EventArgs
    {
        public EncounterStartReason Reason;
    }

    /// <summary>
    /// 戦闘の時計を1回読んだ値(<see cref="Encounter.ReadCombatClock()"/>)。時刻は UTC。
    /// 起点が無ければ <see cref="StartUtc"/> / <see cref="EndUtc"/> は null で、経過は 0。
    /// 1回の表示の中では1回だけ読み、全部の値を同じ終点で出す。
    /// </summary>
    /// <param name="IsClosed">閉じた回(終了時刻がある)か、計測の窓が終わった。</param>
    public readonly record struct CombatClockReading(DateTime? StartUtc, DateTime? EndUtc, TimeSpan Elapsed, bool IsClosed)
    {
        /// <summary>秒間値。<paramref name="seconds"/> が1秒未満なら合計そのもの。</summary>
        public static double PerSecond(ulong total, double seconds)
        {
            return seconds >= 1d ? Math.Round(total / seconds, 0) : total;
        }
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

        /// <summary>
        /// 回を作った時刻(<c>DateTime.Now</c>、ローカル)。戦闘の経過には使わない(経過は <see cref="ReadCombatClock()"/>)。
        /// 使うのは履歴の一覧の時刻・DB の並び・ダンジョンの制限時間だけ。
        /// </summary>
        public DateTime StartTime { get; private set; }

        /// <summary>回を閉じた時刻(<c>DateTime.Now</c>、ローカル)。<c>DateTime.MinValue</c> なら開いている。閉じた回の時計の終点。</summary>
        public DateTime EndTime { get; private set; }
        public DateTime LastUpdate { get; set; }
        public ConcurrentDictionary<long, Entity> Entities { get; set; } = [];

        public ulong TotalDamage { get; set; } = 0;
        public ulong TotalShieldBreak { get; set; } = 0;
        public ulong TotalHealing { get; set; } = 0;
        public ulong TotalOverhealing { get; set; } = 0;

        /// <summary>
        /// 被ダメログに行があるか。記録すべき戦闘かの判定(<see cref="HasStatsBeenRecorded"/>)に使う。
        ///
        /// <para>
        /// <b>行数をそのまま見る。</b> 載る経路は予告・詠唱・被弾・ダメージの無い死亡の4つで、
        /// 印を別に持つと経路が増えたときに立て忘れる。判定は実行中のエンカウンターでしか行わないので DB には持たない。
        /// </para>
        /// </summary>
        public bool HasTakenDamageLogRecords
        {
            get
            {
                lock (_takenDamageLogGate)
                {
                    return _takenDamageLog.Count > 0;
                }
            }
        }
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

        // 着地先が決まらなかった行の控え。鍵は(記録先の UUID, 行代表キー)、値は最初に着かなかった記録。
        // 後の記録で着けば消え、保存の直後に残りを検知ログへ送る(ReportUnresolvedBlankSources)。DB には入れない。
        private readonly object _unresolvedBlankSourcesGate = new();
        private readonly Dictionary<(long EntityUuid, long RowKey), UnresolvedBlankSource> _unresolvedBlankSources = [];

        private sealed record UnresolvedBlankSource(
            string KeyText,
            bool IsBuffSource,
            bool IsHealing,
            ulong ValueTotal,
            ulong HitsCount,
            long CharacterId,
            string Trace,
            DateTime FirstBlankAt);

        public Encounter()
        {

        }

        public Encounter(int battleId = 0)
        {
            StartTime = DateTime.Now;
            Entities = new();
            BattleId = battleId;
        }

        public void SetEndTime(DateTime end)
        {
            EndTime = end;
        }

        /// <summary>計測の回か(計測時間を持つ)。履歴でも同じ値(DB に載る)。</summary>
        public bool IsBenchmark => ExData.BenchmarkTime > 0;

        /// <summary>計測の窓の終わり(起点 + 計測時間、UTC)。計測の回で起点があるときだけ。</summary>
        public DateTime? BenchmarkWindowEndUtc =>
            IsBenchmark && ExData.FirstDamageTimeStamp is { } start ? start.AddSeconds(ExData.BenchmarkTime) : null;

        /// <summary>計測の窓が終わったか(計測の回で起点があり、今が窓の終わり以降)。</summary>
        public bool IsBenchmarkWindowElapsed(DateTime utcNow) => BenchmarkWindowEndUtc is { } windowEnd && utcNow >= windowEnd;

        /// <summary>
        /// 計測で、自分が最初にダメージを与えた敵へのダメージだけを数えるか。計測の開始のときの設定を
        /// <c>EncounterManager.EnterDungeon</c> が写す。DB には載らない。
        /// </summary>
        public bool BenchmarkFirstTargetOnly { get; internal set; }

        /// <summary>計測の回で最初にダメージを与えた敵(<see cref="BenchmarkFirstTargetOnly"/> のときだけ決める)。決まる前は 0。</summary>
        private long _benchmarkFirstTargetUuid;

        /// <summary>
        /// 最初に攻撃した敵だけを数える切り替えの門。計測の窓の中の自分の与ダメで呼ぶ(<see cref="AddDamage"/>)。
        /// 最初の1件で敵を決め、ほかの敵への与ダメは記録しない。
        /// </summary>
        private bool IsBenchmarkFirstTarget(long targetUuid)
        {
            if (!IsBenchmark || !BenchmarkFirstTargetOnly)
            {
                return true;
            }

            if (_benchmarkFirstTargetUuid == 0)
            {
                _benchmarkFirstTargetUuid = targetUuid;
                Serilog.Log.Information("Benchmark first target fixed: target={TargetUuid} (encounter={EncounterId})", targetUuid, EncounterId);
            }

            return targetUuid == _benchmarkFirstTargetUuid;
        }

        public CombatClockReading ReadCombatClock() => ReadCombatClock(DateTime.UtcNow);

        /// <summary>
        /// 戦闘の時計を読む。経過・DPS/HPS の分母・推移グラフ・被ダメログの時刻の起点はどれもここ。
        ///
        /// <para>
        /// 起点は <see cref="EncounterExData.FirstDamageTimeStamp"/>(最初の戦闘の出来事の到着時刻、UTC)。無ければ経過は 0。
        /// 終点はライブなら <paramref name="utcNow"/>、閉じた回なら <see cref="EndTime"/>(UTC へ直すのはここだけ)。
        /// 計測の回は窓の終わりを上限にし、窓が終われば閉じた扱い。どれも DB に載る値から決まるので、履歴でも同じ式。
        /// </para>
        /// </summary>
        internal CombatClockReading ReadCombatClock(DateTime utcNow)
        {
            var isClosed = EndTime != DateTime.MinValue;
            if (ExData.FirstDamageTimeStamp is not { } start)
            {
                return new CombatClockReading(null, null, TimeSpan.Zero, isClosed);
            }

            var end = isClosed ? EndTime.ToUniversalTime() : utcNow;
            if (BenchmarkWindowEndUtc is { } windowEnd && end >= windowEnd)
            {
                end = windowEnd;
                isClosed = true;
            }

            // 時刻の出所(キャプチャ時刻・今・閉じた時刻)の食い違い。経過を 0 にして、回ごとに1回だけ残す。
            if (end < start)
            {
                if (!_combatClockOrderWarned)
                {
                    _combatClockOrderWarned = true;
                    Serilog.Log.Warning("Combat clock end {End:o} precedes its start {Start:o} (encounter={EncounterId}); elapsed clamped to zero",
                        end, start, EncounterId);
                }

                end = start;
            }

            return new CombatClockReading(start, end, end - start, isClosed);
        }

        private bool _combatClockOrderWarned;

        /// <summary>
        /// 与ダメ・被弾で戦闘の時計の起点を立てる(<c>MessageManager</c> の起点の判定)。回復は <see cref="AddHealing"/> が自分で立てる。
        /// 一度立てたら動かさない。
        /// </summary>
        internal void StartCombatClock(DateTime arrivalUtc)
        {
            ExData.FirstDamageTimeStamp ??= arrivalUtc;
        }

        /// <summary>
        /// 起点の後の出来事か。回復・行動時刻・発動の数は、起点より前を数えない
        /// (過剰回復だけの回復は起点を立てないので、起点の前の過剰回復は数えない)。
        /// </summary>
        private bool IsAfterCombatClockStart(DateTime arrivalUtc)
        {
            return ExData.FirstDamageTimeStamp is { } start && arrivalUtc >= start;
        }

        /// <summary>
        /// 記録してよい出来事か(計測の回の門)。計測の回でなければ常に真。
        ///
        /// <para>
        /// 計測の回は、窓 [起点, 起点 + 計測時間) の中で、かつ自分の出来事のときだけ真。待機中(起点の前)も窓の後も記録しない。
        /// <paramref name="playerUuid"/> は記録の主のプレイヤー(与ダメ・回復の出し手、被弾・死亡の本人、発動したプレイヤー)。
        /// プレイヤーに属さない記録(敵の詠唱・予告・予告のバーの終わり・敵の死亡)は null で、窓だけを見る。
        /// 出し手がプレイヤー以外の回復は 0 を渡す(自分ではないので記録しない)。
        /// </para>
        ///
        /// <para>
        /// 止めるのは記録だけ。特化・職業の判定、CD の推定、バフ、予告のバーの控えは止めない。
        /// </para>
        /// </summary>
        private bool IsRecordedInBenchmark(DateTime arrivalUtc, long? playerUuid)
        {
            if (!IsBenchmark)
            {
                return true;
            }

            if (ExData.FirstDamageTimeStamp is not { } start
                || arrivalUtc < start
                || arrivalUtc >= start.AddSeconds(ExData.BenchmarkTime))
            {
                return false;
            }

            return playerUuid is not { } uuid
                || (uuid != 0 && uuid == AppState.PlayerUUID);
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
            }
        }

        public void SetAttrKV(long uuid, string key, object value)
        {
            var entity = GetOrCreateEntity(uuid);

            // 死亡状態のプレイヤーの HP は 0。ダメージの無い即死では、サーバは死亡中も
            // 死ぬ前の HP を送ってくるので、届いた値をそのまま書くと死んだ人に HP が残る。
            // 復活では状態が先に死亡から外れ、HP はその後に届く。
            if (key == "AttrHp"
                && (EEntityType)Utils.UuidToEntityType(uuid) == EEntityType.EntChar
                && entity.GetAttrKV("AttrState") is EActorState.ActorStateDead
                && value is long hpValue
                && hpValue != 0)
            {
                value = 0L;
            }

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
            else if (key == "AttrState")
            {
                // 死亡数は数えない(プレイヤー以外は RecordNonPlayerDeath、プレイヤーは RecordPlayerDeath)。HP はいつでも 0 にする。
                if ((EActorState)value == EActorState.ActorStateDead)
                {
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

                RecordBossDbmBar(sceneEvent.IntParams?.Count ?? 0, skillId, duration, insertion, extraPacketData);
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

        /// <summary>
        /// プレイヤーの死亡を1回数える。呼ぶのは差分で <c>AttrDeadTime</c> が新しい値になったとき(<c>MessageManager</c>)。
        /// 計測の回では窓の中の自分の死亡だけ数える(<see cref="IsRecordedInBenchmark"/>)。
        /// </summary>
        public void RecordPlayerDeath(long playerUuid, DateTime arrivalUtc)
        {
            if (!IsRecordedInBenchmark(arrivalUtc, playerUuid))
            {
                return;
            }

            GetOrCreateEntity(playerUuid).IncrementDeaths();
            IncrementDeaths();
        }

        /// <summary>
        /// プレイヤー以外の死亡(状態が「死亡」になった)を1回数える。呼ぶのは <c>MessageManager</c> の属性の処理。
        /// プレイヤーは状態の「死亡」が、死亡の印つきの被弾と同じ差分でいきなり復活(27)になる回に届かず、
        /// 同じ人への再送も届くので、ここでは数えない(<see cref="RecordPlayerDeath"/>)。
        /// </summary>
        public void RecordNonPlayerDeath(long uuid, DateTime arrivalUtc)
        {
            var entity = GetOrCreateEntity(uuid);
            if (entity.EntityType == EEntityType.EntChar || !IsRecordedInBenchmark(arrivalUtc, null))
            {
                return;
            }

            entity.IncrementDeaths();
            if (entity.EntityType == EEntityType.EntMonster)
            {
                IncrementNpcDeaths();
            }
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

        /// <summary>
        /// このエンカウンターに記録すべき戦闘があったか。**プレイヤーのダメージか回復か、被ダメログの行が1でもあれば真。**
        ///
        /// <para>
        /// <b>ダメージだけで判定しない</b> — 回復のみの戦闘が落ちる。
        /// <b>被ダメログの行も数える</b> — 殴られただけの戦闘に加えて、ギミックの即死・詠唱・構えだけの回も残す
        /// (被弾を印にすると、ダメージの無い死亡や予告だけの回が履歴に残らない)。敵の攻撃そのもの(敵同士の戦闘を含む)は数えない。
        /// 見るのは <c>TotalDamage</c> / <c>TotalHealing</c> / <see cref="HasTakenDamageLogRecords"/> の3つ。
        /// </para>
        /// </summary>
        public bool HasStatsBeenRecorded()
        {
            return TotalDamage > 0 || TotalHealing > 0 || HasTakenDamageLogRecords;
        }

        /// <summary>
        /// 技の発動を1回受ける(<c>AttrSkillId</c> に技が入った)。CD の推定へ流し(<see cref="SkillActivated"/>)、特化を判定し、
        /// 記録してよければ発動の数を数える。数えるのは起点の後だけで、計測の回では窓の中の自分の発動(プレイヤー以外は窓の中)だけ。
        /// </summary>
        /// <param name="activationUtc">発動が届いたメッセージの到着時刻。CD の推定の時計もこれ。</param>
        public void RegisterSkillActivation(long uuid, int skillId, DateTime activationUtc)
        {
            var entity = GetOrCreateEntity(uuid);
            OnSkillActivated(new SkillActivatedEventArgs { CasterUuid = uuid, SkillId = skillId, ActivationDateTime = activationUtc });

            var playerUuid = (EEntityType)Utils.UuidToEntityType(uuid) == EEntityType.EntChar ? uuid : (long?)null;
            entity.RegisterSkillActivation(
                skillId,
                activationUtc,
                IsAfterCombatClockStart(activationUtc) && IsRecordedInBenchmark(activationUtc, playerUuid));
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

        /// <summary>
        /// 着地先より前に名前が決まらない行(見出し表にも料理の表にも名前が無い行)の出どころ(着地先)を決めて控える。
        /// 技に着いたもの・着地先が食い違ったものは常設の検知ログ(<c>BlankSourceNameProbe</c>)へすぐ送る。オプションに着いたものは送らない。
        /// 着かなかったものは送らずにこのエンカウンターに控え、後の記録で着けば控えを消す。後で着けば DB に空欄は残らないので、
        /// 残ったものだけを保存の直後に <see cref="ReportUnresolvedBlankSources"/> が送る。
        /// 記録(<c>AddDamage</c> 等)と <see cref="MarkBuffSourcedSkill"/> の後に、プレイヤーの記録にだけ呼ぶこと。
        ///
        /// <para>
        /// 対象はスキル詳細ウィジェットに行が出る条件(その種別の累計が 0 より大きい)の行だけ。
        /// 名前の有無は表示と同じ <see cref="CombatDataCatalog.GetSourceNameBeforeLanding"/>(注記を付ける前の名前)で見る。
        /// 表示名は空欄でも内部ID注記が付いて空文字にならず、注記は表示設定で消えるので、表示名で見ると設定次第で結果が変わる。
        /// </para>
        ///
        /// <para>
        /// 着地先は最初に着いた先で決め、上書きしない。
        /// </para>
        /// </summary>
        public void ResolveSourceLanding(
            long entityUuid,
            long skillId,
            bool isHealing,
            EDamageSource damageSource,
            int ownerId,
            long attackerRawUuid,
            DateTime arrivalTime)
        {
            if (!Entities.TryGetValue(entityUuid, out var entity)
                || !entity.SkillMetrics.TryGetValue(skillId, out var container))
            {
                return;
            }

            var stats = isHealing ? container.Healing : container.Damage;
            if (stats.ValueTotal == 0UL
                || !string.IsNullOrEmpty(CombatDataCatalog.GetSourceNameBeforeLanding(skillId, container.IsBuffSource)))
            {
                return;
            }

            var landing = Services.SourceLandingResolver.Instance.Resolve(
                damageSource, ownerId, attackerRawUuid, entityUuid, arrivalTime, out var trace);
            var keyText = CombatDataCatalog.FormatSourceKey(skillId);

            if (container.LandingKind != SourceLandingKind.None)
            {
                if (landing.Kind != SourceLandingKind.None && landing != container.Landing)
                {
                    Diagnostics.BlankSourceNameProbe.CaptureConflict(
                        skillId, keyText, isHealing, entity.UID, container.Landing, landing, trace);
                }

                return;
            }

            if (landing.Kind == SourceLandingKind.None)
            {
                lock (_unresolvedBlankSourcesGate)
                {
                    _unresolvedBlankSources.TryAdd(
                        (entityUuid, skillId),
                        new UnresolvedBlankSource(
                            keyText,
                            container.IsBuffSource,
                            isHealing,
                            stats.ValueTotal,
                            stats.HitsCount,
                            entity.UID,
                            trace,
                            arrivalTime));
                }

                return;
            }

            container.LandingKind = landing.Kind;
            container.LandingId = landing.Id;
            lock (_unresolvedBlankSourcesGate)
            {
                _unresolvedBlankSources.Remove((entityUuid, skillId));
            }

            if (landing.Kind != SourceLandingKind.RogueEntry)
            {
                Diagnostics.BlankSourceNameProbe.Capture(
                    skillId,
                    keyText,
                    container.IsBuffSource,
                    isHealing,
                    stats.ValueTotal,
                    stats.HitsCount,
                    entity.UID,
                    landing,
                    trace);
            }
        }

        /// <summary>
        /// 着地先が決まらないまま残った行(<see cref="ResolveSourceLanding"/> の控え)を常設の検知ログ(<c>BlankSourceNameProbe</c>)へ送り、控えを空にする。
        /// <b>DB へ保存した直後に呼ぶ。</b> 保存しないエンカウンターでは呼ばない(DB に空欄が残らない)。
        /// </summary>
        public void ReportUnresolvedBlankSources()
        {
            List<(long RowKey, UnresolvedBlankSource Blank)> unresolved;
            lock (_unresolvedBlankSourcesGate)
            {
                unresolved = _unresolvedBlankSources.Select(pair => (pair.Key.RowKey, pair.Value)).ToList();
                _unresolvedBlankSources.Clear();
            }

            foreach (var (rowKey, blank) in unresolved)
            {
                Diagnostics.BlankSourceNameProbe.Capture(
                    rowKey,
                    blank.KeyText,
                    blank.IsBuffSource,
                    blank.IsHealing,
                    blank.ValueTotal,
                    blank.HitsCount,
                    blank.CharacterId,
                    SourceLanding.None,
                    blank.Trace,
                    blank.FirstBlankAt);
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
            var attacker = GetOrCreateEntity(attackerUuid);

            // ダメージは RegisterSkillActivation を通らない別経路なので、ここでも引く。
            // 詠唱の属性を取りこぼした場合の受け皿。同じ特化なら中で何もしない。判定は止めないので、計測の門より前に置く。
            // 渡すのは畳む前の生のスキルID。理由は identitySkillId の説明を見ること。
            attacker.UpdateSubProfessionFromReplacedSkill(identitySkillId);

            if (!IsRecordedInBenchmark(extraPacketData.ArrivalTime, attackerUuid)
                || !IsBenchmarkFirstTarget(targetUuid))
            {
                return;
            }

            // プレイヤーからプレイヤー以外への与ダメは、起点の判定(MessageManager)が同じ差分で先に時計を立てている。
            // 無いなら判定と振り分けの条件が食い違っている。
            var timelineStart = ExData.FirstDamageTimeStamp
                ?? throw new InvalidOperationException(
                    $"Damage recorded before the combat clock started (encounter={EncounterId}, attacker={attackerUuid}, target={targetUuid}).");

            // 戦闘は「次のイベント」なので履歴表示を解除する。開いていなければ即戻る。
            EncounterHistoryProvider.NotifyLiveEncounterEvent();

            LastUpdate = extraPacketData.ArrivalTime;

            if (damageType != EDamageType.Immune)
            {
                TotalDamage += (ulong)damage;
                if (damageType == EDamageType.Absorbed)
                {
                    TotalShieldBreak += (ulong)shieldBreak;
                }
            }

            attacker.RecordCombatAction(extraPacketData.ArrivalTime);
            attacker.AddDamage(targetUuid, skillId, skillLevel, damage, hpLessen, shieldBreak, damageElement, damageType, damageMode, isCrit, isLucky, isCauseLucky, isMiss, isDead, extraPacketData);

            if (damage > 0 && damageType != EDamageType.Immune)
            {
                attacker.DamageStats.AddPerSecondValue(timelineStart, extraPacketData.ArrivalTime, damage);
            }
        }

        /// <param name="damage">
        /// 足す回復量。回復の通知なら実際に増えた HP(<c>ActualValue</c>、ゲーム内メーターと同じ)、通知の無い回復(薬・料理・自然回復)なら HP の増え。
        /// </param>
        /// <param name="nominalHealing">
        /// 名目の回復量(通知の <c>Value</c>、0 なら <c>LuckyValue</c>)。<paramref name="damage"/> との差を過剰回復として数える。
        /// 通知の無い回復は <paramref name="damage"/> と同じ値を渡す(過剰回復は 0)。
        /// </param>
        public void AddHealing(
            long attackerUuid, long targetUuid, long skillId, int skillLevel, long damage, long nominalHealing, long hpLessen, long shieldBreak,
            EDamageProperty damageElement, EDamageType damageType, EDamageMode damageMode,
            bool isCrit, bool isLucky, bool isCauseLucky, bool isMiss, bool isDead, ExtraPacketData extraPacketData)
        {
            // ダメージの有無で回復を捨てる門を置かない。回復だけの戦闘でも HPS メーターが起動するよう、回復で起点を立てる。
            // 過剰回復だけの回復(実際に増えた HP が 0)では立てない。計測の回の起点は MessageManager の判定だけ。
            if (damage > 0 && !IsBenchmark)
            {
                ExData.FirstDamageTimeStamp ??= extraPacketData.ArrivalTime;
            }

            // 起点より前の回復は、何も記録しない(過剰回復・行動時刻・履歴表示の解除も)。計測の回は窓の中の自分の回復だけ(出し手がプレイヤー以外の回復は attackerUuid が 0)。
            if (!IsAfterCombatClockStart(extraPacketData.ArrivalTime)
                || !IsRecordedInBenchmark(extraPacketData.ArrivalTime, attackerUuid))
            {
                return;
            }

            var timelineStart = ExData.FirstDamageTimeStamp!.Value;

            // 戦闘は「次のイベント」なので履歴表示を解除する。開いていなければ即戻る。
            EncounterHistoryProvider.NotifyLiveEncounterEvent();

            LastUpdate = extraPacketData.ArrivalTime;

            var overhealing = Math.Max(0L, nominalHealing - damage);
            TotalOverhealing += (ulong)overhealing;

            var entity = GetOrCreateEntity(attackerUuid);
            GetOrCreateEntity(targetUuid);

            // 実際に増えた HP が 0 の回復(満タンの人への回復)は、ゲーム内メーターと同じく回復として数えない。
            // 名目の全額を過剰回復に足すだけで、回数・会心・幸運・秒ごとの値・行動時間には入れない。
            if (damage <= 0)
            {
                entity.TotalOverhealing += (ulong)overhealing;
                return;
            }

            TotalHealing += (ulong)damage;

            entity.RecordCombatAction(extraPacketData.ArrivalTime);
            entity.AddHealing(targetUuid, skillId, skillLevel, damage, overhealing, damage, hpLessen, shieldBreak, damageElement, damageType, damageMode, isCrit, isLucky, isCauseLucky, isMiss, isDead, extraPacketData);

            if (damage > 0)
            {
                entity.HealingStats.AddPerSecondValue(timelineStart, extraPacketData.ArrivalTime, damage);
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
        /// <param name="carriesHp">
        /// 同じ同期の中で、被ダメログの技の行(加害者・バフ由来か・発生源の番号)ごとの最後の被弾か。HP・最大HP・バリアはこの被弾にだけ載せる。
        /// 同期で届く HP は被弾・回復を全部当てた後の1つだけなので、同じ同期の技の行には同じ値が載る。
        /// </param>
        /// <param name="healthBefore">その同期を当てる前の対象の HP・最大HP・バリア量。<paramref name="carriesHp"/> の被弾にだけ載せる。</param>
        public void AddTakenDamage(
            long attackerUuid, long targetUuid, long skillId, int ownerId, EDamageSource damageSource, int buffSourceSkillId, int summonSourceSkillId, int skillLevel, long damage, long hpLessen, long shieldBreak,
            EDamageProperty damageElement, EDamageType damageType, EDamageMode damageMode,
            bool isCrit, bool isLucky, bool isCauseLucky, bool isMiss, bool isDead, bool carriesHp, TargetHealth healthBefore, ExtraPacketData extraPacketData)
        {
            var arrivalUtc = extraPacketData.ArrivalTime;
            var isTargetPlayer = (EEntityType)Utils.UuidToEntityType(targetUuid) == EEntityType.EntChar;
            if (!IsRecordedInBenchmark(arrivalUtc, isTargetPlayer ? targetUuid : null))
            {
                return;
            }

            // 被弾も「次のイベント」。自傷(加害者の無いバフ・落下・自分の技が自分に当たった被弾)は AddDamage を通らないので、ここで解除する。
            EncounterHistoryProvider.NotifyLiveEncounterEvent();

            LastUpdate = arrivalUtc;

            var targetEntity = GetOrCreateEntity(targetUuid);

            // 行動時刻は被ダメログに載らない被弾でも対象に効かせる。起点より前は動かさない。
            if (IsAfterCombatClockStart(arrivalUtc))
            {
                targetEntity.RecordCombatAction(arrivalUtc);
            }

            if (!IsTakenDamageLogged(targetUuid, damage))
            {
                return;
            }

            // 被弾で時計を立てるのは起点の判定(MessageManager)だけ。起点より前の被弾(自傷・フレンドリーファイア・落下など)も
            // 被ダメログには載り、時刻は起点からの負の経過になる。記録すべき戦闘かは被ダメログの行数で決まる。

            // 加害者の実体を作っておく。被ダメログの加害者名はこの実体(AttrId)から引くので、
            // エンカウンターを作り直した直後に殴ってきた相手でも名前が引けるようにする。
            if (attackerUuid != 0)
            {
                GetOrCreateEntity(attackerUuid);
            }

            var snapshot = targetEntity.AddTakenDamage(attackerUuid, skillId, skillLevel, damage, hpLessen, shieldBreak, damageElement, damageType, damageMode, isCrit, isLucky, isCauseLucky, isMiss, isDead, extraPacketData,
                new SkillSnapshotStamp(
                    NextTakenDamageLogSequence(),
                    carriesHp ? targetEntity.GetAttrKV("AttrHp") as long? : null,
                    carriesHp ? targetEntity.GetAttrKV("AttrMaxHp") as long? : null,
                    carriesHp ? Utils.GetCurrentShield(targetEntity) : null,
                    carriesHp ? healthBefore.Hp : null,
                    carriesHp ? healthBefore.MaxHp : null,
                    carriesHp ? healthBefore.Shield : null,
                    ownerId,
                    damageSource,
                    buffSourceSkillId,
                    summonSourceSkillId));

            AppendTakenDamageLog(targetUuid, snapshot);
        }

        /// <summary>
        /// 被ダメログに載る被弾か。対象がプレイヤーで、値が 0 でないもの。加害者は問わない
        /// (自傷・落下・フレンドリーファイアも載せる)。値 0 は HP もバリアも減っていない被弾。
        /// </summary>
        internal static bool IsTakenDamageLogged(long targetUuid, long damage)
        {
            return damage != 0
                && (EEntityType)Utils.UuidToEntityType(targetUuid) == EEntityType.EntChar;
        }

        /// <summary>
        /// 被ダメログに載せる加害者・詠唱者を決める。召喚体は大元の召喚者へ寄せるが、
        /// <b>プレイヤーから見えて表に名前がある召喚体は、その召喚体自身</b>にする。
        ///
        /// <para>
        /// 見えるかは実体の種類で決める。モンスター(<c>MonsterTable</c>)は全行が見た目のモデルを持ち、
        /// 仮想体(<c>DummyTable</c>)はモデルを指す項目を持たない。弾なども見える実体ではない。
        /// </para>
        ///
        /// <para>
        /// 大元の召喚者がプレイヤーなら、召喚体の名前に関係なくプレイヤーを返す(フレンドリーファイアは打ったプレイヤーとして出す)。
        /// </para>
        /// </summary>
        internal long ResolveTakenDamageLogActor(long rawUuid, long topSummonerUuid)
        {
            if (topSummonerUuid == 0 || topSummonerUuid == rawUuid)
            {
                return rawUuid;
            }

            if ((EEntityType)Utils.UuidToEntityType(topSummonerUuid) == EEntityType.EntChar
                || (EEntityType)Utils.UuidToEntityType(rawUuid) != EEntityType.EntMonster)
            {
                return topSummonerUuid;
            }

            int monsterId;
            if (Entities.TryGetValue(rawUuid, out var summoned) && summoned.GetAttrKV("AttrId") is int attrId && attrId > 0)
            {
                monsterId = attrId;
            }
            else if (!Services.NearbyMonsterIndex.Instance.TryGetMonsterId(rawUuid, out monsterId))
            {
                Serilog.Log.Warning("Summon has no AttrId uuid={SummonUuid}. Cannot tell whether it has a name, so folding it into the root summoner {TopSummonerUuid}",
                    rawUuid, topSummonerUuid);
                return topSummonerUuid;
            }

            return CombatDataCatalog.HasMonsterName(monsterId) ? rawUuid : topSummonerUuid;
        }

        /// <summary>
        /// 技の開始を被ダメログの詠唱として残す。<b>詠唱バーを持つ技</b>と<b>戦闘画面の警告の技</b>だけで、
        /// プレイヤーとプレイヤーの召喚物は対象外。召喚物の扱いは <see cref="ResolveTakenDamageLogActor"/>。
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
                    Serilog.Log.Warning("Skill start without AttrSkillLevel uuid={EntityUuid} skillId={SkillId}. Cannot tell whether it is a warned skill, so not recording it",
                        entityUuid, skillId);
                    return;
                }

                if (!CombatDataCatalog.IsWarningSkill(skillId, skillLevel))
                {
                    return;
                }
            }

            var topSummonerUuid = GetOrCreateEntity(entityUuid).GetAttrKV("AttrTopSummonerId") is long summonerUuid
                ? summonerUuid
                : 0;
            var casterUuid = ResolveTakenDamageLogActor(entityUuid, topSummonerUuid);
            if ((EEntityType)Utils.UuidToEntityType(casterUuid) == EEntityType.EntChar)
            {
                return;
            }

            // 警告の通知は記録とは別に流す(計測の窓の外でも通知は止めない)。詠唱者の名前はこの回の実体から決める。
            if (CombatDataCatalog.IsWarningSkillId(skillId))
            {
                GetOrCreateEntity(casterUuid);
                Services.WarningSkillCastStore.Instance.Add(
                    MeterSnapshotProvider.CreateTakenDamageLogParty(this, casterUuid),
                    skillId,
                    extraPacketData.ArrivalTime);
            }

            if (!IsRecordedInBenchmark(extraPacketData.ArrivalTime, null))
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
            if (dbmId <= 0 || !IsRecordedInBenchmark(extraPacketData.ArrivalTime, null))
            {
                return;
            }

            var (skillId, ownerMonsterId) = ResolveAnnouncementSource(dbmId);

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

        /// <summary>
        /// 予告のバーが終わった時刻に、被ダメログへ「主の予告技」の行を残す。技と主はバーに控えた値(予告の行と同じ決め方)。
        /// 呼ぶのは <see cref="EncounterManager.RecordEndedAnnouncementBars"/> だけ。
        /// </summary>
        public void AddSkillAnnouncementBarEnd(Services.BossDbmBarStore.BossDbmBar bar)
        {
            if (!IsRecordedInBenchmark(bar.EndTimeUtc, null))
            {
                return;
            }

            var announcement = new SkillAnnouncementRecord
            {
                SkillId = bar.SkillId,
                OwnerMonsterId = bar.OwnerMonsterId,
                Timestamp = bar.EndTimeUtc,
                Sequence = NextTakenDamageLogSequence(),
                IsBarEnd = true,
            };

            lock (_takenDamageLogGate)
            {
                ExData.SkillAnnouncements.Add(announcement);
                _takenDamageLog.Add(TakenDamageLogRecord.ForAnnouncement(announcement));
            }
        }

        /// <summary>
        /// 予告の番号から技を引き、周囲にいるモンスターからその技を持つものの種別IDを引く。引けなければどちらも 0。
        /// 被ダメログの予告の行と予告のバーの控えが同じ決め方を使う。
        /// </summary>
        private static (int SkillId, int OwnerMonsterId) ResolveAnnouncementSource(int dbmId)
        {
            var ownerMonsterId = 0;
            if (CombatDataCatalog.TryResolveDbmSkillId(dbmId, out var skillId))
            {
                Services.NearbyMonsterIndex.Instance.TryFindMonsterId(
                    monsterId => CombatDataCatalog.MonsterHasSkill(monsterId, skillId),
                    out ownerMonsterId);
            }

            return (skillId, ownerMonsterId);
        }

        /// <summary>
        /// ボス大技の予告のバーを控える(<see cref="Services.BossDbmBarStore"/>)。決め方はゲームと同じ:
        /// 番号が無い通知は何もしない、番号・持続・insertion が全部 0 なら全部消す、
        /// バーの長さは持続の秒数(0 なら予告の表の <c>CountCDTime</c>)、表に無い番号と長さ 0 のバーは作らない。
        /// </summary>
        private static void RecordBossDbmBar(int intParamCount, int dbmId, int duration, int insertion, ExtraPacketData extraPacketData)
        {
            if (intParamCount == 0)
            {
                return;
            }

            if (intParamCount >= 3 && dbmId == 0 && duration == 0 && insertion == 0)
            {
                Services.BossDbmBarStore.Instance.Clear();
                return;
            }

            if (!CombatDataCatalog.TryGetDbmCountCdTime(dbmId, out var countCdTime))
            {
                Serilog.Log.Warning("Boss announcement {DbmId} is not in DbmTable. No announcement bar is timed for it", dbmId);
                return;
            }

            var seconds = duration != 0 ? duration : countCdTime;
            if (seconds <= 0)
            {
                Serilog.Log.Warning("Boss announcement {DbmId} has no duration. No announcement bar is timed for it", dbmId);
                return;
            }

            var (skillId, ownerMonsterId) = ResolveAnnouncementSource(dbmId);
            Services.BossDbmBarStore.Instance.Set(new Services.BossDbmBarStore.BossDbmBar(
                dbmId,
                skillId,
                ownerMonsterId,
                extraPacketData.ArrivalTime.AddSeconds(seconds)));
        }

        /// <summary>
        /// ダメージの無い死亡(死亡の印つきの被弾が無いまま死亡したもの)を被ダメログに残す。
        /// 死亡かどうか・印つきの被弾があったかは <c>MessageManager</c> が差分全体を見て決める。
        /// </summary>
        /// <param name="healthBefore">死亡を伝えた同期を当てる前の HP・最大HP・バリア量。</param>
        public void AddPlayerDeathWithoutDamage(long playerUuid, TargetHealth healthBefore, ExtraPacketData extraPacketData)
        {
            if (!IsRecordedInBenchmark(extraPacketData.ArrivalTime, playerUuid))
            {
                return;
            }

            // ダメージの無い死亡も「次のイベント」。AddDamage を通らないので、ここで解除する。
            EncounterHistoryProvider.NotifyLiveEncounterEvent();

            var death = new PlayerDeathRecord
            {
                PlayerUuid = playerUuid,
                MaxHp = GetOrCreateEntity(playerUuid).GetAttrKV("AttrMaxHp") as long?,
                Timestamp = extraPacketData.ArrivalTime,
                Sequence = NextTakenDamageLogSequence(),
                HpBefore = healthBefore.Hp,
                MaxHpBefore = healthBefore.MaxHp,
                ShieldBefore = healthBefore.Shield,
            };

            lock (_takenDamageLogGate)
            {
                ExData.PlayerDeaths.Add(death);
                _takenDamageLog.Add(TakenDamageLogRecord.ForDeath(death));
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
        /// 1回だけ、予告とダメージの無い死亡とプレイヤーの <c>TakenStats</c> と全エンティティの詠唱を通し番号順に並べ直す。
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

            foreach (var death in ExData.PlayerDeaths)
            {
                records.Add(TakenDamageLogRecord.ForDeath(death));
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
        /// <param name="payload">通知が運んだ中身。ライブ表示の項目を新しく作れるのは <see cref="BuffEventPayload.BuffInfo"/> だけ。</param>
        /// <param name="fightSourceType">付与元の種類。<c>null</c> は「この通知は運んでいない」で、記録済みの値を上書きしない。</param>
        public void NotifyBuffEvent(long entityUuid, EBuffEventType buffEventType, int buffUuid, int baseId, int level, long fireUuid, int layer, int duration, int sourceConfigId, DateTime? creationTime, ExtraPacketData extraPacketData, BuffEventPayload payload, int? fightSourceType)
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
                Payload = payload,
            });
            GetOrCreateEntity(entityUuid).NotifyBuffEvent(buffEventType, buffUuid, baseId, level, fireUuid, entityCasterName, layer, duration, sourceConfigId, creationTime, extraPacketData, payload, fightSourceType);

            // 強化する9特化は、特化アビリティ本体が付与するバフ(と、その実行時変種)で判定する。
            // 紐付け先は保持者ではなく術者(FireUuid)。味方に配られるバフは受け手が保持するので、
            // 保持者に付けると回復を受けた人まで同じ特化になる。
            //
            // 除去は見ない。「あることしか示さない」判定なので、消えても未装着の根拠にならない。
            if (buffEventType == EBuffEventType.BuffEventRemove)
            {
                ApplySpecMarkerRemoval(entityUuid, buffUuid);
                ApplySeasonTalentRemoval(entityUuid, buffUuid);
            }
            else if (baseId > 0)
            {
                ApplySpecFromTalentBuff(baseId, buffUuid, fireUuid, fightSourceType ?? -1, entityUuid, sourceConfigId);
                ApplySeasonTalentFromRootBuff(baseId, buffUuid, fireUuid);
            }
        }

        /// <summary>
        /// シーズンタレントの型の根ノードのバフから、<b>そのバフを張った本人</b>の有効化している型を確定する。
        /// 根以外のノードや因子のバフからは推さない(シーズンごとに木が違うことがあるため)。
        /// 帰属先は特化と同じく術者(<c>FireUuid</c>)。根ノードのバフは本人が本人に付ける。
        /// </summary>
        public void ApplySeasonTalentFromRootBuff(int baseId, int buffUuid, long fireUuid)
        {
            if (!CombatDataCatalog.IsSeasonTalentRootBuff(baseId))
            {
                return;
            }

            if (fireUuid == 0 || (EEntityType)Utils.UuidToEntityType(fireUuid) != EEntityType.EntChar)
            {
                return;
            }

            GetOrCreateEntity(fireUuid).UpdateSeasonTalentFromRootBuff(baseId, buffUuid);
        }

        /// <summary>
        /// シーズンタレントの型の根ノードのバフの除去を受けて、型を無効へ戻す。
        /// 除去イベントは <c>BaseId</c> を運ばないので、付与時に控えた実体UUIDとの一致で判定する。
        /// 保持者で突き合わせる(根ノードのバフは術者と保持者が同じ人)。
        /// </summary>
        private void ApplySeasonTalentRemoval(long entityUuid, int buffUuid)
        {
            if (entityUuid == 0 || buffUuid == 0)
            {
                return;
            }

            if (!Entities.TryGetValue(entityUuid, out var entity)
                || !entity.ClearSeasonTalentFromRootRemoval(buffUuid))
            {
                return;
            }

            var characterId = Utils.UuidToEntityId(entityUuid);
            Services.PartyMemberCache.Instance.SetSeasonTalentInactive(characterId);
            Services.MeterPlayerSpecCache.Instance.SetSeasonTalentInactive(characterId);
            PlayerRosterProjection.AddOrUpdateNearbyPlayer(entityUuid);
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
            //
            // 根の +1〜+9 の変種も、10刻みに丸めると根と同じ距離0になる。変種は死亡や時間切れで
            // 消えるので、控えるとその除去で特化を落としてしまう。控えるのは観測IDが根そのものの時だけ。
            // 変種は下の書き込みで特化の判定には使う(丸めは外さない)。
            var isMarker = distanceFromRoot == 0 && observedBuffId == grantedBuffId;
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
        /// <summary>被ダメログのダメージの無い死亡(<see cref="Encounter.AddPlayerDeathWithoutDamage"/>)。予告と同じくここに持つ。</summary>
        [ProtoMember(9)]
        public List<PlayerDeathRecord> PlayerDeaths { get; set; } = [];

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

        /// <summary>
        /// 有効化しているシーズンタレントの型の、根ノードのバフID。0 なら未観測か無効。
        /// 型の判定は根ノードのバフだけで行う(根以外のノードや因子からは推さない)。
        /// </summary>
        public int SeasonTalentBuffId { get; set; }

        /// <summary>上の根ノードのバフの実体UUID。除去イベントは BaseId を運ばないため、これで突き合わせる。</summary>
        public int SeasonTalentBuffUuid { get; set; }

        /// <summary>
        /// シーズンタレントについて、全バフスナップショットを受信済みか。立て方と落とし方は
        /// <see cref="HasBuffSnapshot"/> と同じだが、記録では特化の未装着(Rank1)とは別に焼き付くので分けて持つ。
        /// </summary>
        public bool HasSeasonTalentSnapshot { get; set; }

        /// <summary>
        /// NPC(助っ人)か。判定はパーティの社交データ(<c>BotAiId</c>)だけが持っていて実体には届かないので、
        /// 作り直しの前に表示している値を焼き付ける(<see cref="Entity.ApplyDisplayedIdentityForRecord"/>)。
        /// 履歴では社交データが無く、これが唯一の根拠になる。
        /// </summary>
        public bool IsNpc { get; set; }

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

        public int SeasonTalentBuffId { get => _identity.SeasonTalentBuffId; private set => _identity.SeasonTalentBuffId = value; }
        public int SeasonTalentBuffUuid { get => _identity.SeasonTalentBuffUuid; private set => _identity.SeasonTalentBuffUuid = value; }
        public bool HasSeasonTalentSnapshot { get => _identity.HasSeasonTalentSnapshot; private set => _identity.HasSeasonTalentSnapshot = value; }

        /// <summary>
        /// シーズンタレントの型が無効(どの型も有効化していない)と確定できる状態か。
        /// 全バフスナップショットを受け取った上で、そこに根ノードのバフが無かったときだけ true。
        /// 特化の <see cref="IsSpecAbilityUnequipped"/> と同じ作り。
        /// </summary>
        public bool IsSeasonTalentInactive =>
            ProfessionId > 0 && HasSeasonTalentSnapshot && SeasonTalentBuffId == 0;

        /// <summary>
        /// NPC(助っ人)か。判定の出所はパーティの社交データだけで、実体には届かない。
        /// 作り直しの前に焼き付けて次の回へ持ち越す。社交データが切れた回と履歴ではここだけが根拠。
        /// </summary>
        public bool IsNpc { get => _identity.IsNpc; private set => _identity.IsNpc = value; }

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

        /// <summary>行動と行動の間のうち、<see cref="InactiveGapSeconds"/> を超えた分の合計(秒)。有効DPS の分母から除く。</summary>
        public double TotalInactiveTime { get; set; } = 0.0;

        /// <summary>行動の間がこれを超えた分を非行動として除く(秒)。</summary>
        private const double InactiveGapSeconds = 10.0;

        /// <summary>この人の最初の行動(与ダメ・回復・被弾)の到着時刻。起点より前は動かさない。</summary>
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
        /// <b>記録する回に対して、作り直しの前にだけ呼ぶ。いま画面に出している素性をそのまま記録へ焼き付ける。</b>
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
        /// シーズンタレントも同じで、無効を記録するには <paramref name="seasonTalentInactive"/> を立てて根ノードのバフを0にする。
        /// </para>
        /// </summary>
        public void ApplyDisplayedIdentityForRecord(
            string name,
            int professionId,
            int subProfessionId,
            bool hasBuffSnapshot,
            int seasonTalentBuffId,
            bool seasonTalentInactive,
            int abilityScore,
            int level,
            long seasonLevel,
            long seasonStrength,
            long maxHp,
            bool isNpc)
        {
            // NPC かどうかはパーティの社交データ(BotAiId)だけが持っていて、実体には届かない。
            // 履歴では社交データが無くなるので、表示に出している値と一緒にここで焼き付ける。
            IsNpc = isNpc;

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
            SeasonTalentBuffId = seasonTalentBuffId;
            HasSeasonTalentSnapshot = seasonTalentInactive;

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

        /// <summary>
        /// シーズンタレントの型を「観測できていない」に戻す。根ノードのバフとスナップショット受信済みの印も落とす
        /// (<see cref="SetSubProfessionUnknown"/> と同じ理由)。
        /// </summary>
        public void SetSeasonTalentUnknown()
        {
            SeasonTalentBuffId = 0;
            SeasonTalentBuffUuid = 0;
            HasSeasonTalentSnapshot = false;
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

        /// <param name="countsTowardStats">発動の数に入れるか。決めるのは <see cref="Encounter.RegisterSkillActivation"/>(起点の後・計測の窓)。</param>
        public void RegisterSkillActivation(int skillId, DateTime activationUtc, bool countsTowardStats)
        {
            // 特化判定はここに置く。詠唱の AttrSkillId 経由でも AddDamage 経由でも必ず通るため、
            // 空振り(当たらなかった一撃)でも特定できる。記録の門とは関係なく判定する。
            UpdateSubProfessionFromReplacedSkill(skillId);

            if (countsTowardStats)
            {
                CountSkillActivation(skillId);
            }

            OnSkillActivated(new SkillActivatedEventArgs { CasterUuid = UUID, SkillId = skillId, ActivationDateTime = activationUtc });
        }

        private void CountSkillActivation(int skillId)
        {
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

                combatStats.AddData(otherUuid, skillId, skillLevel, value, isCrit, isLucky, hpLessenValue, shieldBreak, isCauseLucky, damageElement, damageType, damageMode, isDead, extraPacketData, default);

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

                combatStats.AddData(otherUuid, skillId, skillLevel, value, isCrit, isLucky, hpLessenValue, shieldBreak, isCauseLucky, damageElement, damageType, damageMode, isDead, extraPacketData, default);
            }
        }

        /// <summary>
        /// 行動(与ダメ・回復・被弾)を1回受けて行動時刻を進める。前の行動から <see cref="InactiveGapSeconds"/> を超えた分を非行動に積む。
        /// 呼ぶのは <see cref="Encounter"/> の記録の入口だけで、起点の後の出来事に限る。
        /// </summary>
        internal void RecordCombatAction(DateTime arrivalUtc)
        {
            if (LastCombatActionTime is { } last)
            {
                var idleSeconds = (arrivalUtc - last).TotalSeconds;
                if (idleSeconds > InactiveGapSeconds)
                {
                    TotalInactiveTime += idleSeconds - InactiveGapSeconds;
                }
            }

            FirstCombatActionTime ??= arrivalUtc;
            LastCombatActionTime = arrivalUtc;
        }

        /// <summary>
        /// 有効な秒数(有効DPS の分母)。この人の最初の行動から時計の終点までのうち、行動の間が <see cref="InactiveGapSeconds"/> を
        /// 超えた分(最後の行動から終点までを含む)を除いた長さ。終点は戦闘の時計にそろえる(ライブは今、閉じた回は終了時刻、計測は窓の終わり)。
        /// </summary>
        public double GetActiveSeconds(CombatClockReading clock)
        {
            if (clock.EndUtc is not { } end
                || FirstCombatActionTime is not { } first
                || end <= first)
            {
                return 0d;
            }

            var openIdleSeconds = LastCombatActionTime is { } last
                ? Math.Max((end - last).TotalSeconds - InactiveGapSeconds, 0d)
                : 0d;
            return Math.Max((end - first).TotalSeconds - TotalInactiveTime - openIdleSeconds, 0d);
        }

        public void AddDamage(long targetUuid, long skillId, int skillLevel, long damage, long hpLessen, long shieldBreak,
            EDamageProperty damageElement, EDamageType damageType, EDamageMode damageMode,
            bool isCrit, bool isLucky, bool isCauseLucky, bool isMiss, bool isDead, ExtraPacketData extraPacketData)
        {
            if (damageType != EDamageType.Immune)
            {
                TotalDamage += (ulong)damage;
            }

            if (damageType == EDamageType.Absorbed)
            {
                TotalShieldBreak += (ulong)shieldBreak;
            }

            DamageStats.AddData(targetUuid, skillId, skillLevel, damage, isCrit, isLucky, hpLessen, shieldBreak, isCauseLucky, damageElement, damageType, damageMode, isDead, extraPacketData, default);

            RegisterSkillData(ESkillType.Damage, targetUuid, skillId, skillLevel, damage, isCrit, isLucky, hpLessen, shieldBreak, isCauseLucky, damageElement, damageType, damageMode, isDead, extraPacketData);
        }

        public void AddHealing(
            long targetUuid, long skillId, int skillLevel, long damage, long overhealing, long effectiveHealing, long hpLessen, long shieldBreak,
            EDamageProperty damageElement, EDamageType damageType, EDamageMode damageMode,
            bool isCrit, bool isLucky, bool isCauseLucky, bool isMiss, bool isDead, ExtraPacketData extraPacketData)
        {
            TotalHealing += (ulong)damage;
            TotalOverhealing += (ulong)overhealing;

            HealingStats.AddData(targetUuid, skillId, skillLevel, damage, isCrit, isLucky, hpLessen, shieldBreak, isCauseLucky, damageElement, damageType, damageMode, isDead, extraPacketData, default);

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
        /// シーズンタレントの型の根ノードのバフから、有効化している型を確定する。
        /// <b>このエンティティが術者であること</b>は呼び出し側(<see cref="EncounterManager.ApplySeasonTalentFromRootBuff"/>)が保証する。
        /// 同じ型の再付与でも実体UUIDを控え直すので、シーン切替の再付与にも追従する。
        /// </summary>
        public void UpdateSeasonTalentFromRootBuff(int rootBuffId, int rootBuffUuid)
        {
            if (rootBuffId <= 0)
            {
                return;
            }

            if (rootBuffUuid != 0)
            {
                SeasonTalentBuffUuid = rootBuffUuid;
            }

            if (SeasonTalentBuffId == rootBuffId)
            {
                return;
            }

            SeasonTalentBuffId = rootBuffId;
            Services.PartyMemberCache.Instance.SetSeasonTalent(Utils.UuidToEntityId(UUID), rootBuffId);
            PlayerRosterProjection.AddOrUpdateNearbyPlayer(UUID);
        }

        /// <summary>
        /// 控えてある根ノードのバフが除去されたら、型を無効へ戻す。一致しなければ何もしないで <c>false</c>。
        /// <see cref="SetSeasonTalentUnknown"/> は使えない(受信済みの印まで落とし、無効ではなく「観測できていない」になる)。
        /// </summary>
        public bool ClearSeasonTalentFromRootRemoval(int buffUuid)
        {
            if (buffUuid == 0 || SeasonTalentBuffUuid != buffUuid)
            {
                return false;
            }

            SeasonTalentBuffId = 0;
            SeasonTalentBuffUuid = 0;
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
            // シーズンタレントの型も同じスナップショットで決める(無効が言えるのもこの経路だけ)。
            HasSeasonTalentSnapshot = true;
            var manager = EncounterManager.Current;
            for (var index = 0; index < buffs.Count; index++)
            {
                manager?.ApplySpecFromTalentBuff(
                    buffs[index].BaseId, buffs[index].BuffUuid, buffs[index].FireUuid, buffs[index].FightSourceType,
                    UUID, buffs[index].SourceConfigId);
                manager?.ApplySeasonTalentFromRootBuff(buffs[index].BaseId, buffs[index].BuffUuid, buffs[index].FireUuid);
            }

            // 自分が術者のタレントバフが1件も無かった＝未装着。PTメンバーぶんはキャッシュにも残す。
            if (SubProfessionId == 0 && ProfessionId > 0)
            {
                Services.PartyMemberCache.Instance.SetSpecAbilityUnequipped(Utils.UuidToEntityId(UUID));
                PlayerRosterProjection.AddOrUpdateNearbyPlayer(UUID);
            }

            // 根ノードのバフが1件も無かった＝シーズンタレントの型が無効。
            if (SeasonTalentBuffId == 0 && ProfessionId > 0)
            {
                Services.PartyMemberCache.Instance.SetSeasonTalentInactive(Utils.UuidToEntityId(UUID));
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
            // シーズンタレントの印も同じ時点で立てる(特化と同じ扱い)。
            HasSeasonTalentSnapshot = true;

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
            return TakenStats.AddData(attackerUuid, skillId, skillLevel, damage, isCrit, isLucky, hpLessen, shieldBreak, isCauseLucky, damageElement, damageType, damageMode, isDead, extraPacketData, stamp);
        }

        /// <param name="carriesBuffInfo">
        /// このイベントが <c>BuffInfo</c>(または <c>BuffChange</c>)を伴っていたか。
        /// <c>false</c> のとき渡ってくる 0 は「送られてこなかった」という意味なので、
        /// 既に知っているバフの持続・層・付与時刻を上書きしない。
        /// </param>
        public void NotifyBuffEvent(EBuffEventType buffEventType, int buffUuid, int baseId, int level, long fireUuid, string entityCasterName, int layer, int duration, int sourceConfigId, DateTime? creationTime, ExtraPacketData extraPacketData, BuffEventPayload payload, int? fightSourceType)
        {
            var carriesBuffInfo = payload != BuffEventPayload.None;
            if (buffEventType == EBuffEventType.BuffEventRemove)
            {
                if (!BuffEvents.TryGetValue((ulong)buffUuid, out var buffEvent))
                {

                    buffEvent = new BuffEvent(buffUuid);
                }
                buffEvent.SetRemoveTime(extraPacketData.ArrivalTime);

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
                    buffEvent = new BuffEvent(buffUuid, baseId, level, fireUuid, entityCasterName, layer, duration, sourceConfigId, fightSourceType ?? BuffEvent.UnknownFightSourceType);
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
                            buffEvent.SetAddTime(creationTime.Value);
                        }
                        else
                        {
                            buffEvent.SetAddTime(extraPacketData.ArrivalTime);
                        }
                    }
                    else
                    {
                        buffEvent.SetAddTime(extraPacketData.ArrivalTime);
                    }
                }

                BuffEvents[(ulong)buffUuid] = buffEvent;
                AddRecentBuffEventHistory(buffUuid, buffEvent);

                // ライブ表示用の状態はエンカウンター境界を跨いで保持する。
                // creationTime はサーバが名乗る付与時刻。ストア側が「もう何秒経ったか」を
                // 差し引くのに使う(AOI出現で受け取る、見る前から乗っているバフのため)。
                Services.ActiveBuffStore.Instance.AddOrUpdate(
                    UUID, (ulong)buffUuid, buffEvent, creationTime, createsInstance: payload == BuffEventPayload.BuffInfo);
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

        /// <summary>発動が届いたメッセージの到着時刻(UTC)。</summary>
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

        /// <summary>通知が届いたメッセージの到着時刻(UTC)。</summary>
        public DateTime UpdateDateTime { get; set; }
        public DateTime? CreationDateTime { get; set; }

        /// <summary>
        /// 通知が運んだ中身。<c>None</c> の回の <see cref="BaseId"/> 以下の 0 は「送られてこなかった」で、バフの状態ではない。
        /// </summary>
        public BuffEventPayload Payload { get; set; }
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

        /// <summary>
        /// 見出し表で名前を持たない行の着地先の種類(<see cref="Services.SourceLandingResolver"/>)。
        /// 最初に着いた先で決め、上書きしない。履歴でも同じ名前を出すので保存する。
        /// </summary>
        public SourceLandingKind LandingKind { get; set; }

        /// <summary>着地先のID。<see cref="LandingKind"/> がオプションならバフID、技なら技ID。</summary>
        public int LandingId { get; set; }

        [Newtonsoft.Json.JsonIgnore]
        public SourceLanding Landing => new(LandingKind, LandingId);
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

        public ulong ValueTotal { get; private set; }
        public ulong ValueNormalTotal { get; private set; }
        public ulong ValueCritTotal { get; private set; }
        public ulong ValueLuckyTotal { get; private set; }
        public ulong ValueCritLuckyTotal { get; private set; }
        public ulong ValueImmuneTotal { get; private set; }
        public long ValueMax { get; private set; }
        public long ValueMin { get; private set; }
        public double ValueAverage { get; private set; }

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
        /// DPS/HPS推移グラフの材料。キーは <see cref="EncounterExData.FirstDamageTimeStamp"/> からの
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

        /// <summary>
        /// 属性ごとの累計。スキル詳細の属性列(割合)の材料。
        ///
        /// <para>
        /// 足す条件は <see cref="ValueTotal"/> と同じで、<c>Immune</c> と <c>Miss</c> は足さない。
        /// したがって全部の合計は <see cref="ValueTotal"/> に一致し、行の中で割合を足すと 100% になる。
        /// </para>
        /// </summary>
        public Dictionary<EDamageProperty, ulong> ValueTotalByElement { get; private set; } = new();

        /// <summary>
        /// 物理・魔法ごとの累計。足す条件は <see cref="ValueTotalByElement"/> と同じ。
        /// <c>DamageNormal</c> は「物理でも魔法でもない」で、表示では「無分類」に当たる。
        /// </summary>
        public Dictionary<EDamageMode, ulong> ValueTotalByMode { get; private set; } = new();

        private readonly object _valueBreakdownGate = new();

        public bool ShouldSerializeValueTotalByElement() => ValueTotalByElement.Count > 0;

        public bool ShouldSerializeValueTotalByMode() => ValueTotalByMode.Count > 0;

        public void AddValueBreakdown(EDamageProperty damageElement, EDamageMode damageMode, long value)
        {
            lock (_valueBreakdownGate)
            {
                ValueTotalByElement[damageElement] =
                    ValueTotalByElement.GetValueOrDefault(damageElement) + (ulong)value;
                ValueTotalByMode[damageMode] =
                    ValueTotalByMode.GetValueOrDefault(damageMode) + (ulong)value;
            }
        }

        public KeyValuePair<EDamageProperty, ulong>[] GetValueTotalByElementCopy()
        {
            lock (_valueBreakdownGate)
            {
                return ValueTotalByElement.ToArray();
            }
        }

        public KeyValuePair<EDamageMode, ulong>[] GetValueTotalByModeCopy()
        {
            lock (_valueBreakdownGate)
            {
                return ValueTotalByMode.ToArray();
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

        /// <summary>
        /// 1件を足す。持つのは合計と回数だけで、秒間値は持たない(読むときに戦闘の時計で割る。<see cref="Encounter.ReadCombatClock()"/>)。
        /// </summary>
        public SkillSnapshot? AddData(long otherUuid, long skillId, int level, long value, bool isCrit, bool isLucky, long hpLessenValue, long shieldBreak, bool isCauseLucky, EDamageProperty damageElement, EDamageType damageType, EDamageMode damageMode, bool isDead, ExtraPacketData extraPacketData, SkillSnapshotStamp stamp)
        {
            DateTime now = extraPacketData.ArrivalTime;

            Id = skillId;
            Level = level;

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

                // 内訳は ValueTotal と同じ門の内側で足す。合計すると ValueTotal に一致する。
                AddValueBreakdown(damageElement, damageMode, value);

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
            // 率は小数点以下2位まで持つ。表示側は F2 で固定2桁にする。
            CritRate = HitsCount > 0 ? Math.Round(((double)CritCount / (double)HitsCount) * 100.0, 2) : 0.0;
            LuckyRate = HitsCount > 0 && HitsCount >= LuckyHitCount ? Math.Round(((double)LuckyHitCount / Math.Clamp((double)(HitsCount - LuckyHitCount), 1, double.MaxValue)) * 100.0, 2) : 0.0;

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
                TargetShield = stamp.TargetShield,
                TargetHpBefore = stamp.TargetHpBefore,
                TargetMaxHpBefore = stamp.TargetMaxHpBefore,
                TargetShieldBefore = stamp.TargetShieldBefore,
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

        /// <summary>属性。<c>null</c> は属性を保存していなかった頃の記録で、無属性とは別物。</summary>
        public EDamageProperty? DamageElement { get; set; }
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

        /// <summary>イベント後の対象のバリア量。被ダメログのまとめの行に HP と一緒に出す。</summary>
        public long? TargetShield { get; set; }

        /// <summary>
        /// その同期を当てる前の対象の HP・最大HP・バリア量。被ダメログのまとめの行の矢印の前に出す。
        /// <see cref="TargetHp"/> と同じ被弾にだけ入る。前の値を持っていなければ null。
        /// </summary>
        public long? TargetHpBefore { get; set; }

        public long? TargetMaxHpBefore { get; set; }

        public long? TargetShieldBefore { get; set; }

        public object Clone()
        {
            return this.MemberwiseClone();
        }
    }

    /// <summary>バフの通知が運んだ中身。</summary>
    public enum BuffEventPayload
    {
        /// <summary>中身なし。持続・層・付与時刻を知らされていない。</summary>
        None,

        /// <summary>付与(<c>BuffInfo</c>)。ライブ表示の項目を新しく作れるのはこれだけ。</summary>
        BuffInfo,

        /// <summary>層・持続の変化(<c>BuffChange</c>)。既にある項目を更新するだけ。</summary>
        BuffChange,
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
        /// 中身付きの付与を一度も受け取っていない実体は <see cref="UnknownFightSourceType"/>。
        /// </summary>
        public int FightSourceType { get; private set; }

        /// <summary>付与元の種類をまだ知らない。<c>EFightSource</c> に負の値は無い。</summary>
        public const int UnknownFightSourceType = -1;
        public string Name { get; private set; } = null!;
        [JsonIgnore]
        public string Description { get; private set; } = "";
        public string Icon { get; private set; } = null!;
        public int BuffAbilityType { get; private set; }
        public int BuffAbilitySubType { get; private set; }
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

        public void SetAddTime(DateTime dateTime)
        {
            AddDateTime = dateTime;
        }

        public void SetRemoveTime(DateTime dateTime)
        {
            RemoveDateTime = dateTime;
        }

        public void SetEntitySourceNameFromUuid(string name)
        {

        }

        public void SetDescription(string value)
        {
            Description = value;
        }

        /// <param name="fightSourceType">
        /// <c>null</c> は通知が種類を運んでいない(層の変化の通知など)。記録済みの種類を 0(技)で上書きしない。
        /// </param>
        public void SetEvent(int uuid, int baseId, int level, long fireUuid, string entityCasterName, int layer, int duration, int sourceConfigId, int? fightSourceType)
        {
            if (fightSourceType is { } providedFightSourceType)
            {
                FightSourceType = providedFightSourceType;
            }
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
