using StarResonanceDps.Core.CombatRuntime.Protocols;
using Serilog;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using static Zproto.WorldNtfCsharp.Types;
using Zproto;
using Google.Protobuf.Collections;
using System.Numerics;
using StarResonanceDps.Core.CombatRuntime.DataTypes;
using StarResonanceDps.Core.Services;
using System.Collections.Concurrent;
using ZLinq;

namespace StarResonanceDps.Core.CombatRuntime
{
    public static class MessageManager
    {
        private const double OriginEnergyRawScale = 100d;
        private const string CurrentStaminaSnapshotAttribute = "CurrentStaminaSnapshot";
        private const string MaxStaminaSnapshotAttribute = "MaxStaminaSnapshot";
        private const uint WorldProxyServiceId = 103198054;
        private const uint GetTeamInfoMethodId = 0x4C01F;

        public static NetCap? netCap = null;
        public static string NetCaptureDeviceName = "";
        public static EGameCapturePreference GameCapturePreference = EGameCapturePreference.Auto;
        public static string GameCaptureCustomExeName = "";

        public static void InitializeCapturing()
        {
            if (NetCaptureDeviceName == null)
            {
                throw new InvalidOperationException();
            }

            GrpcTeamManager.ResetMemberState();

            netCap = new NetCap();
            netCap.Init(new NetCapConfig()
            {
                CaptureDeviceName = NetCaptureDeviceName,
                ExeNames = Utils.GameCapturePreferenceToExeNames(GameCapturePreference, GameCaptureCustomExeName)
            });

            netCap.RegisterWorldNotifyHandler(StarResonanceDps.Core.CombatRuntime.Protocols.ServiceMethods.WorldNtf.EnterScene, ProcessEnterScene);

            netCap.RegisterWorldNotifyHandler(StarResonanceDps.Core.CombatRuntime.Protocols.ServiceMethods.WorldNtf.SyncContainerData, ProcessSyncContainerData);
            netCap.RegisterWorldNotifyHandler(StarResonanceDps.Core.CombatRuntime.Protocols.ServiceMethods.WorldNtf.SyncContainerDirtyData, ProcessSyncContainerDirtyData);

            netCap.RegisterWorldNotifyHandler(StarResonanceDps.Core.CombatRuntime.Protocols.ServiceMethods.WorldNtf.SyncNearDeltaInfo, ProcessSyncNearDeltaInfo);

            netCap.RegisterWorldNotifyHandler(StarResonanceDps.Core.CombatRuntime.Protocols.ServiceMethods.WorldNtf.SyncServerTime, ProcessSyncServerTime);
            netCap.RegisterWorldNotifyHandler(StarResonanceDps.Core.CombatRuntime.Protocols.ServiceMethods.WorldNtf.SyncToMeDeltaInfo, ProcessSyncToMeDeltaInfo);

            netCap.RegisterWorldNotifyHandler(StarResonanceDps.Core.CombatRuntime.Protocols.ServiceMethods.WorldNtf.SyncNearEntities, ProcessSyncNearEntities);

            // 計測専用。これまでハンドラが無く中身を一度も見ていない通知。
            netCap.RegisterWorldNotifyHandler(StarResonanceDps.Core.CombatRuntime.Protocols.ServiceMethods.WorldNtf.NotifyBuffChange, ProcessNotifyBuffChange);

            netCap.RegisterWorldNotifyHandler(StarResonanceDps.Core.CombatRuntime.Protocols.ServiceMethods.WorldNtf.SyncSceneEvents, ProcessSyncSceneEvents);

            netCap.RegisterNotifyHandler(936649811, (uint)StarResonanceDps.Core.CombatRuntime.Protocols.ServiceMethods.WorldActivityNtf.SyncHitInfo, ProcessSyncHitInfo);

            netCap.RegisterWorldNotifyHandler(StarResonanceDps.Core.CombatRuntime.Protocols.ServiceMethods.WorldNtf.SyncDungeonData, ProcessSyncDungeonData);

            netCap.RegisterWorldNotifyHandler(StarResonanceDps.Core.CombatRuntime.Protocols.ServiceMethods.WorldNtf.SyncDungeonDirtyData, ProcessSyncDungeonDirtyData);

            netCap.RegisterNotifyHandler((ulong)EServiceId.GrpcTeamNtf, (uint)StarResonanceDps.Core.CombatRuntime.Protocols.ServiceMethods.GrpcTeamNtf.NoticeUpdateTeamInfo, ProcessNoticeUpdateTeamInfo);
            netCap.RegisterNotifyHandler((ulong)EServiceId.GrpcTeamNtf, (uint)StarResonanceDps.Core.CombatRuntime.Protocols.ServiceMethods.GrpcTeamNtf.NoticeUpdateTeamMemberInfo, ProcessNoticeUpdateTeamMemberInfo);
            netCap.RegisterNotifyHandler((ulong)EServiceId.GrpcTeamNtf, (uint)StarResonanceDps.Core.CombatRuntime.Protocols.ServiceMethods.GrpcTeamNtf.NotifyJoinTeam, ProcessNotifyJoinTeam);
            netCap.RegisterNotifyHandler((ulong)EServiceId.GrpcTeamNtf, (uint)StarResonanceDps.Core.CombatRuntime.Protocols.ServiceMethods.GrpcTeamNtf.NotifyLeaveTeam, ProcessNotifyLeaveTeam);
            netCap.RegisterNotifyHandler((ulong)EServiceId.GrpcTeamNtf, (uint)StarResonanceDps.Core.CombatRuntime.Protocols.ServiceMethods.GrpcTeamNtf.NotifyTeamGroupUpdate, ProcessNotifyTeamGroupUpdate);

            netCap.RegisterNotifyHandler((ulong)EServiceId.GrpcTeamNtf, (uint)StarResonanceDps.Core.CombatRuntime.Protocols.ServiceMethods.GrpcTeamNtf.NotifyBeTransferLeader, ProcessNotifyBeTransferLeader);
            netCap.RegisterNotifyHandler((ulong)EServiceId.GrpcTeamNtf, (uint)StarResonanceDps.Core.CombatRuntime.Protocols.ServiceMethods.GrpcTeamNtf.NoticeTeamDissolve, ProcessNoticeTeamDissolve);
            netCap.RegisterNotifyHandler((ulong)EServiceId.GrpcTeamNtf, (uint)StarResonanceDps.Core.CombatRuntime.Protocols.ServiceMethods.GrpcTeamNtf.NotifyTeamActivityState, ProcessNotifyTeamActivityState);
            netCap.RegisterNotifyHandler((ulong)EServiceId.GrpcTeamNtf, (uint)StarResonanceDps.Core.CombatRuntime.Protocols.ServiceMethods.GrpcTeamNtf.TeamActivityResult, ProcessTeamActivityResult);
            netCap.RegisterNotifyHandler((ulong)EServiceId.GrpcTeamNtf, (uint)StarResonanceDps.Core.CombatRuntime.Protocols.ServiceMethods.GrpcTeamNtf.TeamActivityListResult, ProcessTeamActivityListResult);
            netCap.RegisterNotifyHandler((ulong)EServiceId.GrpcTeamNtf, (uint)StarResonanceDps.Core.CombatRuntime.Protocols.ServiceMethods.GrpcTeamNtf.TeamActivityVoteResult, ProcessTeamActivityVoteResult);
            netCap.RegisterNotifyHandler((ulong)EServiceId.GrpcTeamNtf, (uint)StarResonanceDps.Core.CombatRuntime.Protocols.ServiceMethods.GrpcTeamNtf.NotifyCharMatchResult, ProcessNotifyCharMatchResult);
            netCap.RegisterNotifyHandler((ulong)EServiceId.GrpcTeamNtf, (uint)StarResonanceDps.Core.CombatRuntime.Protocols.ServiceMethods.GrpcTeamNtf.NotifyTeamMatchResult, ProcessNotifyTeamMatchResult);
            netCap.RegisterNotifyHandler((ulong)EServiceId.GrpcTeamNtf, (uint)StarResonanceDps.Core.CombatRuntime.Protocols.ServiceMethods.GrpcTeamNtf.NotifyCharAbortMatch, ProcessNotifyCharAbortMatch);
            netCap.RegisterNotifyHandler((ulong)EServiceId.GrpcTeamNtf, (uint)StarResonanceDps.Core.CombatRuntime.Protocols.ServiceMethods.GrpcTeamNtf.UpdateTeamMemBeCall, ProcessUpdateTeamMemBeCall);
            netCap.RegisterNotifyHandler((ulong)EServiceId.GrpcTeamNtf, (uint)StarResonanceDps.Core.CombatRuntime.Protocols.ServiceMethods.GrpcTeamNtf.NotifyTeamMemBeCall, ProcessNotifyTeamMemBeCall);
            netCap.RegisterNotifyHandler((ulong)EServiceId.GrpcTeamNtf, (uint)StarResonanceDps.Core.CombatRuntime.Protocols.ServiceMethods.GrpcTeamNtf.NotifyTeamMemBeCallResult, ProcessNotifyTeamMemBeCallResult);
            netCap.RegisterNotifyHandler((ulong)EServiceId.GrpcTeamNtf, (uint)StarResonanceDps.Core.CombatRuntime.Protocols.ServiceMethods.GrpcTeamNtf.NotifyTeamEnterErr, ProcessNotifyTeamEnterErr);

            netCap.RegisterNotifyHandler((ulong)EServiceId.WorldActNtf, (uint)StarResonanceDps.Core.CombatRuntime.Protocols.ServiceMethods.WorldActNtf.SyncWorldActData, ProcessSyncWorldActData);

            netCap.RegisterNotifyHandler((ulong)EServiceId.SocialNtf, (uint)StarResonanceDps.Core.CombatRuntime.Protocols.ServiceMethods.SocialNtf.NotifySocialData, ProcessNotifySocialData);

            // ログイン画面へ戻ると、サーバーから GrpcCharactor(通知の service も GrpcCharactor)の ExitGame が通知で届く。
            netCap.RegisterNotifyHandler((ulong)EServiceId.GrpcCharactor, (uint)StarResonanceDps.Core.CombatRuntime.Protocols.ServiceMethods.GrpcCharactorNtf.ExitGame, ProcessExitGame);

            // 入り直し(キャラクター選択)。ログイン画面の門を開ける合図。
            netCap.RegisterProxyHandler((uint)EServiceId.GrpcCharactor, (uint)StarResonanceDps.Core.CombatRuntime.Protocols.ServiceMethods.GrpcCharactorNtf.SelectChar, ProcessSelectChar);

            netCap.NotifyGate = ShouldDispatchNotify;
            netCap.ProxyGate = ShouldDispatchProxy;

            netCap.RegisterProxyReturnHandler(WorldProxyServiceId, GetTeamInfoMethodId, ProcessGetTeamInfoReturn);

            netCap.Start();
            System.Diagnostics.Debug.WriteLine("MessageManager.InitializeCapturing : Capturing Started...");
        }

        public static void StopCapturing()
        {
            if (netCap != null)
            {
                netCap.Stop();
            }

            SkillCooldownStateStore.Reset();
            GrpcTeamManager.ResetMemberState();
            NearbyEntityStore.Instance.Clear();
            ActiveBuffStore.Instance.Clear();
            BuffInstanceIndex.Instance.Clear();
            SummonSourceIndex.Instance.Clear();
            SourceLandingResolver.Instance.Clear();
            NearbyMonsterIndex.Instance.Clear();
            PartyMemberCache.Instance.Clear();
        }

        public static SharpPcap.LibPcap.LibPcapLiveDevice? TryFindBestNetworkDevice()
        {
            var devices = SharpPcap.LibPcap.LibPcapLiveDeviceList.Instance;

            foreach (var device in devices)
            {
                if (device.Addresses.Count == 0)
                {
                    continue;
                }

                if (device.Interface?.GatewayAddresses.Count == 0)
                {
                    continue;
                }

                if (device.MacAddress == null)
                {
                    continue;
                }

                System.Diagnostics.Debug.WriteLine($"Best Network Device = {device.Description} -- {device.Name}");
                return device;
            }

            return null;
        }

        public static void ProcessEnterScene(ReadOnlySpan<byte> payloadBuffer, ExtraPacketData extraData)
        {
            if (payloadBuffer.Length == 0)
            {
                return;
            }

            var vData = EnterScene.Parser.ParseFrom(payloadBuffer);

            if (vData.EnterSceneInfo != null)
            {
                if (vData.EnterSceneInfo.PlayerEnt != null)
                {
                    NearbyEntityProjection.SetSelfEntity(vData.EnterSceneInfo.PlayerEnt.Uuid);

                    if (vData.EnterSceneInfo.PlayerEnt.Attrs != null)
                    {
                        ProcessAttrs(vData.EnterSceneInfo.PlayerEnt.Uuid, vData.EnterSceneInfo.PlayerEnt.Attrs.Attrs, isFullSnapshot: true);
                    }

                    if (vData.EnterSceneInfo.PlayerEnt.TempAttrs != null)
                    {
                        ProcessTempAttrs(vData.EnterSceneInfo.PlayerEnt.Uuid, vData.EnterSceneInfo.PlayerEnt.TempAttrs.Attrs);
                    }

                    PlayerRosterProjection.UpsertSelf(vData.EnterSceneInfo.PlayerEnt.Uuid);
                }

                if (vData.EnterSceneInfo.SceneAttrs != null)
                {
                    Log.Debug("ProcessEnterScene");

                    foreach (var attr in vData.EnterSceneInfo.SceneAttrs.Attrs)
                    {
                        var reader = new Google.Protobuf.CodedInputStream(attr.RawData.ToByteArray());

                        EAttrType attrId = (EAttrType)attr.Id;
                        string attrIdName = attrId.ToString();
                        bool isNoValue = attr.RawData.Length == 0;
                        switch (attrId)
                        {
                            case EAttrType.AttrSceneUuid:
                                Log.Debug($"\t{attrIdName} = {(isNoValue ? 0 : reader.ReadInt64())}");
                                break;
                            case EAttrType.AttrSceneBasicId:
                                Log.Debug($"\t{attrIdName} = {(isNoValue ? 0 : reader.ReadUInt32())}");
                                break;
                            case EAttrType.AttrSceneChannel:
                                Log.Debug($"\t{attrIdName} = {(isNoValue ? 0 : reader.ReadUInt32())}");
                                break;
                            default:
                                var val = isNoValue ? 0 : reader.ReadInt32();
                                Log.Debug($"\t{attrIdName} = {val}");
                                break;
                        }
                    }
                }
            }
        }

        public static void ProcessSyncWorldActData(ReadOnlySpan<byte> payloadBuffer, ExtraPacketData extraData)
        {
            if (payloadBuffer.Length == 0)
            {
                return;
            }

            var vData = WorldActActivityData.Parser.ParseFrom(payloadBuffer);

            System.Diagnostics.Debug.WriteLine("ProcessSyncWorldActData");
        }

        public static void ProcessSyncSceneEvents(ReadOnlySpan<byte> payloadBuffer, ExtraPacketData extraData)
        {
            if (payloadBuffer.Length == 0)
            {
                return;
            }

            var vData = SyncSceneEvents.Parser.ParseFrom(payloadBuffer);

            foreach (var evt in vData.Evt.Events)
            {
                EncounterManager.Current.AddSceneEvent(evt, extraData);
            }
        }

        /// <summary>
        /// NotifySocialData に同梱される「自分自身」のソーシャル情報を取り込む。
        ///
        /// <para>
        /// 名前・職業・レベル・戦闘力は本来フルコンテナ(SyncContainerData)で届くが、
        /// それはマップをロードした瞬間にしか流れないため、ロード済みマップの途中で
        /// アプリを起動すると永久に受け取れず、自分の行だけ空のままになる。
        /// 周辺プレイヤーは視界進入のたびに全属性が届くので埋まるため、自分だけ取り残される。
        /// </para>
        ///
        /// <para>
        /// この経路は途中起動でも定期的に届く。他プレイヤーに対して
        /// ApplyTeamMemberSocialData がやっているのと同じ内容を、自分にも適用する。
        /// </para>
        ///
        /// <para>
        /// 値が既にある場合は上書きしない。この経路の内容が古い可能性は否定できないため、
        /// 欠落を埋める用途に限定する。
        /// </para>
        /// </summary>
        private static void ApplySelfSocialData(SocialData? socialData)
        {
            if (socialData is null || socialData.CharId <= 0)
            {
                return;
            }

            if (AppState.PlayerUID != 0 && socialData.CharId != AppState.PlayerUID)
            {
                return;
            }

            var selfUuid = currentUserUuid != 0
                ? currentUserUuid
                : AppState.PlayerUUID != 0
                    ? AppState.PlayerUUID
                    : Utils.EntityIdToUuid(socialData.CharId, (long)EEntityType.EntChar, false, false);
            if (selfUuid == 0 || EncounterManager.Current is null)
            {
                return;
            }

            var entity = EncounterManager.Current.GetOrCreateEntity(selfUuid);
            var changed = false;

            if (string.IsNullOrEmpty(entity.Name)
                && !string.IsNullOrEmpty(socialData.BasicData?.Name))
            {
                entity.SetName(socialData.BasicData.Name);
                changed = true;
            }

            if (entity.ProfessionId == 0 && socialData.ProfessionData?.ProfessionId > 0 && !IsTransformed(entity.UUID))
            {
                entity.SetProfessionId(socialData.ProfessionData.ProfessionId);
                changed = true;
            }

            if (entity.Level == 0 && socialData.BasicData?.Level > 0)
            {
                entity.SetLevel(socialData.BasicData.Level);
                changed = true;
            }

            if (entity.SeasonLevel == 0 && socialData.BasicData?.SeasonLevel > 0)
            {
                entity.SetSeasonLevel(socialData.BasicData.SeasonLevel);
                changed = true;
            }

            if (entity.AbilityScore == 0 && socialData.UserAttrData?.FightPoint > 0)
            {
                entity.SetAbilityScore(ToInt32Saturating(socialData.UserAttrData.FightPoint));
                changed = true;
            }

            if (entity.SeasonStrength == 0 && socialData.UserAttrData?.SeasonStrength > 0)
            {
                entity.SetSeasonStrength(socialData.UserAttrData.SeasonStrength);
                changed = true;
            }

            if (changed)
            {
                PlayerRosterProjection.UpsertSelf(selfUuid);
            }
        }

        private static int ToInt32Saturating(long value)
        {
            return value > int.MaxValue ? int.MaxValue : (int)value;
        }

        /// <summary>
        /// ログイン画面にいる間は <c>true</c>。<c>ExitGame</c> で立て、入り直し(<c>SelectChar</c>)かフルコンテナで下ろす。
        ///
        /// <para>
        /// <b>この門が要る理由。</b> ログアウトの時点で受信の待ち行列に残っている AOI の差分が、
        /// 起動直後の状態へ戻した直後に処理されて、消したはずのプレイヤーリスト・実体を作り直してしまう
        /// (実測: 戻した4ミリ秒後に <c>SyncNearDeltaInfo</c> がプレイヤー51人・実体92体を作り直した)。
        /// </para>
        /// </summary>
        private static volatile bool _isLoggedOut;

        /// <summary>
        /// 通知を処理してよいか。ログイン画面にいる間は、ゲームの状態を変える通知を通さない。
        /// フルコンテナだけは通し、そこで門を開ける(入り直しの合図を取りこぼしたときの保険)。
        /// </summary>
        internal static bool ShouldDispatchNotify(ulong serviceUuid, uint methodId)
        {
            if (!_isLoggedOut)
            {
                return true;
            }

            if (serviceUuid == (ulong)EServiceId.WorldNtf
                && methodId == (uint)StarResonanceDps.Core.CombatRuntime.Protocols.ServiceMethods.WorldNtf.SyncContainerData)
            {
                OpenGateAfterLogin("フルコンテナ");
                return true;
            }

            return false;
        }

        /// <summary>
        /// 要求・応答を処理してよいか。ログイン画面にいる間は、入り直し(<c>SelectChar</c>)だけ通す。
        /// </summary>
        internal static bool ShouldDispatchProxy(uint serviceId, uint methodId)
        {
            return !_isLoggedOut
                || (serviceId == (uint)EServiceId.GrpcCharactor
                    && methodId == (uint)StarResonanceDps.Core.CombatRuntime.Protocols.ServiceMethods.GrpcCharactorNtf.SelectChar);
        }

        /// <summary>入り直し(キャラクター選択)。ここから先は新しいセッションなので門を開ける。</summary>
        public static void ProcessSelectChar(ReadOnlySpan<byte> payloadBuffer, uint returnUid, ExtraPacketData extraData)
        {
            OpenGateAfterLogin("SelectChar");
        }

        private static void OpenGateAfterLogin(string reason)
        {
            if (!_isLoggedOut)
            {
                return;
            }

            _isLoggedOut = false;
            Log.Information("ログイン画面の門を開けた({Reason})", reason);
        }

        /// <summary>
        /// ログアウトで起動直後の状態へ戻し終えた。パケット処理のスレッドで上がる。
        /// App の実体の窓はこれを受けて、捕まえている個体を放し、起動時と同じく種類と種別IDで捕まえ直す。
        /// </summary>
        public static event Action? ResetToStartupCompleted;

        /// <summary>
        /// ゲームがログイン画面へ戻った(サーバーから <c>GrpcCharactor</c> の <c>ExitGame</c> が通知で届く)。
        /// ログイン画面ではキャラクターを替えうるので、自分の素性も含めて<b>アプリ起動直後の状態へ戻す</b>。
        /// ログイン画面のシーン(シーン表の login)はサーバーから届かないので、この通知を合図にする。
        ///
        /// <para>
        /// 順序:
        /// 1. 3分計測中なら止める(計測したエンカウンターはいつもどおり保存される)
        /// 2. ダンジョンの状態とシーンを起動時の値に戻す(開いている記録には押さない)
        /// 3. battle 行を閉じて開き直し、エンカウンターをマップ移動と同じ手順で保存して、持ち越しなし(reason=None、起動時と同じ)で作り直す。
        ///    <b>プレイヤーリストはまだ残っている</b>ので、保存の直前の表示値の焼き付けが効く
        /// 4. 自分の素性と、接続中に積もった状態を起動時の値に戻す。キャプチャ(接続とデバイス)・設定・データ表・DB・計測は戻さない
        /// 5. 空のプレイヤーリストを作り直し、開いている履歴をライブに戻して、App に知らせる
        /// </para>
        /// </summary>
        public static void ProcessExitGame(ReadOnlySpan<byte> payloadBuffer, ExtraPacketData extraData)
        {
            Log.Information("ExitGame: ログイン画面へ戻った。起動直後の状態へ戻す");

            // 戻すより先に門を閉じる。待ち行列に残っている AOI の差分が、戻した直後に一覧を作り直すため。
            _isLoggedOut = true;

            try
            {
                ResetToStartupState();
            }
            catch (Exception ex)
            {
                Log.Error(ex, "ExitGame: 起動直後の状態へ戻す途中で例外");
                throw;
            }
        }

        private static void ResetToStartupState()
        {
            if (AppState.IsBenchmarkMode)
            {
                MeterSnapshotProvider.TryStopBenchmark();
            }

            BattleStateMachine.ResetDungeonStateToStartup();
            EncounterManager.ResetSceneToStartup();

            EncounterManager.StartNewMap();

            EncounterManager.EnterDungeon(true, EncounterStartReason.None);

            BattleStateMachine.ClearEncounterEndFinalData();

            currentUserUuid = 0;
            AppState.PlayerUUID = 0;
            AppState.PlayerUID = 0;
            AppState.AccountId = null!;
            AppState.PlayerName = null!;
            AppState.ProfessionId = 0;
            AppState.PlayerMeterPlacement = 0;
            AppState.PlayerTotalMeterValue = 0;
            AppState.PlayerMeterValuePerSecond = 0;
            AppState.BenchmarkTime = 0;
            AppState.BenchmarkSingleTargetUUID = 0;

            // どれもパケット処理のスレッドだけが触る。
            ShapeshiftedEntities.Clear();
            PlayerDeathStates.Clear();
            LastPassiveHealTicks.Clear();
            PlayerStateHistory.Clear();
            IsWipeCheckQueued = false;

            // キャプチャの停止(StopCapturing)で消しているもの。停止はしない。
            SkillCooldownStateStore.Reset();

            GrpcTeamManager.ResetMemberState();

            ActiveBuffStore.Instance.Clear();
            BuffInstanceIndex.Instance.Clear();
            SummonSourceIndex.Instance.Clear();
            SourceLandingResolver.Instance.Clear();
            NearbyMonsterIndex.Instance.Clear();

            PartyMemberCache.Instance.Clear();

            // マップ移動では残していたもの(自分の陣営・自分のスキル・プレイヤーリストの行など)。
            PlayerSkillLevelStateStore.ResetSelfToStartup();

            PlayerRosterProjection.ResetToStartup();

            NearbyEntityProjection.ResetToStartup();

            PlayerRosterProjection.RebuildRoster();

            if (AppState.OpenedHistoricalEncounter is not null)
            {
                EncounterHistoryProvider.SelectLive();
            }

            ResetToStartupCompleted?.Invoke();
        }

        public static void ProcessNotifySocialData(ReadOnlySpan<byte> payloadBuffer, ExtraPacketData extraData)
        {
            if (payloadBuffer.Length == 0)
            {
                return;
            }

            var vData = SocialNtf.Types.NotifySocialData.Parser.ParseFrom(payloadBuffer);

            if (vData != null)
            {
                ApplySelfSocialData(vData.VRequest?.Data);
                GrpcTeamManager.ProcessSocialTeamData(vData.VRequest?.Data);
            }

            if (vData?.VRequest?.Data?.SceneData is { } socialScene)
            {
                // AllowSceneUpdate は「同じシーンの繰り返し通知」を抑えるための関門。
                // NotifySocialData は social のスナップショットであってシーン通知ではなく、
                // 中身が食い違ったまま何度も飛んでくるため、無条件に流すと表示が暴れる。
                //
                // **別のマップ/チャンネルへ移った通知は抑制しない。** 関門を再武装するのは
                // フルコンテナ到着(StartNewMap)だけなので、フルコンテナが来ない切替
                // (ギルドハウス levelMapId=12000 など)では関門が閉じたままになり、
                // 移動通知を全部捨ててしまう。
                //
                // **LevelMapId == 0 は「まだシーン通知を一度も受け取っていない」状態。**
                // 0 → 実マップ は移動ではなく初回確定なので、ここを移動として扱わない。
                // 扱うと StartNewMap → EnterDungeon が走り、起動時のエンカウンターを
                // SceneId=0 / SceneName 空のまま保存してしまう。
                var isSceneChange = EncounterManager.LevelMapId != 0
                    && (socialScene.LevelMapId != EncounterManager.LevelMapId
                        || socialScene.LineId != EncounterManager.ChannelLineId);

                if (isSceneChange || EncounterManager.AllowSceneUpdate)
                {
                    ApplySceneData(
                        socialScene.LevelMapId,
                        socialScene.LineId,
                        socialScene.SceneGuid,
                        "NotifySocialData",
                        resetForSceneChange: isSceneChange);
                    EncounterManager.AllowSceneUpdate = false;
                }
            }
        }

        public static void ProcessNoticeUpdateTeamInfo(ReadOnlySpan<byte> payloadBuffer, ExtraPacketData extraData)
        {
            if (payloadBuffer.Length == 0)
            {
                return;
            }

            var vData = GrpcTeamNtf.Types.NoticeUpdateTeamInfo.Parser.ParseFrom(payloadBuffer);

            if (vData == null)
            {
                return;
            }

            GrpcTeamManager.ProcessNoticeUpdateTeamInfo(vData, extraData);
        }

        public static void ProcessNoticeUpdateTeamMemberInfo(ReadOnlySpan<byte> payloadBuffer, ExtraPacketData extraData)
        {
            if (payloadBuffer.Length == 0)
            {
                return;
            }

            var vData = GrpcTeamNtf.Types.NoticeUpdateTeamMemberInfo.Parser.ParseFrom(payloadBuffer);

            if (vData == null)
            {
                return;
            }

            GrpcTeamManager.ProcessNoticeUpdateTeamMemberInfo(vData, extraData);
        }

        public static void ProcessNotifyJoinTeam(ReadOnlySpan<byte> payloadBuffer, ExtraPacketData extraData)
        {
            if (payloadBuffer.Length == 0)
            {
                return;
            }

            var vData = GrpcTeamNtf.Types.NotifyJoinTeam.Parser.ParseFrom(payloadBuffer);

            if (vData == null)
            {
                return;
            }

            GrpcTeamManager.ProcessNotifyJoinTeam(vData, extraData);
        }

        public static void ProcessNotifyLeaveTeam(ReadOnlySpan<byte> payloadBuffer, ExtraPacketData extraData)
        {
            if (payloadBuffer.Length == 0)
            {
                return;
            }

            var vData = GrpcTeamNtf.Types.NotifyLeaveTeam.Parser.ParseFrom(payloadBuffer);

            if (vData == null)
            {
                return;
            }

            GrpcTeamManager.ProcessNotifyLeaveTeam(vData, extraData);
        }

        public static void ProcessNotifyTeamGroupUpdate(ReadOnlySpan<byte> payloadBuffer, ExtraPacketData extraData)
        {
            if (payloadBuffer.Length == 0)
            {
                return;
            }

            var vData = GrpcTeamNtf.Types.NotifyTeamGroupUpdate.Parser.ParseFrom(payloadBuffer);
            if (vData == null)
            {
                return;
            }

            GrpcTeamManager.ProcessNotifyTeamGroupUpdate(vData, extraData);
        }

        public static void ProcessNotifyBeTransferLeader(ReadOnlySpan<byte> payloadBuffer, ExtraPacketData extraData)
        {
            if (payloadBuffer.Length == 0)
            {
                return;
            }

            var vData = GrpcTeamNtf.Types.NotifyBeTransferLeader.Parser.ParseFrom(payloadBuffer);

            if (vData == null)
            {
                return;
            }

            GrpcTeamManager.ProcessNotifyBeTransferLeader(vData, extraData);
        }

        public static void ProcessNoticeTeamDissolve(ReadOnlySpan<byte> payloadBuffer, ExtraPacketData extraData)
        {
            if (payloadBuffer.Length == 0)
            {
                return;
            }

            var vData = GrpcTeamNtf.Types.NoticeTeamDissolve.Parser.ParseFrom(payloadBuffer);

            if (vData == null)
            {
                return;
            }

            GrpcTeamManager.ProcessNoticeTeamDissolve(vData, extraData);
        }

        public static void ProcessNotifyTeamActivityState(ReadOnlySpan<byte> payloadBuffer, ExtraPacketData extraData)
        {

            if (payloadBuffer.Length == 0)
            {
                return;
            }

            var vData = GrpcTeamNtf.Types.NotifyTeamActivityState.Parser.ParseFrom(payloadBuffer);

            if (vData == null)
            {
                return;
            }

            GrpcTeamManager.ProcessNotifyTeamActivityState(vData, extraData);
        }

        public static void ProcessTeamActivityResult(ReadOnlySpan<byte> payloadBuffer, ExtraPacketData extraData)
        {

            if (payloadBuffer.Length == 0)
            {
                return;
            }

            var vData = GrpcTeamNtf.Types.TeamActivityResult.Parser.ParseFrom(payloadBuffer);

            if (vData == null)
            {
                return;
            }

            GrpcTeamManager.ProcessTeamActivityResult(vData, extraData);
        }

        public static void ProcessTeamActivityListResult(ReadOnlySpan<byte> payloadBuffer, ExtraPacketData extraData)
        {

            if (payloadBuffer.Length == 0)
            {
                return;
            }

            var vData = GrpcTeamNtf.Types.TeamActivityListResult.Parser.ParseFrom(payloadBuffer);

            if (vData == null)
            {
                return;
            }

            GrpcTeamManager.ProcessTeamActivityListResult(vData, extraData);
        }

        public static void ProcessTeamActivityVoteResult(ReadOnlySpan<byte> payloadBuffer, ExtraPacketData extraData)
        {

            if (payloadBuffer.Length == 0)
            {
                return;
            }

            var vData = GrpcTeamNtf.Types.TeamActivityVoteResult.Parser.ParseFrom(payloadBuffer);

            if (vData == null)
            {
                return;
            }

            GrpcTeamManager.ProcessTeamActivityVoteResult(vData, extraData);
        }

        public static void ProcessNotifyCharMatchResult(ReadOnlySpan<byte> payloadBuffer, ExtraPacketData extraData)
        {
            System.Diagnostics.Debug.WriteLine("ProcessNotifyCharMatchResult");

            if (payloadBuffer.Length == 0)
            {
                return;
            }

            var vData = GrpcTeamNtf.Types.NotifyCharMatchResult.Parser.ParseFrom(payloadBuffer);

            if (vData == null)
            {
                return;
            }

            System.Diagnostics.Debug.WriteLine(vData);
        }

        public static void ProcessNotifyTeamMatchResult(ReadOnlySpan<byte> payloadBuffer, ExtraPacketData extraData)
        {
            System.Diagnostics.Debug.WriteLine("ProcessNotifyTeamMatchResult");

            if (payloadBuffer.Length == 0)
            {
                return;
            }

            var vData = GrpcTeamNtf.Types.NotifyTeamMatchResult.Parser.ParseFrom(payloadBuffer);

            if (vData == null)
            {
                return;
            }

            System.Diagnostics.Debug.WriteLine(vData);
        }

        public static void ProcessNotifyCharAbortMatch(ReadOnlySpan<byte> payloadBuffer, ExtraPacketData extraData)
        {
            System.Diagnostics.Debug.WriteLine("ProcessNotifyCharAbortMatch");

            if (payloadBuffer.Length == 0)
            {
                return;
            }

            var vData = GrpcTeamNtf.Types.NotifyCharAbortMatch.Parser.ParseFrom(payloadBuffer);

            if (vData == null)
            {
                return;
            }

            System.Diagnostics.Debug.WriteLine(vData);
        }

        public static void ProcessUpdateTeamMemBeCall(ReadOnlySpan<byte> payloadBuffer, ExtraPacketData extraData)
        {
            System.Diagnostics.Debug.WriteLine("ProcessUpdateTeamMemBeCall");

            if (payloadBuffer.Length == 0)
            {
                return;
            }

            var vData = GrpcTeamNtf.Types.UpdateTeamMemBeCall.Parser.ParseFrom(payloadBuffer);

            if (vData == null)
            {
                return;
            }

        }

        public static void ProcessNotifyTeamMemBeCall(ReadOnlySpan<byte> payloadBuffer, ExtraPacketData extraData)
        {
            System.Diagnostics.Debug.WriteLine("ProcessNotifyTeamMemBeCall");

            if (payloadBuffer.Length == 0)
            {
                return;
            }

            var vData = GrpcTeamNtf.Types.NotifyTeamMemBeCall.Parser.ParseFrom(payloadBuffer);

            if (vData == null)
            {
                return;
            }

        }

        public static void ProcessNotifyTeamMemBeCallResult(ReadOnlySpan<byte> payloadBuffer, ExtraPacketData extraData)
        {
            System.Diagnostics.Debug.WriteLine("ProcessNotifyTeamMemBeCallResult");

            if (payloadBuffer.Length == 0)
            {
                return;
            }

            var vData = GrpcTeamNtf.Types.NotifyTeamMemBeCallResult.Parser.ParseFrom(payloadBuffer);

            if (vData == null)
            {
                return;
            }

        }

        public static void ProcessNotifyTeamEnterErr(ReadOnlySpan<byte> payloadBuffer, ExtraPacketData extraData)
        {
            System.Diagnostics.Debug.WriteLine("ProcessNotifyTeamEnterErr");

            if (payloadBuffer.Length == 0)
            {
                return;
            }

            var vData = GrpcTeamNtf.Types.NotifyTeamEnterErr.Parser.ParseFrom(payloadBuffer);

            if (vData == null)
            {
                return;
            }

        }

        public static void ProcessSyncHitInfo(ReadOnlySpan<byte> payloadBuffer, ExtraPacketData extraData)
        {
            System.Diagnostics.Debug.WriteLine($"ProcessSyncHitInfo");
        }
        public static bool IsWipeCheckQueued = false;
        private static readonly HashSet<EAttrType> ShieldListChangedAttributes = [EAttrType.AttrShieldList];

        internal static bool IsSelfPlayer(long uuid)
        {
            return uuid == currentUserUuid
                || uuid == AppState.PlayerUUID
                || (AppState.PlayerUID != 0
                    && Utils.UuidToEntityId(uuid) == AppState.PlayerUID);
        }

        /// <summary>
        /// 変身中か。変身中は職業IDの属性(AttrProfessionId)だけが職業を決める。
        /// ソーシャル・コンテナ・ダメージの技の経路は変身前のクラスを運んでくるので、変身中は書かない。
        /// ミーンは変身のバフ、ドロシー・ルーシィ・ナツは変身クラスの職業IDで判断する。
        /// </summary>
        private static bool IsTransformed(long uuid)
        {
            if (Models.PlayerClassSpecResolver.HasMeanTransformBuff(uuid))
            {
                return true;
            }

            return EncounterManager.Current.Entities.TryGetValue(uuid, out var entity)
                && Models.PlayerClassSpecResolver.TryResolveTransformation(entity.ProfessionId, out _);
        }

        /// <summary>変身の属性(AttrShapeshiftType)が値ありで最後に届いた実体。</summary>
        private static readonly HashSet<long> ShapeshiftedEntities = [];

        /// <summary>
        /// この取り込みの時点で変身中か。1回の取り込みの中で、差し替えの値が変身の属性より先に並ぶことがあるので、
        /// 先に全体から変身の属性を探して決める。差分に変身の属性が無ければ前の状態のまま。
        /// </summary>
        private static bool ResolveShapeshifted(long uuid, RepeatedField<Attr> attrs, bool isFullSnapshot)
        {
            foreach (var attr in attrs)
            {
                if (attr.Id != (int)EAttrType.AttrShapeshiftType)
                {
                    continue;
                }

                var type = attr.RawData is { Length: > 0 }
                    ? new Google.Protobuf.CodedInputStream(attr.RawData.ToByteArray()).ReadInt32()
                    : 0;
                if (type != 0)
                {
                    ShapeshiftedEntities.Add(uuid);
                    return true;
                }

                ShapeshiftedEntities.Remove(uuid);
                return false;
            }

            if (isFullSnapshot)
            {
                ShapeshiftedEntities.Remove(uuid);
                return false;
            }

            return ShapeshiftedEntities.Contains(uuid);
        }

        /// <summary>
        /// 回血のバフ(<c>NameDesign</c> 回血)。薬を飲むと付き、同じ差分で HP だけが増える。回復の通知は来ない。
        /// 回復の薬はどれもこのバフを付けるので、どの薬かは分からない。
        /// </summary>
        private const int PotionHealBuffId = 2033000;

        /// <summary>
        /// 自然回復のバフ。683104(主城自动回血、シーン入場で付く)と 683105(坐木桩自动回血、切り株に座っている間)。
        /// 乗っている間だけ、戦闘とは関係なく約1秒ごとに最大HPの5%ずつ HP が増える。2つは同時に乗らない。
        /// </summary>
        private static readonly int[] RegenBuffIds = [683104, 683105];

        /// <summary>差分を当てる前のプレイヤーの状態・死亡時刻・HP・最大HP。</summary>
        private readonly record struct PlayerVitals(EActorState? State, long DeadTime, long? Hp, long? MaxHp);

        /// <summary>プレイヤーごとの、いまの死亡の扱い。復活で消す。</summary>
        private sealed class PlayerDeathState
        {
            /// <summary>死亡の印つきの被弾を受けた。</summary>
            public bool HasLethalHit;

            /// <summary>この死亡を扱い終えた(ダメージの無い死亡なら被ダメログに残した)。</summary>
            public bool IsDeathHandled;
        }

        private static readonly Dictionary<long, PlayerDeathState> PlayerDeathStates = [];

        /// <summary>料理・自然回復の刻みの種類。<see cref="LastPassiveHealTicks"/> に覚える。</summary>
        private enum PassiveHealTick
        {
            Food,
            Regen,
            Both
        }

        /// <summary>
        /// プレイヤーごとの直前の料理・自然回復の刻み。両方乗っているときの満タンの端数を、どちらの番か決めるのに使う。
        /// 端数を振り分けたときも、振り分けた種類で覚え直す。
        /// </summary>
        private static readonly Dictionary<long, PassiveHealTick> LastPassiveHealTicks = [];

        private static PlayerVitals CapturePlayerVitals(long uuid)
        {
            if (!EncounterManager.Current.Entities.TryGetValue(uuid, out var entity))
            {
                return new PlayerVitals(null, 0, null, null);
            }

            return new PlayerVitals(
                entity.GetAttrKV("AttrState") as EActorState?,
                entity.GetAttrKV("AttrDeadTime") is long deadTime ? deadTime : 0,
                entity.GetAttrKV("AttrHp") as long?,
                entity.GetAttrKV("AttrMaxHp") as long?);
        }

        private static PlayerDeathState GetPlayerDeathState(long uuid)
        {
            if (!PlayerDeathStates.TryGetValue(uuid, out var death))
            {
                death = new PlayerDeathState();
                PlayerDeathStates[uuid] = death;
            }

            return death;
        }

        /// <summary>
        /// 差分1つぶんの死亡の扱い。死亡は、状態が「死亡」に変わったか、死亡時刻が新しい値になったことで分かる。
        /// 状態の「死亡」は、死亡の印つきの被弾と同じ差分でいきなり復活(27)になる回に届かず、死亡時刻は遅れて届く。
        ///
        /// <para>
        /// 死亡の印つきの被弾が無いまま死亡したら、ダメージの無い死亡として被ダメログに1回だけ残す。
        /// 印つきの被弾がある死亡は被弾の行で分かるので何も足さない。死亡数は死亡時刻が新しい値になったときに数える。
        /// 状態が「死亡」から外れるか復活になったら、次の死亡に備えて消す。
        /// </para>
        /// </summary>
        private static void TrackPlayerDeath(long uuid, PlayerVitals before, HashSet<EAttrType> changedAttributes, ExtraPacketData extraData)
        {
            var entity = EncounterManager.Current.GetOrCreateEntity(uuid);
            var state = entity.GetAttrKV("AttrState") as EActorState?;
            var deadTime = entity.GetAttrKV("AttrDeadTime") is long value ? value : 0;
            var stateChanged = changedAttributes.Contains(EAttrType.AttrState);

            var becameDead = stateChanged
                && state == EActorState.ActorStateDead
                && before.State != EActorState.ActorStateDead;
            var hasNewDeadTime = changedAttributes.Contains(EAttrType.AttrDeadTime)
                && deadTime != 0
                && deadTime != before.DeadTime;

            if (becameDead || hasNewDeadTime)
            {
                var death = GetPlayerDeathState(uuid);
                if (!death.IsDeathHandled)
                {
                    death.IsDeathHandled = true;
                    if (!death.HasLethalHit)
                    {
                        EncounterManager.Current.AddPlayerDeathWithoutDamage(uuid, extraData);
                    }
                }
            }

            if (hasNewDeadTime)
            {
                EncounterManager.Current.RecordPlayerDeath(uuid);
            }

            var revived = stateChanged
                && ((before.State == EActorState.ActorStateDead && state != EActorState.ActorStateDead)
                    || state == EActorState.ActorStateResurrection);
            if (revived)
            {
                PlayerDeathStates.Remove(uuid);
            }
        }

        /// <summary>
        /// 薬の回復を HPS に足す。薬は回復の通知を出さず、回血のバフの付与と同じ差分で HP だけが増える。
        /// 増えた量を、飲んだ本人から本人への回復として回血のバフの鍵で足す。行の名前は見出し表に無いので空欄になる。
        ///
        /// <para>
        /// 同じ差分にダメージ・回復の通知があると、HP の増え方から薬のぶんだけを取り出せないので足さずに警告を出す。
        /// </para>
        /// </summary>
        private static void RecordPotionHeal(long uuid, long? hpBefore, HashSet<EAttrType> changedAttributes, AoiSyncDelta delta, ExtraPacketData extraData)
        {
            // HP が変わっていなければ(満タンで飲んだなど)回復量は 0。
            if (!changedAttributes.Contains(EAttrType.AttrHp))
            {
                return;
            }

            if (AppState.IsBenchmarkMode && uuid != AppState.PlayerUUID)
            {
                return;
            }

            if (hpBefore is null)
            {
                Serilog.Log.Warning("薬の回復: 差分の前の HP が無いので回復量を決められない uuid={Uuid}", uuid);
                return;
            }

            if (delta.SkillEffects?.Damages.Count > 0)
            {
                Serilog.Log.Warning("薬の回復: 同じ差分にダメージ・回復の通知 {Count} 件があり、薬のぶんを取り出せない uuid={Uuid}",
                    delta.SkillEffects.Damages.Count, uuid);
                return;
            }

            var hpAfter = EncounterManager.Current.GetOrCreateEntity(uuid).GetAttrKV("AttrHp") as long?;
            var healing = (hpAfter ?? 0) - hpBefore.Value;
            if (healing <= 0)
            {
                return;
            }

            AddBuffHealing(uuid, PotionHealBuffId, healing, extraData);
        }

        /// <summary>
        /// 料理と自然回復(<see cref="RegenBuffIds"/>)の回復を HPS に足す。どちらも回復の通知が無く、約1秒ごとに HP だけが増える。
        /// 1回の量は、料理は料理の表(<c>CookCuisineTable</c>)の値、自然回復は最大HPの5%(切り捨て)。
        /// 料理の表に回復量の無い料理は回復の通知で届くので、ここでは扱わない。
        ///
        /// <list type="bullet">
        ///   <item>増えが料理の1回ぶん → 料理</item>
        ///   <item>増えが最大HPの5%(自然回復のバフが乗っているとき) → 自然回復</item>
        ///   <item>増えが5%と料理の1回ぶんの合計 → 両方</item>
        ///   <item>
        ///     満タンになった端数 → 片方だけ乗っていればその片方。両方乗っていれば、刻みは料理 → 自然回復の順に来るので、
        ///     直前の刻み(<see cref="LastPassiveHealTicks"/>)が料理だけなら自然回復、それ以外なら料理
        ///     (料理の1回ぶんを超えた分は自然回復)
        ///   </item>
        ///   <item>最大HPが変わった差分(HP が割合のまま付け直される)・それ以外の増え → 数えない</item>
        /// </list>
        ///
        /// <para>
        /// 同じ差分に回復・ダメージの通知があると、HP の増えから料理・自然回復のぶんを取り出せないので数えない。
        /// 自然回復のバフは1回目の刻みと同じ差分で付くことがあるので、この差分で付いたバフも乗っているものとして見る。
        /// </para>
        /// </summary>
        private static void RecordPassiveHeal(long uuid, PlayerVitals before, HashSet<EAttrType> changedAttributes, AoiSyncDelta delta, IReadOnlyCollection<int> addedBuffIds, ExtraPacketData extraData)
        {
            if (!changedAttributes.Contains(EAttrType.AttrHp)
                || delta.SkillEffects?.Damages.Count > 0
                || before.Hp is not { } hpBefore
                || before.MaxHp is not { } maxHpBefore
                || before.State == EActorState.ActorStateDead
                || (AppState.IsBenchmarkMode && uuid != AppState.PlayerUUID))
            {
                return;
            }

            var entity = EncounterManager.Current.GetOrCreateEntity(uuid);
            if (entity.GetAttrKV("AttrHp") is not long hpAfter
                || entity.GetAttrKV("AttrMaxHp") is not long maxHp
                || maxHp != maxHpBefore)
            {
                return;
            }

            var increase = hpAfter - hpBefore;
            if (increase <= 0)
            {
                return;
            }

            var activeBuffIds = ActiveBuffStore.Instance.GetActive(uuid).Select(buff => buff.BaseId).Concat(addedBuffIds).ToHashSet();
            var regenBuffId = RegenBuffIds.FirstOrDefault(activeBuffIds.Contains);
            var regenAmount = regenBuffId != 0 ? maxHp * 5 / 100 : (long?)null;

            var regenAmounts = HelperMethods.DataTables.CookCuisines.RegenAmountsByBuffId;
            var foodBuffIds = activeBuffIds.Where(regenAmounts.ContainsKey).ToArray();
            if (foodBuffIds.Length > 1)
            {
                return;
            }

            var foodBuffId = foodBuffIds.Length == 1 ? foodBuffIds[0] : 0;
            var foodAmount = foodBuffId != 0 ? regenAmounts[foodBuffId] : (long?)null;
            var isFull = hpAfter == maxHp;
            PassiveHealTick? tick = null;

            if (foodAmount is { } food && increase == food)
            {
                AddBuffHealing(uuid, foodBuffId, food, extraData);
                tick = PassiveHealTick.Food;
            }
            else if (regenAmount is { } regen && Math.Abs(increase - regen) <= 1)
            {
                AddBuffHealing(uuid, regenBuffId, increase, extraData);
                tick = PassiveHealTick.Regen;
            }
            else if (regenAmount is { } regenPart && foodAmount is { } foodPart && Math.Abs(increase - (regenPart + foodPart)) <= 1)
            {
                AddBuffHealing(uuid, regenBuffId, increase - foodPart, extraData);
                AddBuffHealing(uuid, foodBuffId, foodPart, extraData);
                tick = PassiveHealTick.Both;
            }
            else if (isFull && regenAmount is { } regenTick && foodAmount is { } foodTick && increase < regenTick + foodTick)
            {
                if (LastPassiveHealTicks.TryGetValue(uuid, out var last) && last == PassiveHealTick.Food)
                {
                    if (increase < regenTick)
                    {
                        AddBuffHealing(uuid, regenBuffId, increase, extraData);
                        tick = PassiveHealTick.Regen;
                    }
                }
                else if (increase <= foodTick)
                {
                    AddBuffHealing(uuid, foodBuffId, increase, extraData);
                    tick = PassiveHealTick.Food;
                }
                else
                {
                    AddBuffHealing(uuid, foodBuffId, foodTick, extraData);
                    AddBuffHealing(uuid, regenBuffId, increase - foodTick, extraData);
                    tick = PassiveHealTick.Both;
                }
            }
            else if (isFull && regenAmount is { } regenOnly && foodAmount is null && increase < regenOnly)
            {
                AddBuffHealing(uuid, regenBuffId, increase, extraData);
                tick = PassiveHealTick.Regen;
            }
            else if (isFull && regenAmount is null && foodAmount is { } foodOnly && increase < foodOnly)
            {
                AddBuffHealing(uuid, foodBuffId, increase, extraData);
                tick = PassiveHealTick.Food;
            }

            if (tick is { } counted)
            {
                LastPassiveHealTicks[uuid] = counted;
            }
        }

        /// <summary>回復の通知の無い回復を、飲んだ・食べた・受けた本人から本人への回復として、そのバフの鍵で HPS に足す。行の名前は見出し表に無いので空欄になる。</summary>
        private static void AddBuffHealing(long uuid, int buffId, long healing, ExtraPacketData extraData)
        {
            var source = SkillSourceResolver.Resolve(EDamageSource.Buff, buffId, 0);
            EncounterManager.Current.AddHealing(uuid, uuid, source.Key, 0, healing, healing, 0, default, EDamageType.Heal, default, false, false, false, false, false, extraData);
            EncounterManager.Current.MarkBuffSourcedSkill(uuid, source.Key);
            PlayerRosterProjection.UpsertPlayer(uuid);
        }

        /// <summary>ダメージの値。通知の値が無ければ幸運の値、負なら HP の減り(無ければ 0)。</summary>
        private static long ResolveDamageValue(SyncDamageInfo damageInfo)
        {
            var damage = damageInfo.Value != 0
                ? damageInfo.Value
                : damageInfo.LuckyValue;

            if (damage < 0)
            {
                damage = damageInfo.HpLessenValue > 0 ? damageInfo.HpLessenValue : 0;
            }

            return damage;
        }

        /// <summary>
        /// 被弾した本人が原因の被弾か。落下(<c>OwnerId</c> は 0、加害者は本人)と、加害者も大元の召喚者も無いバフ等のダメージ
        /// (自分が付けたバフの自傷。付与の <c>FireUuid</c> は本人)。被ダメログには本人を加害者として載せ、メーターには入れない。
        /// </summary>
        private static bool IsSelfCausedDamage(SyncDamageInfo damageInfo)
        {
            return damageInfo.DamageSource == EDamageSource.Fall
                || (damageInfo.OwnerId != 0 && damageInfo.AttackerUuid == 0 && damageInfo.TopSummonerId == 0);
        }

        /// <summary>
        /// 本人が原因の被弾(<see cref="IsSelfCausedDamage"/>)を被ダメログにだけ残す。
        /// バフ由来なら、本人が付けたそのバフの付与元の技を控える(技の行の名前になる)。
        /// </summary>
        private static void AddSelfCausedTakenDamage(SyncDamageInfo damageInfo, long targetUuid, bool carriesHp, ExtraPacketData extraData)
        {
            var skillId = SkillSourceResolver.Resolve(damageInfo.DamageSource, damageInfo.OwnerId, damageInfo.HitEventId).Key;
            var damage = ResolveDamageValue(damageInfo);
            var shieldBreak = damageInfo.Type == EDamageType.Absorbed ? damage : 0;

            var buffSourceSkillId = 0;
            if (damageInfo.DamageSource == EDamageSource.Buff
                && BuffInstanceIndex.Instance.TryResolveSourceSkill(
                    damageInfo.OwnerId,
                    targetUuid,
                    targetUuid,
                    extraData.ArrivalTime,
                    out var resolvedBuffSourceSkillId,
                    out _))
            {
                buffSourceSkillId = resolvedBuffSourceSkillId;
            }

            EncounterManager.Current.AddTakenDamage(
                targetUuid,
                targetUuid,
                skillId,
                damageInfo.OwnerId,
                damageInfo.DamageSource,
                buffSourceSkillId,
                0,
                damageInfo.OwnerLevel,
                damage,
                damageInfo.HpLessenValue,
                shieldBreak,
                damageInfo.Property,
                damageInfo.Type,
                damageInfo.DamageMode,
                (damageInfo.TypeFlag & 1) == 1,
                damageInfo.LuckyValue != 0,
                (damageInfo.TypeFlag & 0B100) == 0B100,
                damageInfo.IsMiss,
                damageInfo.IsDead,
                carriesHp,
                extraData);
        }

        private static void UpdateProfessionId(long uuid, int professionId)
        {
            EncounterManager.Current.SetAttrKV(uuid, "AttrProfessionId", professionId);

            if (!IsSelfPlayer(uuid))
            {
                return;
            }

            PlayerSkillLevelStateStore.SetSelfCurrentProfessionId(professionId);
            AppState.ProfessionId = professionId;
            RefreshSelfRosterEntry(uuid);
        }

        /// <summary>
        /// 職業・タレント・スキルの更新後に自分の行を作り直す。
        ///
        /// <para>
        /// <b>ここで特化は決めない。</b> <c>ProfessionTalentInfo.TalentStageCfgId</c> は
        /// 「どのタレントツリーを選んでいるか」であって「特化アビリティを装着しているか」ではなく、
        /// 未装着でもツリーの値を返し続ける。特化は自分も他人と同じく特化マーカーバフだけで決める。
        /// </para>
        /// </summary>
        private static void RefreshSelfRosterEntry(long uuid)
        {
            if (uuid == 0)
            {
                return;
            }

            PlayerRosterProjection.UpsertSelf(uuid);
        }

        private static void ProcessGetTeamInfoReturn(
            ReadOnlySpan<byte> payloadBuffer,
            uint returnUid,
            ExtraPacketData extraData)
        {
            try
            {
                var response = GetTeamInfoReply.Parser.ParseFrom(payloadBuffer);
                GrpcTeamManager.ProcessGetTeamInfo(response, extraData);
            }
            catch (Exception ex)
            {
                Log.Debug(ex, "Failed to parse team information response");
            }
        }

        /// <summary>
        /// プレイヤー以外の実体に出した元(<c>AttrFightSourceInfo</c>)が届いたら、出した技を <see cref="SummonSourceIndex"/> に控える。
        /// 技ならその技、バフならこの瞬間に生きているバフの付与元の技。どちらでもないか引けなければ控えを消す。
        /// 属性を当てた後に呼ぶ(同じ束で届いた最上位の召喚者を使う)。
        /// </summary>
        private static void RecordSummonSource(long uuid, RepeatedField<Attr> attrs, DateTime arrivalTime)
        {
            if (Utils.UuidToEntityType(uuid) == (long)EEntityType.EntChar)
            {
                return;
            }

            foreach (var attr in attrs)
            {
                if ((EAttrType)attr.Id != EAttrType.AttrFightSourceInfo || attr.RawData == null)
                {
                    continue;
                }

                var sourceSkillId = 0;
                if (attr.RawData.Length > 0)
                {
                    var source = FightSourceInfo.Parser.ParseFrom(attr.RawData);
                    if (source.FightSourceType == (int)EFightSource.Skill)
                    {
                        sourceSkillId = source.SourceConfigId;
                    }
                    else if (source.FightSourceType == (int)EFightSource.Buff)
                    {
                        var topSummonerUuid = EncounterManager.Current.GetAttrKV(uuid, "AttrTopSummonerId") as long? ?? 0;
                        if (BuffInstanceIndex.Instance.TryResolveSourceSkill(source.SourceConfigId, uuid, topSummonerUuid, arrivalTime, out var buffSourceSkillId, out _))
                        {
                            sourceSkillId = buffSourceSkillId;
                        }
                    }
                }

                if (sourceSkillId > 0)
                {
                    var summonerUuid = EncounterManager.Current.GetAttrKV(uuid, "AttrSummonerId") as long? ?? 0;
                    SummonSourceIndex.Instance.Set(uuid, sourceSkillId, summonerUuid);
                }
                else
                {
                    SummonSourceIndex.Instance.Remove(uuid);
                }
            }
        }

        /// <summary>
        /// プレイヤー以外の実体の出どころ(付与元と召喚者)を <see cref="SourceLandingResolver"/> に控える。属性を当てた後に呼ぶ。
        /// 出現(<paramref name="isAppear"/>)で付与元が無ければ、同じ UUID の前の控えを消す。
        /// </summary>
        private static void RecordSourceLanding(long uuid, RepeatedField<Attr>? attrs, DateTime arrivalTime, bool isAppear)
        {
            if (Utils.UuidToEntityType(uuid) == (long)EEntityType.EntChar)
            {
                return;
            }

            FightSourceInfo? source = null;
            if (attrs != null)
            {
                foreach (var attr in attrs)
                {
                    if ((EAttrType)attr.Id == EAttrType.AttrFightSourceInfo && attr.RawData is { Length: > 0 })
                    {
                        source = FightSourceInfo.Parser.ParseFrom(attr.RawData);
                    }
                }
            }

            if (source == null && !isAppear)
            {
                return;
            }

            var summonerUuid = EncounterManager.Current.GetAttrKV(uuid, "AttrSummonerId") as long? ?? 0;
            var topSummonerUuid = EncounterManager.Current.GetAttrKV(uuid, "AttrTopSummonerId") as long? ?? 0;
            var attrId = EncounterManager.Current.GetAttrKV(uuid, "AttrId") is { } attrIdValue ? Convert.ToInt32(attrIdValue) : 0;
            SourceLandingResolver.Instance.RecordEntity(uuid, attrId, source, summonerUuid, topSummonerUuid, arrivalTime, isAppear);
        }

        /// <summary>
        /// モンスターの種別ID(<c>AttrId</c>)が届いたら <see cref="NearbyMonsterIndex"/> に入れる。属性を当てた後に呼ぶ。
        /// </summary>
        private static void RecordNearbyMonster(long uuid, RepeatedField<Attr> attrs)
        {
            if (Utils.UuidToEntityType(uuid) != (long)EEntityType.EntMonster
                || !attrs.Any(attr => (EAttrType)attr.Id == EAttrType.AttrId && attr.RawData != null))
            {
                return;
            }

            if (EncounterManager.Current.GetAttrKV(uuid, "AttrId") is int monsterId && monsterId > 0)
            {
                NearbyMonsterIndex.Instance.Set(uuid, monsterId);
            }
            else
            {
                NearbyMonsterIndex.Instance.Remove(uuid);
            }
        }

        /// <param name="isFullSnapshot">出現・EnterScene の全属性の写しか(変身の属性が無ければ変身していない)。</param>
        public static void ProcessAttrs(long uuid, RepeatedField<Attr> attrs, bool isFullSnapshot = false)
        {
            // 変身中はサーバーが能力スコア・シーズン強度・レベル・シーズンレベルを変身体の値に差し替えてくるので、変身前の値のまま持つ。
            var isShapeshifted = ResolveShapeshifted(uuid, attrs, isFullSnapshot);

            foreach (var attr in attrs)
            {
                if (attr.Id == 0 || attr.RawData == null)
                {
                    continue;
                }
                var reader = new Google.Protobuf.CodedInputStream(attr.RawData.ToByteArray());

                EAttrType attrId = (EAttrType)attr.Id;
                string attrIdName = attrId.ToString();
                bool isNoValue = attr.RawData.Length == 0;
                switch (attrId)
                {
                    case EAttrType.AttrName:
                        EncounterManager.Current.SetAttrKV(uuid, attrIdName, isNoValue ? "" : reader.ReadString().TrimEnd());
                        break;
                    case EAttrType.AttrHatedName:
                        EncounterManager.Current.SetAttrKV(uuid, attrIdName, isNoValue ? "" : reader.ReadString().TrimEnd());
                        break;
                    case EAttrType.AttrSkillId:
                        EncounterManager.Current.SetAttrKV(uuid, attrIdName, isNoValue ? 0 : reader.ReadInt32());
                        break;
                    case EAttrType.AttrCdAcceleratePct:
                        {
                            var accelerationPct = isNoValue ? 0 : reader.ReadInt32();
                            EncounterManager.Current.SetAttrKV(uuid, attrIdName, accelerationPct);
                            if (uuid == currentUserUuid || uuid == AppState.PlayerUUID)
                            {
                                SkillCooldownStateStore.UpdateSelfAcceleration(uuid, accelerationPct);
                            }
                            break;
                        }
                    case EAttrType.AttrProfessionId:
                        {
                            var incomingProfessionId = isNoValue ? 0 : reader.ReadInt32();
                            if (IsSelfPlayer(uuid))
                            {
                            }

                            UpdateProfessionId(uuid, incomingProfessionId);
                            break;
                        }
                    case EAttrType.AttrCamp:
                        {
                            var camp = isNoValue ? 0 : reader.ReadInt32();
                            EncounterManager.Current.SetAttrKV(uuid, attrIdName, camp);
                            NearbyEntityProjection.UpdateCamp(uuid, !isNoValue, camp);
                            break;
                        }
                    case EAttrType.AttrFightPoint:
                        if (!isShapeshifted)
                        {
                            EncounterManager.Current.SetAttrKV(uuid, attrIdName, isNoValue ? 0 : reader.ReadInt32());
                        }
                        break;
                    case EAttrType.AttrLevel:
                        if (!isShapeshifted)
                        {
                            EncounterManager.Current.SetAttrKV(uuid, attrIdName, isNoValue ? 0 : reader.ReadInt32());
                        }
                        break;
                    case EAttrType.AttrRankLevel:
                        EncounterManager.Current.SetAttrKV(uuid, attrIdName, isNoValue ? 0 : reader.ReadInt32());
                        break;
                    case EAttrType.AttrCri:
                        EncounterManager.Current.SetAttrKV(uuid, attrIdName, isNoValue ? 0 : reader.ReadInt32());
                        break;
                    case EAttrType.AttrLuck:
                        EncounterManager.Current.SetAttrKV(uuid, attrIdName, isNoValue ? 0 : reader.ReadInt32());
                        break;
                    case EAttrType.AttrHp:
                        EncounterManager.Current.SetAttrKV(uuid, attrIdName, isNoValue ? 0L : reader.ReadInt64());
                        break;
                    case EAttrType.AttrMaxHp:
                        EncounterManager.Current.SetAttrKV(uuid, attrIdName, isNoValue ? 0L : reader.ReadInt64());
                        break;
                    case EAttrType.AttrOriginEnergy:
                        {
                            var rawOriginEnergy = isNoValue ? 0 : reader.ReadInt32();
                            EncounterManager.Current.SetAttrKV(uuid, attrIdName, rawOriginEnergy);
                            EncounterManager.Current.SetAttrKV(
                                uuid,
                                CurrentStaminaSnapshotAttribute,
                                Math.Max(rawOriginEnergy / OriginEnergyRawScale, 0d));
                            break;
                        }
                    case EAttrType.AttrMaxOriginEnergy:
                        {
                            var rawMaxOriginEnergy = isNoValue ? 0 : reader.ReadInt32();
                            EncounterManager.Current.SetAttrKV(uuid, attrIdName, rawMaxOriginEnergy);
                            EncounterManager.Current.SetAttrKV(
                                uuid,
                                MaxStaminaSnapshotAttribute,
                                Math.Max(rawMaxOriginEnergy / OriginEnergyRawScale, 0d));
                            break;
                        }
                    case EAttrType.AttrMaxStunned:
                    case EAttrType.AttrStunned:
                        EncounterManager.Current.SetAttrKV(uuid, attrIdName, isNoValue ? 0 : reader.ReadInt32());
                        break;
                    // 攻撃力・防御力は物理も魔法も同じ形で届く。魔法側を default(int32)へ落とさず、まとめて int64 で読む。
                    case EAttrType.AttrAttack:
                    case EAttrType.AttrMattack:
                    case EAttrType.AttrDefense:
                    case EAttrType.AttrMdefense:
                        EncounterManager.Current.SetAttrKV(uuid, attrIdName, isNoValue ? 0L : reader.ReadInt64());
                        break;
                    case EAttrType.AttrPos:
                        EncounterManager.Current.SetAttrKV(uuid, attrIdName, isNoValue ? new Vec3() : Vec3.Parser.ParseFrom(reader));
                        break;
                    case EAttrType.AttrTargetPos:
                        EncounterManager.Current.SetAttrKV(uuid, attrIdName, isNoValue ? new Vec3() : Vec3.Parser.ParseFrom(reader));
                        break;
                    case EAttrType.AttrFinalTargetPos:
                        EncounterManager.Current.SetAttrKV(uuid, attrIdName, isNoValue ? new Vec3() : Vec3.Parser.ParseFrom(reader));
                        break;
                    case EAttrType.AttrDmgTargetPos:
                        EncounterManager.Current.SetAttrKV(uuid, attrIdName, isNoValue ? new Vec3() : Vec3.Parser.ParseFrom(reader));
                        break;
                    case EAttrType.AttrBulletTargetPos:
                        EncounterManager.Current.SetAttrKV(uuid, attrIdName, isNoValue ? new Vec3() : Vec3.Parser.ParseFrom(reader));
                        break;
                    case EAttrType.AttrSummonerPos:
                        EncounterManager.Current.SetAttrKV(uuid, attrIdName, isNoValue ? new Vec3() : Vec3.Parser.ParseFrom(reader));
                        break;
                    case EAttrType.AttrTargetPartPos:
                        EncounterManager.Current.SetAttrKV(uuid, attrIdName, isNoValue ? new Vec3() : Vec3.Parser.ParseFrom(reader));
                        break;
                    case EAttrType.AttrParadeLeaderPos:
                        EncounterManager.Current.SetAttrKV(uuid, attrIdName, isNoValue ? new Vec3() : Vec3.Parser.ParseFrom(reader));
                        break;
                    case EAttrType.AttrState:
                        EncounterManager.Current.SetAttrKV(uuid, "AttrState", isNoValue ? (EActorState)0 : (EActorState)reader.ReadInt32());

                        if (uuid == currentUserUuid)
                        {
                            IsWipeCheckQueued = true;
                        }

                        break;
                    case EAttrType.AttrShieldList:
                        {
                            if (isNoValue)
                            {
                                EncounterManager.Current.SetAttrKV(uuid, attrIdName, new List<ShieldInfo>());
                                break;
                            }

                            List<ShieldInfo> shieldList = new();
                            while (!reader.IsAtEnd)
                            {
                                int len = reader.ReadLength();

                                ShieldInfo shield = new();

                                reader.ReadMessage(shield);
                                shieldList.Add(shield);
                            }
                            EncounterManager.Current.SetAttrKV(uuid, "AttrShieldList", shieldList);
                            break;
                        }
                    case EAttrType.AttrActionTime:
                        EncounterManager.Current.SetAttrKV(uuid, attrIdName, isNoValue ? 0L : reader.ReadInt64());
                        break;
                    case EAttrType.AttrActionUpperTime:
                        EncounterManager.Current.SetAttrKV(uuid, attrIdName, isNoValue ? 0L : reader.ReadInt64());
                        break;
                    case EAttrType.AttrStiffTarget:
                        EncounterManager.Current.SetAttrKV(uuid, attrIdName, isNoValue ? 0L : reader.ReadInt64());
                        break;
                    case EAttrType.AttrStiffStageTime:
                        EncounterManager.Current.SetAttrKV(uuid, attrIdName, isNoValue ? 0L : reader.ReadInt64());
                        break;
                    case EAttrType.AttrSkillBeginTime:
                        EncounterManager.Current.SetAttrKV(uuid, attrIdName, isNoValue ? 0L : reader.ReadInt64());
                        break;
                    case EAttrType.AttrFirstAttack:
                        EncounterManager.Current.SetAttrKV(uuid, attrIdName, isNoValue ? 0L : reader.ReadInt64());
                        break;
                    case EAttrType.AttrCombatStateTime:
                        EncounterManager.Current.SetAttrKV(uuid, attrIdName, isNoValue ? 0L : reader.ReadInt64());
                        break;
                    case EAttrType.AttrTargetUuid:
                        EncounterManager.Current.SetAttrKV(uuid, attrIdName, isNoValue ? 0L : reader.ReadInt64());
                        break;
                    case EAttrType.AttrTargetId:
                        EncounterManager.Current.SetAttrKV(uuid, attrIdName, isNoValue ? 0L : reader.ReadInt64());
                        break;
                    case EAttrType.AttrSummonerId:
                        EncounterManager.Current.SetAttrKV(uuid, attrIdName, isNoValue ? 0L : reader.ReadInt64());
                        break;
                    case EAttrType.AttrTopSummonerId:
                        EncounterManager.Current.SetAttrKV(uuid, attrIdName, isNoValue ? 0L : reader.ReadInt64());
                        break;
                    case EAttrType.AttrHateList:
                        if (isNoValue)
                        {
                            EncounterManager.Current.SetAttrKV(uuid, attrIdName, new List<HateInfo>());
                            break;
                        }

                        List<HateInfo> hateList = new();
                        while (!reader.IsAtEnd)
                        {
                            int len = reader.ReadLength();

                            HateInfo hate = new();

                            reader.ReadMessage(hate);
                            hateList.Add(hate);
                        }
                        EncounterManager.Current.SetAttrKV(uuid, "AttrHateList", hateList);
                        break;
                    case EAttrType.AttrSlot:
                        {
                            var slotPayload = isNoValue ? [] : attr.RawData.ToByteArray();
                            var isSelfSlot = IsSelfPlayer(uuid);

                            // 自分のアクションバー。枠番号つきで全枠が届く唯一の経路。
                            // 他人には届かないので、自分のときだけ取り込む。
                            if (isSelfSlot && slotPayload.Length > 0)
                            {
                                var actionBar = Zproto.Slot.Parser.ParseFrom(slotPayload);
                                var slotMap = new Dictionary<int, int>(actionBar.Slots.Count);
                                foreach (var slotPair in actionBar.Slots)
                                {
                                    // 空枠は skillId=0 のまま入れる。落とすと枠の位置が失われる。
                                    slotMap[slotPair.Key] = slotPair.Value.SkillId;
                                }

                                PlayerSkillLevelStateStore.ReplaceSelfActionBarSlots(slotMap);
                            }

                            break;
                        }
                    case EAttrType.AttrSeasonLevel:
                        if (!isShapeshifted)
                        {
                            EncounterManager.Current.SetAttrKV(uuid, attrIdName, isNoValue ? 0 : reader.ReadInt32());
                        }
                        break;
                    case EAttrType.AttrSeasonStrength:
                        if (!isShapeshifted)
                        {
                            EncounterManager.Current.SetAttrKV(uuid, attrIdName, isNoValue ? 0 : reader.ReadInt32());
                        }
                        break;
                    case EAttrType.AttrSeasonStrengthAdd:
                        EncounterManager.Current.SetAttrKV(uuid, attrIdName, isNoValue ? 0 : reader.ReadInt32());
                        break;
                    case EAttrType.AttrSeasonStrengthTotal:
                        EncounterManager.Current.SetAttrKV(uuid, attrIdName, isNoValue ? 0 : reader.ReadInt32());
                        break;
                    case EAttrType.AttrSkillLevelIdList:
                        if (isNoValue)
                        {
                            var emptySkillLevels = new List<DataTypes.Skills.SkillLevelInfo>();
                            if (IsSelfPlayer(uuid))
                            {
                                PlayerSkillLevelStateStore.UpdateSelfRawSkillLevels(
                                    emptySkillLevels);
                            }

                            EncounterManager.Current.SetAttrKV(uuid, attrIdName, emptySkillLevels);
                            break;
                        }

                        List<DataTypes.Skills.SkillLevelInfo> skillLevelInfoList = new();
                        while (!reader.IsAtEnd)
                        {
                            int len = reader.ReadLength();

                            SkillLevelInfo info = new();

                            reader.ReadMessage(info);
                            skillLevelInfoList.Add(new DataTypes.Skills.SkillLevelInfo(info));
                        }
                        if (IsSelfPlayer(uuid))
                        {
                            PlayerSkillLevelStateStore.UpdateSelfRawSkillLevels(
                                skillLevelInfoList);
                        }

                        EncounterManager.Current.SetAttrKV(uuid, "AttrSkillLevelIdList", skillLevelInfoList);

                        // AOI同期でしか届かないので、相手がマップ外へ出ると取得できなくなる。
                        // 自分以外のパーティメンバーの分だけ補完用に保持する(判定はキャッシュ側)。
                        PartyMemberCache.Instance.SetSkillLevels(
                            Utils.UuidToEntityId(uuid),
                            skillLevelInfoList);
                        break;
                    case EAttrType.AttrTeamId:
                        var teamId = isNoValue ? 0L : reader.ReadInt64();
                        EncounterManager.Current.SetAttrKV(uuid, attrIdName, teamId);
                        GrpcTeamManager.ProcessEntityTeamId(uuid, teamId);
                        break;
                    case EAttrType.AttrStateTime:
                        EncounterManager.Current.SetAttrKV(uuid, attrIdName, isNoValue ? 0L : reader.ReadInt64());
                        break;
                    case EAttrType.AttrRideUuid:
                        EncounterManager.Current.SetAttrKV(uuid, attrIdName, isNoValue ? 0L : reader.ReadInt64());
                        break;
                    case EAttrType.AttrDeadTime:
                        EncounterManager.Current.SetAttrKV(uuid, attrIdName, isNoValue ? 0L : reader.ReadInt64());
                        break;
                    case EAttrType.AttrEquipData:
                        if (isNoValue)
                        {
                            EncounterManager.Current.SetAttrKV(uuid, attrIdName, new List<Zproto.EquipNine>());
                            break;
                        }

                        List<Zproto.EquipNine> equipNineList = new();
                        while (!reader.IsAtEnd)
                        {
                            int len = reader.ReadLength();

                            Zproto.EquipNine info = new();

                            reader.ReadMessage(info);
                            equipNineList.Add(info);
                        }
                        EncounterManager.Current.SetAttrKV(uuid, "AttrEquipData", equipNineList);
                        break;
                    default:
                        EncounterManager.Current.SetAttrKV(uuid, attrIdName, isNoValue ? 0 : reader.ReadInt32());
                        break;
                }
            }
        }

        public static void ProcessTempAttrs(long uuid, RepeatedField<Zproto.TempAttr> tempAttrs)
        {
            foreach (var tempAttr in tempAttrs)
            {
                if (HelperMethods.DataTables.TempAttrs.Data.TryGetValue(tempAttr.Id.ToString(), out var matchedTempAttr))
                {
                    EncounterManager.Current.SetTempAttrKV(uuid, tempAttr.Id, new TempAttributesContainer() { Id = tempAttr.Id, Value = tempAttr.Value, TempAttr = matchedTempAttr });
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine($"WARN: Unmatched TempAttr: UUID={uuid} Id={tempAttr.Id} Value={tempAttr.Value}");
                }
            }
        }

        public static void ProcessSyncNearEntities(ReadOnlySpan<byte> payloadBuffer, ExtraPacketData extraData)
        {
            var syncNearEntities = SyncNearEntities.Parser.ParseFrom(payloadBuffer);

            foreach (var disappearedEntity in syncNearEntities.Disappear)
            {
                PlayerRosterProjection.RemoveNearbyPlayer(disappearedEntity.Uuid);
                NearbyEntityProjection.RemoveEntity(disappearedEntity.Uuid);
                SummonSourceIndex.Instance.Remove(disappearedEntity.Uuid);
                NearbyMonsterIndex.Instance.Remove(disappearedEntity.Uuid);
                ShapeshiftedEntities.Remove(disappearedEntity.Uuid);
                PlayerDeathStates.Remove(disappearedEntity.Uuid);
                LastPassiveHealTicks.Remove(disappearedEntity.Uuid);
            }

            foreach (var entity in syncNearEntities.Appear)
            {
                long uid = Utils.UuidToEntityId(entity.Uuid);

                if (uid == 0)
                {
                    continue;
                }

                EncounterManager.Current.SetEntityType(entity.Uuid, entity.EntType);

                if (entity.TempAttrs != null && entity.TempAttrs.Attrs.Any())
                {
                    ProcessTempAttrs(entity.Uuid, entity.TempAttrs.Attrs);
                }

                var attrCollection = entity.Attrs;
                if (attrCollection?.Attrs != null)
                {
                    ProcessAttrs(entity.Uuid, attrCollection.Attrs, isFullSnapshot: true);
                    RecordSummonSource(entity.Uuid, attrCollection.Attrs, extraData.ArrivalTime);
                    RecordNearbyMonster(entity.Uuid, attrCollection.Attrs);
                }

                RecordSourceLanding(entity.Uuid, attrCollection?.Attrs, extraData.ArrivalTime, isAppear: true);


                ApplyAppearBuffSnapshot(entity, extraData);

                PlayerRosterProjection.AddOrUpdateNearbyPlayer(entity.Uuid);
                NearbyEntityProjection.AddOrUpdateAppearedEntity(entity.Uuid);
            }

            if (IsWipeCheckQueued)
            {
                IsWipeCheckQueued = false;
                CheckForWipe();
            }

            BattleStateMachine.CheckDeferredCalls();
        }

        /// <summary>
        /// AOI出現メッセージが運ぶ全バフスナップショット(<c>Entity.BuffInfos</c>, field 7)を
        /// 特化判定へ渡す。
        ///
        /// <para>
        /// 差分(<c>AoiSyncDelta.BuffEffect</c>)は「見ている間に付いたバフ」しか運ばないので、
        /// 既に付いている特化マーカーバフは差分だけでは永久に届かない。
        /// このスナップショットが唯一「いま何を持っているか」を全部運ぶ経路で、
        /// 「マーカーが1つも無い = アビリティ未装着」を確定できるのもここだけ。
        /// </para>
        ///
        /// <para>
        /// <b>特化判定と表示の両方へ渡す。</b> 差分は「見ている間に付いたバフ」しか運ばないので、
        /// 流さないと<b>自分が見る前から乗っているバフが他人では一生表示されない</b>。
        /// 持続の長い料理(1800秒)・薬剤ほど当たりやすく、マップ切替で
        /// <c>ActiveBuffStore.Clear()</c> したあとも復活しない、という見え方になっていた。
        /// 自分は <c>SyncToMeDeltaInfo</c> がイベントとして全部送ってくるので影響を受けない。
        /// </para>
        ///
        /// <para>
        /// 残り時間の起点は <c>NotifyBuffEvent</c> 側の既存の2段に任せる —
        /// 付与時刻と受信時刻の選別と、同一実体(<c>BuffUuid</c>＋付与時刻＋持続が一致)なら
        /// 観測時刻を据え置く扱い。<b>後者があるので、出現のたびに再送されても
        /// 残り時間は満額へ巻き戻らない。</b>
        /// </para>
        /// </summary>
        private static void ApplyAppearBuffSnapshot(Zproto.Entity entity, ExtraPacketData extraData)
        {
            // 付与元の索引には敵も含めて入れる。見る前から乗っているバフはこの経路でしか届かない。
            if (entity.BuffInfos?.BuffInfos is { } appearBuffInfos)
            {
                foreach (var appearBuffInfo in appearBuffInfos)
                {
                    BuffInstanceIndex.Instance.Add(
                        entity.Uuid,
                        appearBuffInfo.BuffUuid,
                        appearBuffInfo.BaseId,
                        appearBuffInfo.FireUuid,
                        appearBuffInfo.FightSourceInfo?.FightSourceType ?? -1,
                        appearBuffInfo.FightSourceInfo?.SourceConfigId ?? 0,
                        appearBuffInfo.Duration,
                        extraData.ArrivalTime);
                }
            }

            // 特化を持つのはプレイヤーだけ。モンスターまで通すと診断ログが埋まる。
            if (Utils.UuidToEntityType(entity.Uuid) != (long)EEntityType.EntChar)
            {
                return;
            }

            // 出現時の一覧は全件なので、一覧に無い項目は見えない間に消えたもの。
            // 一覧そのものが届いていない出現では、何が付いているか分からないので触らない。
            if (entity.BuffInfos is { } appearBuffList)
            {
                ActiveBuffStore.Instance.RemoveMissingFromSnapshot(
                    entity.Uuid,
                    appearBuffList.BuffInfos.Select(info => (ulong)info.BuffUuid).ToHashSet());
            }

            var buffInfos = entity.BuffInfos?.BuffInfos;
            if (buffInfos == null || buffInfos.Count == 0)
            {
                return;
            }

            // FireUuid(術者)と FightSourceType も運ぶ。どちらも特化判定の採用条件。
            // SourceConfigId は判定が食い違ったときに正体を割り出す唯一の手掛かりなので一緒に持つ。
            var snapshot = new List<(int BaseId, int BuffUuid, long FireUuid, int FightSourceType, int SourceConfigId)>(buffInfos.Count);
            for (var index = 0; index < buffInfos.Count; index++)
            {
                var buffInfo = buffInfos[index];
                if (buffInfo.BaseId <= 0)
                {
                    continue;
                }

                snapshot.Add((
                    buffInfo.BaseId,
                    buffInfo.BuffUuid,
                    buffInfo.FireUuid,
                    buffInfo.FightSourceInfo?.FightSourceType ?? 0,
                    buffInfo.FightSourceInfo?.SourceConfigId ?? 0));

                // 差分の BuffEffectAddBuff と同じ入口・同じ引数へ渡す。
                // CreateTime の変換も向こうと揃える(Unixミリ秒 → UTC)。
                DateTime? creationTime = null;
                if (buffInfo.CreateTime > 0)
                {
                    creationTime = DateTimeOffset.FromUnixTimeMilliseconds(buffInfo.CreateTime).UtcDateTime;
                }

                EncounterManager.Current.NotifyBuffEvent(
                    entity.Uuid,
                    EBuffEventType.BuffEventAddTo,
                    buffInfo.BuffUuid,
                    buffInfo.BaseId,
                    buffInfo.Level,
                    buffInfo.FireUuid,
                    buffInfo.Layer,
                    buffInfo.Duration,
                    buffInfo.FightSourceInfo?.SourceConfigId ?? 0,
                    creationTime,
                    extraData,
                    BuffEventPayload.BuffInfo,
                    buffInfo.FightSourceInfo?.FightSourceType);
            }

            if (snapshot.Count == 0)
            {
                return;
            }

            EncounterManager.Current.GetOrCreateEntity(entity.Uuid).ApplyBuffSnapshotForSpec(snapshot);
        }

        public static void ProcessSyncNearDeltaInfo(ReadOnlySpan<byte> payloadBuffer, ExtraPacketData extraData)
        {
            var syncNearDeltaInfo = SyncNearDeltaInfo.Parser.ParseFrom(payloadBuffer);
            if (syncNearDeltaInfo.DeltaInfos == null || syncNearDeltaInfo.DeltaInfos.Count == 0)
            {
                return;
            }

            foreach (var aoiSyncDelta in syncNearDeltaInfo.DeltaInfos)
            {
                ProcessAoiSyncDelta(aoiSyncDelta, extraData);
            }

            if (IsWipeCheckQueued)
            {
                IsWipeCheckQueued = false;
                CheckForWipe();
            }

            BattleStateMachine.CheckDeferredCalls();
        }

        public static void ProcessAoiSyncDelta(AoiSyncDelta delta, ExtraPacketData extraData)
        {
            if (delta == null)
            {
                return;
            }

            long targetUuid = delta.Uuid;
            if (targetUuid == 0)
            {
                return;
            }

            BattleStateMachine.CompleteBenchmarkIfElapsed(DateTime.Now);

            bool isTargetPlayer = (Utils.UuidToEntityType(targetUuid) == (long)EEntityType.EntChar);
            long targetUid = Utils.UuidToEntityId(targetUuid);
            var attrCollection = delta.Attrs;
            HashSet<EAttrType> changedAttributes = [];

            // 差分の中は 属性 → バフ → ダメージ の順に当てるので、死亡の印つきの被弾は属性より先に控える。
            // 同じ差分で状態が「死亡」になる回を、ダメージの無い死亡と取り違えないため。
            var playerVitalsBefore = default(PlayerVitals);
            if (isTargetPlayer)
            {
                if (delta.SkillEffects?.Damages.Any(damage => damage.IsDead && damage.Type != EDamageType.Heal) == true)
                {
                    GetPlayerDeathState(targetUuid).HasLethalHit = true;
                }

                playerVitalsBefore = CapturePlayerVitals(targetUuid);
            }

            if (attrCollection?.Attrs != null && attrCollection.Attrs.Any())
            {
                foreach (var attr in attrCollection.Attrs)
                {
                    if (attr.Id != 0 && attr.RawData != null)
                    {
                        changedAttributes.Add((EAttrType)attr.Id);
                    }
                }

                ProcessAttrs(targetUuid, attrCollection.Attrs);
                if (isTargetPlayer)
                {
                    TrackPlayerDeath(targetUuid, playerVitalsBefore, changedAttributes, extraData);
                }

                RecordSummonSource(targetUuid, attrCollection.Attrs, extraData.ArrivalTime);
                RecordSourceLanding(targetUuid, attrCollection.Attrs, extraData.ArrivalTime, isAppear: false);
                RecordNearbyMonster(targetUuid, attrCollection.Attrs);

                // 技の開始は AttrSkillId に値が入ったデルタで分かる。終了は値なし(0)で届く。
                if (changedAttributes.Contains(EAttrType.AttrSkillId)
                    && EncounterManager.Current.GetAttrKV(targetUuid, "AttrSkillId") is int castSkillId
                    && castSkillId > 0)
                {
                    var castSkillLevel = EncounterManager.Current.GetAttrKV(targetUuid, "AttrSkillLevel") is int level ? level : 0;
                    EncounterManager.Current.AddSkillCast(targetUuid, castSkillId, castSkillLevel, extraData);
                }
            }

            if (delta.TempAttrs != null && delta.TempAttrs.Attrs.Any())
            {
                ProcessTempAttrs(targetUuid, delta.TempAttrs.Attrs);
            }

            NearbyEntityProjection.RefreshEntity(targetUuid, changedAttributes);
            PlayerRosterProjection.AddOrUpdateNearbyPlayer(targetUuid);


            var lastDungeonState = BattleStateMachine.DungeonStateHistory.LastOrDefault();
            if (lastDungeonState.Key == EDungeonState.DungeonStateSettlement || lastDungeonState.Key == EDungeonState.DungeonStateVote)
            {

                return;
            }

            var originalArrivalTime = extraData.ArrivalTime;

            long buffBasedShieldBreakValue = 0;
            bool shieldListChangedByBuffRemoval = false;

            var sawBuffAdd = false;
            var sawPotionHealBuff = false;
            var addedBuffIds = new List<int>();
            if (delta.BuffEffect != null)
            {
                for (int buffIdx = 0; buffIdx < delta.BuffEffect.BuffEffects.Count; buffIdx++)
                {

                    var buffEffect = delta.BuffEffect.BuffEffects[buffIdx];

                    // この効果を付与・層の変化として処理したか。
                    var handledByLogicEffect = false;

                    if (buffEffect.LogicEffect != null && buffEffect.LogicEffect.Count > 0)
                    {
                        for (int logicIdx = 0; logicIdx < buffEffect.LogicEffect.Count; logicIdx++)
                        {
                            var logicEffect = buffEffect.LogicEffect[logicIdx];
                            var reader = new Google.Protobuf.CodedInputStream(logicEffect.RawData.ToByteArray());
                            if (logicEffect.EffectType == EBuffEffectLogicPbType.BuffEffectAddBuff)
                            {
                                var buffInfo = BuffInfo.Parser.ParseFrom(reader);
                                DateTime? creationTime = null;
                                if (buffInfo.CreateTime > 0)
                                {
                                    DateTimeOffset dto = DateTimeOffset.FromUnixTimeMilliseconds(buffInfo.CreateTime);

                                    creationTime = dto.UtcDateTime;
                                }
                                sawBuffAdd = true;
                                addedBuffIds.Add(buffInfo.BaseId);
                                if (buffInfo.BaseId == PotionHealBuffId)
                                {
                                    sawPotionHealBuff = true;
                                }

                                BuffInstanceIndex.Instance.Add(
                                    targetUuid,
                                    buffEffect.BuffUuid,
                                    buffInfo.BaseId,
                                    buffInfo.FireUuid,
                                    buffInfo.FightSourceInfo?.FightSourceType ?? -1,
                                    buffInfo.FightSourceInfo?.SourceConfigId ?? 0,
                                    buffInfo.Duration,
                                    extraData.ArrivalTime);

                                EncounterManager.Current.NotifyBuffEvent(targetUuid, buffEffect.Type, buffEffect.BuffUuid, buffInfo.BaseId, buffInfo.Level, buffInfo.FireUuid, buffInfo.Layer, buffInfo.Duration, buffInfo.FightSourceInfo.SourceConfigId, creationTime, extraData, BuffEventPayload.BuffInfo, buffInfo.FightSourceInfo.FightSourceType);
                                handledByLogicEffect = true;
                            }
                            else if (logicEffect.EffectType == EBuffEffectLogicPbType.BuffEffectBuffChange)
                            {

                                var changeInfo = BuffChange.Parser.ParseFrom(reader);
                                DateTime? creationTime = null;
                                if (changeInfo.CreateTime > 0)
                                {
                                    DateTimeOffset dto = DateTimeOffset.FromUnixTimeMilliseconds(changeInfo.CreateTime);

                                    creationTime = dto.UtcDateTime;
                                }

                                EncounterManager.Current.NotifyBuffEvent(targetUuid, buffEffect.Type, buffEffect.BuffUuid, 0, 0, 0, changeInfo.Layer, (int)changeInfo.Duration, 0, creationTime, extraData, BuffEventPayload.BuffChange, fightSourceType: null);
                                handledByLogicEffect = true;
                            }
                        }
                    }

                    // 付与・層の変化として処理できなかった効果は、中身なしの通知として必ず流す。
                    // ここを LogicEffect の有無で切ると、演出などの LogicEffect が付いた除去が
                    // どこにも届かず、そのバフが消えない。同じデルタに同じ buffUuid が二度出る回も同じ。
                    // baseId 以下はサーバが送ってこなかったぶんの 0 で、バフの状態ではない。
                    // 既知のバフの値を上書きしないことは BuffEventPayload.None が担う。
                    if (!handledByLogicEffect)
                    {
                        EncounterManager.Current.NotifyBuffEvent(targetUuid, buffEffect.Type, buffEffect.BuffUuid, 0, 0, 0, 0, 0, 0, null, extraData, BuffEventPayload.None, fightSourceType: null);
                    }

                    if (buffEffect.Type == EBuffEventType.BuffEventRemove)
                    {
                        BuffInstanceIndex.Instance.Remove(targetUuid, buffEffect.BuffUuid, extraData.ArrivalTime);
                        if (EncounterManager.Current.Entities.TryGetValue(targetUuid, out var targetEntity))
                        {
                            List<ShieldInfo>? attrShieldList = targetEntity.GetAttrKV("AttrShieldList") as List<ShieldInfo>;

                            if (attrShieldList != null)
                            {
                                var matches = attrShieldList.Where(x => x.Uuid == buffEffect.BuffUuid);
                                if (matches.Any())
                                {

                                    var match = matches.First();

                                    buffBasedShieldBreakValue = match.Value;

                                    attrShieldList.Remove(match);

                                    targetEntity.SetAttrKV("AttrShieldList", attrShieldList);
                                    shieldListChangedByBuffRemoval = true;
                                }
                            }
                        }
                    }
                }
            }

            // 自分は SyncNearEntities.Appear に出ないので、他人のような全バフスナップショットが届かない。
            // 代わりに自分のコンテナ同期が、起動時とシーン切替のたびに全バフを1デルタで送ってくる。
            // 自分のバフは付与も除去も取りこぼさないので、流れ始めた時点で完全な像を持っている。
            // これが立たないと「マーカーが無い = アビリティ未装着」を自分について確定できない。
            if (sawBuffAdd && IsSelfPlayer(targetUuid))
            {
                EncounterManager.Current.GetOrCreateEntity(targetUuid).MarkSelfBuffStreamReceived();
            }

            if (shieldListChangedByBuffRemoval)
            {
                NearbyEntityProjection.RefreshEntity(targetUuid, ShieldListChangedAttributes);
                PlayerRosterProjection.UpsertPlayer(targetUuid);
            }

            extraData.ArrivalTime = originalArrivalTime;

            if (AppState.IsBenchmarkMode
                && (AppState.IsBenchmarkCompleting || AppState.IsBenchmarkCompleted))
            {
                BattleStateMachine.CheckDeferredCalls();
                return;
            }

            // 薬を飲んだ差分は薬の回復だけ。料理・自然回復としては見ない。
            if (isTargetPlayer)
            {
                if (sawPotionHealBuff)
                {
                    RecordPotionHeal(targetUuid, playerVitalsBefore.Hp, changedAttributes, delta, extraData);
                }
                else
                {
                    RecordPassiveHeal(targetUuid, playerVitalsBefore, changedAttributes, delta, addedBuffIds, extraData);
                }
            }

            var skillEffect = delta.SkillEffects;

            if (skillEffect?.Damages == null || skillEffect.Damages.Count == 0)
            {
                return;
            }

            // 被ダメログの加害者を先に決めて、この同期で被ダメログに載る被弾のうち、技の行ごとの最後の被弾を求める。
            // HP はその被弾に載せる(同期で届く HP は全部当てた後の1つだけなので、同じ同期の技の行には同じ HP が並ぶ)。
            // 技の行の鍵は被ダメログの表示(TakenDamageLogLayout)と同じ: 加害者・バフ由来か・発生源の番号(到着時刻は同期の中で同じ)。
            // 加害者は、見えて名前がある召喚体なら大元の召喚者ではなくその召喚体にする。
            // 自分が原因の被弾(加害者の無いバフのダメージと落下)は被弾した本人。
            var damages = skillEffect.Damages;
            var takenDamageLogActors = new long[damages.Count];
            var lastLoggedTakenDamageIndexByGroup = new Dictionary<(long Actor, bool IsBuffSource, int OwnerId), int>();
            for (var index = 0; index < damages.Count; index++)
            {
                var damageInfo = damages[index];
                if (damageInfo.Type == EDamageType.Heal)
                {
                    continue;
                }

                if (IsSelfCausedDamage(damageInfo))
                {
                    takenDamageLogActors[index] = targetUuid;
                }
                else if (damageInfo.OwnerId == 0)
                {
                    continue;
                }
                else
                {
                    takenDamageLogActors[index] = EncounterManager.Current.ResolveTakenDamageLogActor(damageInfo.AttackerUuid, damageInfo.TopSummonerId);
                }

                if (Encounter.IsTakenDamageLogged(targetUuid, ResolveDamageValue(damageInfo)))
                {
                    lastLoggedTakenDamageIndexByGroup[(takenDamageLogActors[index], damageInfo.DamageSource == EDamageSource.Buff, damageInfo.OwnerId)] = index;
                }
            }

            var carriesTakenDamageHp = new bool[damages.Count];
            foreach (var lastIndex in lastLoggedTakenDamageIndexByGroup.Values)
            {
                carriesTakenDamageHp[lastIndex] = true;
            }

            HashSet<long> rosterPlayersToUpsert = [];

            var damageIndex = -1;
            foreach (var syncDamageInfo in skillEffect.Damages)
            {
                damageIndex++;

                if (IsSelfCausedDamage(syncDamageInfo))
                {
                    if (syncDamageInfo.Type != EDamageType.Heal
                        && !(AppState.IsBenchmarkMode && targetUuid != AppState.PlayerUUID))
                    {
                        AddSelfCausedTakenDamage(syncDamageInfo, targetUuid, carriesTakenDamageHp[damageIndex], extraData);
                        if (isTargetPlayer)
                        {
                            rosterPlayersToUpsert.Add(targetUuid);
                        }
                    }

                    continue;
                }

                if (syncDamageInfo.OwnerId == 0)
                {
                    continue;
                }

                long attackerUuid = (syncDamageInfo.TopSummonerId != 0 ? syncDamageInfo.TopSummonerId : syncDamageInfo.AttackerUuid);
                if (attackerUuid == 0)
                {
                    continue;
                }
                bool isAttackerPlayer = (Utils.UuidToEntityType(attackerUuid) == (long)EEntityType.EntChar);

                // OwnerId の中身は DamageSource で変わる(スキルID / 弾ID / バフID)。
                // 表示に使うキーへ畳む。畳み先を決めるのは行対応表と4言語テーブルだけで、
                // 実行時の情報(バフ実体・召喚体の AttrId など)は見ない。
                //
                // HitEventId が要る。ゲームの行は DamageId の粒度で決まっており、
                // その下2桁がこの値にあたる。OwnerId だけだと別の行が1行に潰れる。
                var foldedSource = SkillSourceResolver.Resolve(
                    syncDamageInfo.DamageSource,
                    syncDamageInfo.OwnerId,
                    syncDamageInfo.HitEventId);
                long skillId = foldedSource.Key;

                // 職業・特化の判定には、畳んだIDではなく「ゲームが実際に発動したスキルID」を使う。
                // 畳み込みは弾・バフ・コンボ段を1つの表示IDへ寄せるので、判定表が想定していない
                // 入力まで当たるようになる。味方に発生させるスキルが畳まれて表に当たると、
                // その人の特化と職業を書き換えてしまう。
                //
                // Skill 由来に限るのは、OwnerId がスキルIDを名乗るのがこのときだけだから。
                // 弾・バフ由来の生IDをそのまま渡すと、SkillTable と BuffTable で重複する
                // 90個のIDに当たる(置換後スキル表の 2301 は BuffTable にも存在する)。
                var identitySkillId = syncDamageInfo.DamageSource == EDamageSource.Skill
                    ? syncDamageInfo.OwnerId
                    : 0;

                if (syncDamageInfo.TopSummonerId != 0)
                {
                    if (EncounterManager.Current.Entities.TryGetValue(syncDamageInfo.AttackerUuid, out var summonedEntity))
                    {
                        EncounterManager.Current.UpdateCasterSkillTierLevel(syncDamageInfo.TopSummonerId, summonedEntity);
                    }
                }

                if (isAttackerPlayer && attackerUuid != 0)
                {
                    EncounterManager.Current.SetEntityType(attackerUuid, EEntityType.EntChar);
                    // 特化判定と同じ理由で、こちらも畳む前の生のスキルIDで引く。
                    // 畳んだIDだと 2301 → 職業13(ビートパフォーマー)が味方に付く。
                    var professionId = foldedSource.IsBuffSource
                        ? 0
                        : Professions.GetBaseProfessionIdBySkillId(identitySkillId);
                    if (professionId != 0
                        && EncounterManager.Current.GetOrCreateEntity(attackerUuid).ProfessionId <= 0
                        && !IsTransformed(attackerUuid))
                    {
                        EncounterManager.Current.SetProfessionId(attackerUuid, professionId);
                    }
                }

                long damage = ResolveDamageValue(syncDamageInfo);

                bool isCrit = (syncDamageInfo.TypeFlag & 1) == 1;
                bool isHeal = syncDamageInfo.Type == EDamageType.Heal;
                var luckyValue = syncDamageInfo.LuckyValue;
                bool isLucky = luckyValue != 0;
                long hpLessen = syncDamageInfo.HpLessenValue;

                bool isCauseLucky = (syncDamageInfo.TypeFlag & 0B100) == 0B100;

                bool isMiss = syncDamageInfo.IsMiss;

                bool isDead = syncDamageInfo.IsDead;

                string damageElement = syncDamageInfo.Property.ToString();

                EDamageSource damageSource = syncDamageInfo.DamageSource;

                long shieldBreak = 0;
                if (syncDamageInfo.Type == EDamageType.Absorbed)
                {
                    shieldBreak = damage;
                }
                else if (buffBasedShieldBreakValue > 0 && targetUuid != attackerUuid)
                {
                    if (hpLessen >= buffBasedShieldBreakValue)
                    {

                        syncDamageInfo.Type = EDamageType.Absorbed;

                        hpLessen = hpLessen - buffBasedShieldBreakValue;
                        shieldBreak = buffBasedShieldBreakValue;
                    }
                    else
                    {

                        shieldBreak = 0;
                    }

                    buffBasedShieldBreakValue = 0;
                }

                if (AppState.IsBenchmarkMode)
                {
                    if (isAttackerPlayer && attackerUuid != AppState.PlayerUUID)
                    {

                        continue;
                    }
                    else if (isAttackerPlayer && attackerUuid == AppState.PlayerUUID)
                    {
                        if (!AppState.HasBenchmarkBegun)
                        {
                            AppState.HasBenchmarkBegun = true;

                            EncounterManager.EnterDungeon(false, EncounterStartReason.BenchmarkStart);
                            BattleStateMachine.StartBenchmarkCompletionTimer();
                        }

                        if (AppState.BenchmarkSingleTarget && Utils.UuidToEntityType(targetUuid) == (long)EEntityType.EntMonster && !isHeal)
                        {
                            if (AppState.BenchmarkSingleTargetUUID == 0)
                            {
                                AppState.BenchmarkSingleTargetUUID = targetUuid;
                            }

                            if (targetUuid != AppState.BenchmarkSingleTargetUUID)
                            {
                                continue;
                            }
                        }
                    }
                }

                if (isHeal)
                {
                    EncounterManager.Current.AddHealing((isAttackerPlayer ? attackerUuid : 0), targetUuid, skillId, syncDamageInfo.OwnerLevel, damage, hpLessen, shieldBreak, syncDamageInfo.Property, syncDamageInfo.Type, syncDamageInfo.DamageMode, isCrit, isLucky, isCauseLucky, isMiss, isDead, extraData);
                }
                else
                {
                    // 記録するのはプレイヤーの攻撃だけ。敵の攻撃は被ダメログ(AddTakenDamage)にだけ残す。
                    if (isAttackerPlayer && attackerUuid != targetUuid)
                    {
                        EncounterManager.Current.AddDamage(attackerUuid, targetUuid, skillId, identitySkillId, syncDamageInfo.OwnerLevel, damage, hpLessen, shieldBreak, syncDamageInfo.Property, syncDamageInfo.Type, syncDamageInfo.DamageMode, isCrit, isLucky, isCauseLucky, isMiss, isDead, extraData);
                    }

                    // バフ由来の被ダメは、そのバフを付けた技をいま生きている実体から控える。
                    // 表示するころには実体が残っていないので、記録の瞬間にしか決まらない。
                    var buffSourceSkillId = 0;
                    var summonSourceSkillId = 0;
                    if (!isAttackerPlayer && Utils.UuidToEntityType(targetUuid) == (long)EEntityType.EntChar)
                    {
                        if (syncDamageInfo.DamageSource == EDamageSource.Buff
                            && BuffInstanceIndex.Instance.TryResolveSourceSkill(
                                syncDamageInfo.OwnerId,
                                syncDamageInfo.AttackerUuid,
                                attackerUuid,
                                extraData.ArrivalTime,
                                out var resolvedBuffSourceSkillId,
                                out _))
                        {
                            buffSourceSkillId = resolvedBuffSourceSkillId;
                        }

                        // 仮想体などが出した被ダメは、その実体を出した技を控えから引く(出現時に決めてある)。
                        if (SummonSourceIndex.Instance.TryGet(syncDamageInfo.AttackerUuid, out var summonSource))
                        {
                            summonSourceSkillId = summonSource.SkillId;
                        }
                    }

                    EncounterManager.Current.AddTakenDamage(takenDamageLogActors[damageIndex], targetUuid, skillId, syncDamageInfo.OwnerId, syncDamageInfo.DamageSource, buffSourceSkillId, summonSourceSkillId, syncDamageInfo.OwnerLevel, damage, hpLessen, shieldBreak, syncDamageInfo.Property, syncDamageInfo.Type, syncDamageInfo.DamageMode, isCrit, isLucky, isCauseLucky, isMiss, isDead, carriesTakenDamageHp[damageIndex], extraData);
                }

                // 畳めずバフIDのまま出す行は、名前を GetBuffName で引く必要がある。
                // SkillTable と BuffTable は90IDが重複するので、種別を持たないと取り違える。
                if (foldedSource.IsBuffSource)
                {
                    EncounterManager.Current.MarkBuffSourcedSkill(
                        isHeal ? (isAttackerPlayer ? attackerUuid : 0) : attackerUuid, skillId);
                    EncounterManager.Current.MarkBuffSourcedSkill(targetUuid, skillId);
                }

                // 見出し表で名前を持たない行の出どころを決める(検知ログも兼ねる)。
                // 印を付けた後に呼ぶので、バフ由来かどうかも正しく残る。
                if (isAttackerPlayer)
                {
                    EncounterManager.Current.ResolveSourceLanding(
                        attackerUuid,
                        skillId,
                        isHeal,
                        syncDamageInfo.DamageSource,
                        syncDamageInfo.OwnerId,
                        syncDamageInfo.AttackerUuid,
                        extraData.ArrivalTime);
                }

                if (isAttackerPlayer)
                {
                    rosterPlayersToUpsert.Add(attackerUuid);
                }

                if (isTargetPlayer)
                {
                    rosterPlayersToUpsert.Add(targetUuid);
                }

                buffBasedShieldBreakValue = 0;
            }

            foreach (var playerUuid in rosterPlayersToUpsert)
            {
                PlayerRosterProjection.UpsertPlayer(playerUuid);
            }

            BattleStateMachine.CheckDeferredCalls();
        }

        public static long currentUserUuid = 0;

        public static void ProcessSyncServerTime(ReadOnlySpan<byte> payloadBuffer, ExtraPacketData extraData)
        {
            if (payloadBuffer.Length == 0)
            {
                return;
            }

            var syncServerTime = SyncServerTime.Parser.ParseFrom(payloadBuffer);
            SkillCooldownStateStore.UpdateServerTime(
                syncServerTime.ClientMilliseconds,
                syncServerTime.ServerMilliseconds);
            ActiveBuffStore.Instance.ResolveUnknownElapsed();
        }

        public static void ProcessSyncToMeDeltaInfo(ReadOnlySpan<byte> payloadBuffer, ExtraPacketData extraData)
        {
            var syncToMeDeltaInfo = SyncToMeDeltaInfo.Parser.ParseFrom(payloadBuffer);
            var aoiSyncToMeDelta = syncToMeDeltaInfo.DeltaInfo;
            long uuid = aoiSyncToMeDelta.Uuid;
            if (uuid != 0 && currentUserUuid != uuid)
            {
                currentUserUuid = uuid;
                AppState.PlayerUUID = uuid;
                AppState.PlayerUID = Utils.UuidToEntityId(uuid);
            }

            NearbyEntityProjection.SetSelfEntity(uuid);
            SkillCooldownStateStore.SetSelfPlayer(uuid);
            SkillCooldownStateStore.UpdateSelfCooldowns(uuid, aoiSyncToMeDelta.SyncSkillCDs);

            var aoiSyncDelta = aoiSyncToMeDelta.BaseDelta;
            if (aoiSyncDelta == null)
            {
                return;
            }
            ProcessAoiSyncDelta(aoiSyncDelta, extraData);

            if (IsWipeCheckQueued)
            {
                IsWipeCheckQueued = false;
                CheckForWipe();
            }

            BattleStateMachine.CheckDeferredCalls();
        }

        /// <summary>
        /// シーン情報を反映する。シーンが切り替わっていればプレイヤー/エンティティのリストをリセットする。
        ///
        /// <para>
        /// 判定は (LevelMapId, SceneGuid) の組。LevelMapId だけでは同一マップのチャンネル切替を、
        /// SceneGuid だけでは同一 GUID のままのマップ往復を取りこぼす。
        /// </para>
        ///
        /// <para>
        /// 同じシーンで何度呼ばれてもリセットは一度だけ。フルコンテナと NotifySocialData の
        /// 両方から呼ばれるため、後着側で再度消さないようにするのが目的。
        /// </para>
        /// </summary>
        private static void ApplySceneData(
            uint levelMapId,
            uint lineId,
            string? sceneGuid,
            string source,
            bool resetForSceneChange)
        {
            // 先にシーンを反映してから刷新する。StartNewMap の中の DB.StartBattle が
            // LevelMapId / SceneName を読むため、逆順だと古いマップ名で battle 行が作られる。
            //
            // ここへ来た時点で「反映してよい通知」と呼び出し側が判断済みなので、
            // SetSceneId 側の AllowSceneUpdate 関門は通す。
            //
            // ただし resetForSceneChange のときは、この直後の StartNewMap → EnterDungeon が
            // 「移動前のマップで戦ったエンカウンター」をそのまま保存し、その battle 行も閉じる。
            // 移動先のシーンをそこへ押すと保存直前に書き換えることになるので、
            // 開いている記録は触らせない(新しい Current へは EnterDungeon が押す)。
            EncounterManager.SetSceneId(
                levelMapId,
                force: true,
                updateOpenRecords: !resetForSceneChange);
            EncounterManager.SetChannelLineId(lineId);

            if (resetForSceneChange)
            {
                // メーター(DPS/HPS)・プレイヤーリスト・エンティティリストを揃えて刷新する。
                // メーターだけ ProcessSyncContainerData に置いたままだと、
                // フルコンテナが来ない切替(ギルドハウスなど)でメーターだけ取り残される。
                BattleStateMachine.StartNewMap();

                // StartNewMap の中の EnterDungeon が属性を新しいエンカウンターへ運ぶ。
                // バリアはマップ移動で消える(ゲーム仕様)のに更新が来ないので、運んだ直後に落とす。
                // ここはマップ移動だと確定している唯一の場所。
                EncounterManager.ClearCarriedOverShields();

                PlayerRosterProjection.BeginMap();
                NearbyEntityProjection.BeginMap();
            }

            // StartNewMap は Current を作り直すので、チャンネル番号はその後に入れる。
            EncounterManager.Current.SetChannelLineNumber(lineId);
            PlayerRosterProjection.UpdateMapName();
            NearbyEntityProjection.UpdateMapName();
        }

        /// <summary>
        /// 計測専用のハンドラ。<c>NotifyBuffChange</c> の中身を記録するだけで、状態は一切変えない。
        /// </summary>
        private static void ProcessNotifyBuffChange(ReadOnlySpan<byte> payloadBuffer, ExtraPacketData extraData)
        {
            try
            {
                var notify = NotifyBuffChange.Parser.ParseFrom(payloadBuffer);
                if (notify != null)
                {
                }
            }
            catch
            {
                // 診断のみ。本来の処理へは伝播させない。
            }
        }

        public static void ProcessSyncContainerData(ReadOnlySpan<byte> payloadBuffer, ExtraPacketData extraData)
        {
            BattleStateMachine.CheckDeferredCalls();

            BattleStateMachine.StartNewMap();

            // メーター(StartNewMap)と同じ契機・同じ無条件リセットに揃える。
            // 3つとも消したあとは継続的に埋め直される: メーターは戦闘イベント、
            // プレイヤーリストとエンティティリストは AOIデルタ
            // (AddOrUpdateNearbyPlayer / RefreshEntity の UpsertAppeared)。
            // Appear の再送を待つ必要はない。
            //
            // シーン識別子の変化を条件にした刷新は、AllowSceneUpdate と TryBeginScene という
            // 2つの取りこぼし経路を作り、マップ切替でリストが刷新されない再発を招いた。
            PlayerRosterProjection.BeginMap();
            NearbyEntityProjection.BeginMap();

            var syncContainerData = SyncContainerData.Parser.ParseFrom(payloadBuffer);
            if (syncContainerData?.VData == null)
            {
                return;
            }

            var vData = syncContainerData.VData;
            if (vData.CharId == 0)
            {
                return;
            }

            long playerUuid = Utils.EntityIdToUuid(vData.CharId, (long)EEntityType.EntChar, false, false);

            NearbyEntityProjection.SetSelfEntity(playerUuid);
            SkillCooldownStateStore.SetSelfPlayer(playerUuid);
            AppState.PlayerUID = vData.CharId;
            if (!string.IsNullOrEmpty(vData.CharBase.AccountId))
            {
                AppState.AccountId = vData.CharBase.AccountId;
            }
            long playerUid = vData.CharId;

            if (vData.RoleLevel?.Level != 0)
            {
                EncounterManager.Current.SetAttrKV(playerUuid, "AttrLevel", vData.RoleLevel.Level);
            }

            if (vData.Attr?.CurHp != 0)
            {
                EncounterManager.Current.SetAttrKV(playerUuid, "AttrHp", vData.Attr.CurHp);
            }

            if (vData.Attr?.MaxHp != 0)
            {
                EncounterManager.Current.SetAttrKV(playerUuid, "AttrMaxHp", vData.Attr.MaxHp);
            }

            if (vData.Attr != null)
            {
                EncounterManager.Current.SetAttrKV(
                    playerUuid,
                    CurrentStaminaSnapshotAttribute,
                    Math.Max(vData.Attr.OriginEnergy, 0f));
            }

            if (vData.CharBase != null)
            {
                if (!string.IsNullOrEmpty(vData.CharBase.Name))
                {
                    EncounterManager.Current.SetName(playerUuid, vData.CharBase.Name);
                    AppState.PlayerName = vData.CharBase.Name;
                }

                if (vData.CharBase.FightPoint != 0)
                {
                    EncounterManager.Current.SetAbilityScore(playerUuid, vData.CharBase.FightPoint);
                }

            }

            var professionList = vData.ProfessionList;
            if (professionList != null && professionList.CurProfessionId != 0 && !IsTransformed(playerUuid))
            {
                UpdateProfessionId(playerUuid, professionList.CurProfessionId);
            }

            PlayerSkillLevelStateStore.ReplaceSelfSkillLevels(
                professionList,
                vData.DutyList);
            RefreshSelfRosterEntry(playerUuid);

            var sceneData = vData.SceneData;
            if (sceneData != null)
            {
                System.Diagnostics.Debug.WriteLine($"ProcessSyncContainerData.SceneData:\n{sceneData}");

                ApplySceneData(
                    sceneData.LevelMapId,
                    sceneData.LineId,
                    sceneData.SceneGuid,
                    "フルコンテナ",
                    resetForSceneChange: false);
            }

            var seasonRoleLevelData = vData.SeasonRoleLevelData;
            if (seasonRoleLevelData != null)
            {
                var lastSeason = seasonRoleLevelData.SeasonRoleLevelMap.LastOrDefault();
                if (lastSeason.Value != null)
                {
                    EncounterManager.Current.SetAttrKV(playerUuid, "AttrSeasonLevel", lastSeason.Value.Level);
                }
            }

            if (vData.Equip != null)
            {
                List<Zproto.EquipNine> playerEquips = new();
                foreach (var equip in vData.Equip.EquipList_)
                {
                    System.Diagnostics.Debug.WriteLine($"{playerUid} :: equip::slot={equip.Value.EquipSlot},refinelvl={equip.Value.EquipSlotRefineLevel}");

                    foreach (var item in vData.ItemPackage.Packages[2].Items)
                    {
                        if ((ulong)item.Key == equip.Value.ItemUuid)
                        {
                            playerEquips.Add(new EquipNine() { EquipID = item.Value.ConfigId, Slot = equip.Value.EquipSlot });
                            break;
                        }
                    }
                }
                EncounterManager.Current.SetAttrKV(playerUuid, "AttrEquipData", playerEquips);
            }

            PlayerRosterProjection.RebuildRoster();
        }

        public static void ProcessSyncContainerDirtyData(ReadOnlySpan<byte> payloadBuffer, ExtraPacketData extraData)
        {
            BattleStateMachine.CheckDeferredCalls();

            try
            {
                if (currentUserUuid == 0)
                {
                    return;
                }
                var dirty = SyncContainerDirtyData.Parser.ParseFrom(payloadBuffer);
                if (dirty?.VData?.Buffer == null || dirty.VData.Buffer.Length == 0)
                {
                    return;
                }

                var buf = dirty.VData.Buffer.ToByteArray();
                bool isStreamSafe = dirty.VData.StreamType == EStreamType.StreamTypeDeltaDirtySafe;
                var ser = new StarResonanceDps.Core.Protocols.Game.Binary.CharSerialize(
                    new StarResonanceDps.Core.Protocols.Game.Binary.BlobReader(buf, isStreamSafe));

                if (ser.CharBaseInfo != null)
                {
                    if (!string.IsNullOrEmpty(ser.CharBaseInfo.Name))
                    {
                        EncounterManager.Current.SetName(currentUserUuid, ser.CharBaseInfo.Name);
                        AppState.PlayerName = ser.CharBaseInfo.Name;
                    }
                    if (!string.IsNullOrEmpty(ser.CharBaseInfo.AccountId) && string.IsNullOrEmpty(AppState.AccountId))
                    {
                        AppState.AccountId = ser.CharBaseInfo.AccountId;
                    }
                    if (ser.CharBaseInfo.FightPoint != null)
                    {
                        EncounterManager.Current.SetAbilityScore(currentUserUuid, (int)ser.CharBaseInfo.FightPoint);
                    }

                    if (ser.CharBaseInfo.TeamInfo != null)
                    {
                        System.Diagnostics.Debug.WriteLine("ser.CharBaseInfo.TeamInfo != null)");
                    }
                }

                if (ser.Attr != null)
                {
                    if (ser.Attr.CurHp != null)
                    {
                        EncounterManager.Current.SetAttrKV(currentUserUuid, "AttrHp", ser.Attr.CurHp);
                    }
                    if (ser.Attr.MaxHp != null)
                    {
                        EncounterManager.Current.SetAttrKV(currentUserUuid, "AttrMaxHp", ser.Attr.MaxHp);
                    }
                    if (ser.Attr.OriginEnergy != null)
                    {
                        EncounterManager.Current.SetAttrKV(
                            currentUserUuid,
                            CurrentStaminaSnapshotAttribute,
                            Math.Max(ser.Attr.OriginEnergy.Value, 0f));
                    }
                }

                if (ser.ProfessionList is not null)
                {
                    if (ser.ProfessionList.CurProfessionId is { } professionId && !IsTransformed(currentUserUuid))
                    {
                        UpdateProfessionId(currentUserUuid, professionId);
                    }

                    PlayerSkillLevelStateStore.ApplySelfProfessionListChanges(
                        ser.ProfessionList);
                }

                if (ser.DutyList is not null)
                {
                    PlayerSkillLevelStateStore.ApplySelfDutyListChanges(ser.DutyList);
                }

                RefreshSelfRosterEntry(currentUserUuid);

                if (ser.SceneData != null)
                {
                    Log.Debug($"ser.sceneData = {ser.SceneData}");
                    if (ser.SceneData.LevelMapId != null)
                    {
                        System.Diagnostics.Debug.WriteLine($"ser.SceneData.LevelMapId = {ser.SceneData.LevelMapId})");
                    }
                }

                PlayerRosterProjection.UpsertSelf(currentUserUuid);
            }
            catch (Exception)
            {

                throw;
            }
        }

        public static void ProcessSyncDungeonData(ReadOnlySpan<byte> payloadBuffer, ExtraPacketData extraData)
        {
            BattleStateMachine.CheckDeferredCalls();

            var syncDungeonData = SyncDungeonData.Parser.ParseFrom(payloadBuffer);
            if (syncDungeonData?.VData == null)
            {
                return;
            }

            var vData = syncDungeonData.VData;

            if (vData.DungeonPlayerList != null && vData.DungeonPlayerList.PlayerInfos.Count > 0)
            {
                System.Diagnostics.Debug.WriteLine("vData.DungeonPlayerList != null");
                System.Diagnostics.Debug.WriteLine(vData.DungeonPlayerList.PlayerInfos);
            }

            for(int listIdx = 0; listIdx < vData.Title.TitleList.Count; listIdx++)
            {
                var title_list = vData.Title.TitleList[listIdx];
                for (int infoIdx = 0; infoIdx < title_list.TitleInfo.Count; infoIdx++)
                {
                    var title_info = title_list.TitleInfo[infoIdx];
                    System.Diagnostics.Debug.WriteLine($"TitleList[{listIdx}].TitleInfo[{infoIdx}]: Uuid={title_info.Uuid},TitleId{title_info.TitleId}");
                }
            }

            if (vData.DungeonSceneInfo != null)
            {
                EncounterManager.ApplyDungeonDifficulty(vData.DungeonSceneInfo.Difficulty);
            }

            EncounterManager.Current.DungeonState = vData.FlowInfo.State;
            BattleStateMachine.DungeonStateHistoryAdd(vData.FlowInfo.State);

            int dungeonVarDataIdx = 0;
            foreach (var dungeonVarData in vData.DungeonVar.DungeonVarData)
            {
                System.Diagnostics.Debug.WriteLine($"DungeonVar.DungeonVarData[{dungeonVarDataIdx}] = {dungeonVarData}");
                dungeonVarDataIdx++;
            }

            foreach (var targetData in vData.Target.TargetData)
            {
                BattleStateMachine.DungeonTargetDataHistoryAdd(targetData.Value);
                System.Diagnostics.Debug.WriteLine($"Target.TargetData[{targetData.Key}]: TargetId={targetData.Value.TargetId},Nums={targetData.Value.Nums},Complete={targetData.Value.Complete}");
            }

            foreach (var damage in vData.Damage.Damages)
            {
                System.Diagnostics.Debug.WriteLine($"Damage.Damages[{damage.Key}]: {damage.Value}");
            }

            if (vData.TimerInfo != null)
            {
                System.Diagnostics.Debug.WriteLine($"TimerInfo = {vData.TimerInfo.Type}, {vData.TimerInfo.StartTime}, {vData.TimerInfo.DungeonTimes}, {vData.TimerInfo.Direction}, {vData.TimerInfo.Index}, {vData.TimerInfo.ChangeTime}, {vData.TimerInfo.EffectType}, {vData.TimerInfo.PauseTime}, {vData.TimerInfo.PauseTotalTime}, {vData.TimerInfo.OutLookType}");
            }

            System.Diagnostics.Debug.WriteLine($"syncDungeonData.vData State={vData.FlowInfo.State},TotalScore={vData.DungeonScore.TotalScore},CurRatio={vData.DungeonScore.CurRatio}");
        }

        public static ConcurrentQueue<EActorState> PlayerStateHistory = new();
        public static void CheckForWipe()
        {


            if (currentUserUuid != 0)
            {
                if (!EncounterManager.Current.HasStatsBeenRecorded())
                {
                    return;
                }

                if (EncounterManager.Current.IsWipe)
                {
                    if (EncounterManager.Current.GetDuration().TotalSeconds < 2)
                    {
                        System.Diagnostics.Debug.WriteLine("EncounterManager.Current Duration was under 2 seconds, correcting the Wipe State to false");
                        EncounterManager.Current.SetWipeState(false);
                    }

                    if (!EncounterManager.Current.HasStatsBeenRecorded())
                    {
                        EncounterManager.Current.SetWipeState(false);
                    }

                    System.Diagnostics.Debug.WriteLine("EncounterManager.Current.IsWipe already true");
                }

                var playerEntity = EncounterManager.Current.GetOrCreateEntity(currentUserUuid);
                var attrState = playerEntity.GetAttrKV("AttrState");
                if (attrState != null)
                {

                    if (PlayerStateHistory == null)
                    {
                        PlayerStateHistory = new();
                    }

                    if (PlayerStateHistory.Count >= 5)
                    {
                        PlayerStateHistory.TryDequeue(out _);
                    }

                    {
                        PlayerStateHistory.Enqueue((EActorState)attrState);
                    }
                }
                else
                {
                    return;
                }

                // 全滅の印は復活不可デバフ 510072。付与から1秒経っていれば新しい戦闘へ切り替える。
                // 状態遷移(Dead→Resurrection→TelePort)からは推測しない。
                var currentEncounterDuration = EncounterManager.Current.GetDuration();
                var characterList = EncounterManager.Current.Entities.AsValueEnumerable().Where(x => x.Value.EntityType == EEntityType.EntChar);
                foreach (var character in characterList)
                {
                    if (character.Value.RecentBuffEventHistory.Count > 0)
                    {
                        foreach (var recentBuff in character.Value.RecentBuffEventHistory)
                        {
                            if (recentBuff.Value.BaseId == 510072)
                            {

                                EncounterManager.Current.SetWipeState(true);

                                if (recentBuff.Value.EventAddTime.Add(TimeSpan.FromSeconds(1.0)).TotalSeconds <= currentEncounterDuration.TotalSeconds)
                                {
                                    Log.Debug($"Encounter Wipe Reset buff was found and duration was hit, creating a new Encounter now");
                                    EncounterManager.Current.SetWipeState(true);
                                    EncounterManager.EnterDungeon(false, EncounterStartReason.Wipe);
                                    return;
                                }
                            }
                        }
                    }
                }
            }
        }

        public static void ProcessSyncDungeonDirtyData(ReadOnlySpan<byte> payloadBuffer, ExtraPacketData extraData)
        {
            BattleStateMachine.CheckDeferredCalls();

            var dirty = SyncDungeonDirtyData.Parser.ParseFrom(payloadBuffer);
            if (dirty?.VData?.Buffer == null || dirty.VData.Buffer.Length == 0)
            {
                return;
            }

            var buf = dirty.VData.Buffer.ToByteArray();

            bool isStreamSafe = dirty.VData.StreamType == EStreamType.StreamTypeDeltaDirtySafe;
            var dun = new StarResonanceDps.Core.Protocols.Game.Binary.DungeonDirtyData(
                new StarResonanceDps.Core.Protocols.Game.Binary.BlobReader(buf, isStreamSafe));

            if (dun?.PlayerList != null && dun?.PlayerList.PlayerInfos.Count > 0)
            {
                System.Diagnostics.Debug.WriteLine("dun?.PlayerList != null");
                System.Diagnostics.Debug.WriteLine(dun?.PlayerList.PlayerInfos);
            }

            if (dun?.FlowInfo != null)
            {
                if (dun.FlowInfo?.State != null)
                {
                    EDungeonState dungeonState = (EDungeonState)dun.FlowInfo.State;
                    EncounterManager.Current.DungeonState = dungeonState;
                    BattleStateMachine.DungeonStateHistoryAdd(dungeonState);
                }
            }

            if (dun?.Damage != null)
            {
                foreach (var item in dun.Damage.Damages!)
                {
                    System.Diagnostics.Debug.WriteLine($"dun.Damage.Damages = {item.Key}, {item.Value}");
                }
            }

            if (dun?.DungeonPioneer != null)
            {
                foreach (var item in dun.DungeonPioneer.CompletedTargetThisTime!)
                {
                    var CompletedTargetListIdx = 0;
                    foreach (var completedTargetList in item.Value.CompletedTargetList!)
                    {
                        System.Diagnostics.Debug.WriteLine($"[{item.Key}]CompletedTargetList[{CompletedTargetListIdx}] = {completedTargetList.Key}, {completedTargetList.Value}");
                        CompletedTargetListIdx++;
                    }
                }
            }

            if (dun?.DungeonVar?.Data != null)
            {
                BattleStateMachine.DungeonVarHistoryAdd(dun.DungeonVar);

                if (dun?.DungeonVar?.Data.Count > 1)
                {
                }

                int dungeonVarDataIdx = 0;
                foreach (var dungeonVarData in dun.DungeonVar.Data!)
                {

                    dungeonVarDataIdx++;
                }
            }

            if (dun?.DungeonEvent?.DungeonEventData != null)
            {
                foreach (var dungeonEventData in dun.DungeonEvent.DungeonEventData)
                {
                    System.Diagnostics.Debug.WriteLine($"[{dungeonEventData.Key}]DungeonEventData = {dungeonEventData.Value.EventId}, {dungeonEventData.Value.StartTime}, {dungeonEventData.Value.State}, {dungeonEventData.Value.Result}");
                    if (dungeonEventData.Value.DungeonTarget != null)
                    {
                        foreach (var dungeonTarget in dungeonEventData.Value.DungeonTarget)
                        {
                            System.Diagnostics.Debug.WriteLine($"- {dungeonTarget.Key}: {dungeonTarget.Value.TargetId}, {dungeonTarget.Value.Complete}, {dungeonTarget.Value.Nums}");
                        }
                    }
                }
            }

            if (dun?.TimerInfo != null)
            {
                System.Diagnostics.Debug.WriteLine($"dun.TimerInfo = {dun.TimerInfo.TimerType}, {dun.TimerInfo.StartTime}, {dun.TimerInfo.DungeonTimes}, {dun.TimerInfo.Direction}, {dun.TimerInfo.Index}, {dun.TimerInfo.ChangeTime}, {dun.TimerInfo.EffectType}, {dun.TimerInfo.PauseTime}, {dun.TimerInfo.PauseTotalTime}, {dun.TimerInfo.OutLookType}");
                if (dun.TimerInfo.EffectType == EDungeonTimerEffectType.Sub && dun.TimerInfo.ChangeTime != null)
                {
                    EncounterManager.Current.ExData.DungeonTimeDeathChange += (int)dun.TimerInfo.ChangeTime;
                }
            }

            if (dun?.DungeonVarAll?.DungeonVarAllMap?.Count > 0)
            {
                if (dun?.DungeonVar?.Data.Count > 1)
                {
                    System.Diagnostics.Debug.WriteLine("DungeonVarAll.DungeonVarAllMap.Count > 1!!");
                }

                int dungeonVarAllMapIdx = 0;
                foreach (var dungeonVarAllMap in dun.DungeonVarAll.DungeonVarAllMap!)
                {
                    int dungeonVarDataIdx = 0;
                    foreach (var dungeonVarData in dungeonVarAllMap.Value.Data)
                    {
                        System.Diagnostics.Debug.WriteLine($"dun.DungeonVarAll.DungeonVarAllMap.[{dungeonVarAllMapIdx}][{dungeonVarAllMap.Key}][{dungeonVarDataIdx}] = {dungeonVarData.Name}, {dungeonVarData.Value}");
                        dungeonVarDataIdx++;
                    }

                    dungeonVarAllMapIdx++;
                }
            }

            if (dun?.Target?.TargetData != null)
            {
                if (dun.Target.TargetData.Count > 1)
                {
                    System.Diagnostics.Debug.WriteLine("Target.TargetData.Count > 1!!");
                }

                foreach (var target in dun.Target.TargetData)
                {
                    System.Diagnostics.Debug.WriteLine($"dun.Target.TargetData.Target = {target.Key}, [TargetId:{target.Value.TargetId}, Complete:{target.Value.Complete}, Nums:{target.Value.Nums}]");

                    BattleStateMachine.DungeonTargetDataHistoryAdd(target.Value);
                }
            }
        }
    }
}
