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

        public static void ProcessUnhandled(NotifyId notifyId, ReadOnlySpan<byte> payloadBuffer, ExtraPacketData extraData)
        {
            System.Diagnostics.Debug.WriteLine($"ProcessUnhandled ServiceId:{(EServiceId)notifyId.ServiceId} MethodId:{notifyId.MethodId} Payload.Length:{payloadBuffer.Length}");
            if (payloadBuffer.Length == 0)
            {
                return;
            }
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
                        ProcessAttrs(vData.EnterSceneInfo.PlayerEnt.Uuid, vData.EnterSceneInfo.PlayerEnt.Attrs.Attrs);
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
                EncounterManager.Current.AddSceneEvent(evt);
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

            if (entity.ProfessionId == 0 && socialData.ProfessionData?.ProfessionId > 0)
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
                // ただし別のマップ/チャンネルへ移った通知は抑制対象ではない。
                // 関門を再武装するのはフルコンテナ到着(StartNewMap)だけなので、
                // フルコンテナが来ない切替(実測: ギルドハウス levelMapId=12000)では
                // 関門が閉じたままになり、移動通知が5回届いても全部捨てていた。
                // その結果リストは刷新されず、マップ名も古いままだった(2026-08-26 実測)。
                var isSceneChange = socialScene.LevelMapId != EncounterManager.LevelMapId
                    || socialScene.LineId != EncounterManager.ChannelLineId;

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

        private static bool IsSelfPlayer(long uuid)
        {
            return uuid == currentUserUuid
                || uuid == AppState.PlayerUUID
                || (AppState.PlayerUID != 0
                    && Utils.UuidToEntityId(uuid) == AppState.PlayerUID);
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
        /// ここで特化は決めない。以前は <c>ProfessionTalentInfo.TalentStageCfgId</c> から
        /// 特化を導いていたが、この値は「どのタレントツリーを選んでいるか」であって
        /// 「特化アビリティを装着しているか」ではない。実測(2026-08-25)では、
        /// アビリティ未装着でツリーだけ剛守のとき `TalentStageCfgId = 114` が返り続け、
        /// 装着していない剛守を表示し続けていた。
        /// 特化は自分も他人と同じく特化マーカーバフだけで決める。
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

        public static void ProcessAttrs(long uuid, RepeatedField<Attr> attrs)
        {
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
                        EncounterManager.Current.SetAttrKV(uuid, attrIdName, isNoValue ? 0 : reader.ReadInt32());
                        break;
                    case EAttrType.AttrLevel:
                        EncounterManager.Current.SetAttrKV(uuid, attrIdName, isNoValue ? 0 : reader.ReadInt32());
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
                    case EAttrType.AttrAttack:
                        EncounterManager.Current.SetAttrKV(uuid, attrIdName, isNoValue ? 0L : reader.ReadInt64());
                        break;
                    case EAttrType.AttrDefense:
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
                        EncounterManager.Current.SetAttrKV(uuid, attrIdName, isNoValue ? 0 : reader.ReadInt32());
                        break;
                    case EAttrType.AttrSeasonStrength:
                        EncounterManager.Current.SetAttrKV(uuid, attrIdName, isNoValue ? 0 : reader.ReadInt32());
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
                    ProcessAttrs(entity.Uuid, attrCollection.Attrs);
                }


                ApplyAppearBuffSnapshot(entity);

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
        /// ここで扱うのは特化判定だけ。バフ表示側(<c>ActiveBuffStore</c>)へは流していない。
        /// </para>
        /// </summary>
        private static void ApplyAppearBuffSnapshot(Zproto.Entity entity)
        {
            // 特化を持つのはプレイヤーだけ。モンスターまで通すと診断ログが埋まる。
            if (Utils.UuidToEntityType(entity.Uuid) != (long)EEntityType.EntChar)
            {
                return;
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
                if (buffInfo.BaseId > 0)
                {
                    snapshot.Add((
                        buffInfo.BaseId,
                        buffInfo.BuffUuid,
                        buffInfo.FireUuid,
                        buffInfo.FightSourceInfo?.FightSourceType ?? 0,
                        buffInfo.FightSourceInfo?.SourceConfigId ?? 0));
                }
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
            }


            if (delta.TempAttrs != null && delta.TempAttrs.Attrs.Any())
            {
                ProcessTempAttrs(targetUuid, delta.TempAttrs.Attrs);
            }

            NearbyEntityProjection.RefreshEntity(targetUuid, changedAttributes);
            PlayerRosterProjection.AddOrUpdateNearbyPlayer(targetUuid);

            if (AppState.IsEncounterSavingPaused && Settings.Instance.MinimalProcessingWhileEncounterSavingPaused)
            {
                BattleStateMachine.CheckDeferredCalls();
                return;
            }

            var lastDungeonState = BattleStateMachine.DungeonStateHistory.LastOrDefault();
            if (lastDungeonState.Key == EDungeonState.DungeonStateSettlement || lastDungeonState.Key == EDungeonState.DungeonStateVote)
            {

                return;
            }

            var originalArrivalTime = extraData.ArrivalTime;

            long buffBasedShieldBreakValue = 0;
            bool shieldListChangedByBuffRemoval = false;

            List<int> EventHandledBuffs = new();
            List<int> LogicHandledBuffs = new();
            var sawBuffAdd = false;
            if (delta.BuffEffect != null)
            {
                for (int buffIdx = 0; buffIdx < delta.BuffEffect.BuffEffects.Count; buffIdx++)
                {

                    var buffEffect = delta.BuffEffect.BuffEffects[buffIdx];
                    EventHandledBuffs.Add(buffEffect.BuffUuid);


                    if (buffEffect.LogicEffect != null && buffEffect.LogicEffect.Count > 0)
                    {
                        for (int logicIdx = 0; logicIdx < buffEffect.LogicEffect.Count; logicIdx++)
                        {
                            LogicHandledBuffs.Add(buffEffect.BuffUuid);

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

                                EncounterManager.Current.NotifyBuffEvent(targetUuid, buffEffect.Type, buffEffect.BuffUuid, buffInfo.BaseId, buffInfo.Level, buffInfo.FireUuid, buffInfo.Layer, buffInfo.Duration, buffInfo.FightSourceInfo.SourceConfigId, creationTime, extraData, buffInfo.FightSourceInfo?.FightSourceType ?? 0);
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

                                EncounterManager.Current.NotifyBuffEvent(targetUuid, buffEffect.Type, buffEffect.BuffUuid, 0, 0, 0, changeInfo.Layer, (int)changeInfo.Duration, 0, creationTime, extraData);
                            }
                        }
                    }
                    else
                    {

                        if (!LogicHandledBuffs.Contains(buffEffect.BuffUuid))
                        {
                            // LogicEffect が無い回。baseId 以下はサーバが送ってこなかったぶんの 0 で、
                            // バフの状態ではない。carriesBuffInfo: false で「知らない」ことを伝える。
                            EncounterManager.Current.NotifyBuffEvent(targetUuid, buffEffect.Type, buffEffect.BuffUuid, 0, 0, 0, 0, 0, 0, null, extraData, carriesBuffInfo: false);
                        }
                    }

                    if (buffEffect.Type == EBuffEventType.BuffEventRemove)
                    {
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

            var skillEffect = delta.SkillEffects;

            if (skillEffect?.Damages == null || skillEffect.Damages.Count == 0)
            {
                return;
            }

            HashSet<long> rosterPlayersToUpsert = [];

            foreach (var syncDamageInfo in skillEffect.Damages)
            {

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
                // 表示に使うIDへ畳む。畳み先を決めるのは対応表と4言語テーブルだけで、
                // 実行時の情報(バフ実体・召喚体の AttrId など)は見ない。
                var foldedSource = SkillSourceResolver.Resolve(
                    syncDamageInfo.DamageSource,
                    syncDamageInfo.OwnerId);
                int skillId = foldedSource.Id;

                // 職業・特化の判定には、畳んだIDではなく「ゲームが実際に発動したスキルID」を使う。
                // 畳み込みは弾・バフ・コンボ段を1つの表示IDへ寄せるので、判定表が想定していない
                // 入力まで当たるようになる(実測: 安可が味方に発生させる 230401 が
                // 2304 を経てコンボ先頭 2301 へ畳まれ、味方16人を響奏にしていた)。
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
                    if (professionId != 0 && EncounterManager.Current.GetOrCreateEntity(attackerUuid).ProfessionId <= 0)
                    {
                        EncounterManager.Current.SetProfessionId(attackerUuid, professionId);
                    }
                }

                long damage = 0;
                if (syncDamageInfo.Value != 0)
                {
                    damage = syncDamageInfo.Value;
                }
                else if (syncDamageInfo.LuckyValue != 0)
                {
                    damage = syncDamageInfo.LuckyValue;
                }

                if (damage < 0)
                {
                    if (syncDamageInfo.HpLessenValue > 0)
                    {
                        damage = syncDamageInfo.HpLessenValue;
                    }
                    else
                    {
                        damage = 0;
                    }
                }

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
                    EncounterManager.Current.AddHealing((isAttackerPlayer ? attackerUuid : 0), targetUuid, skillId, syncDamageInfo.OwnerLevel, damage, hpLessen, shieldBreak, syncDamageInfo.Property, syncDamageInfo.Type, syncDamageInfo.DamageMode, isCrit, isLucky, isCauseLucky, isMiss, isDead, syncDamageInfo.DamagePos, extraData);
                }
                else
                {
                    if (attackerUuid != targetUuid)
                    {
                        EncounterManager.Current.AddDamage(attackerUuid, targetUuid, skillId, identitySkillId, syncDamageInfo.OwnerLevel, damage, hpLessen, shieldBreak, syncDamageInfo.Property, syncDamageInfo.Type, syncDamageInfo.DamageMode, isCrit, isLucky, isCauseLucky, isMiss, isDead, syncDamageInfo.DamagePos, extraData);
                    }

                    EncounterManager.Current.AddTakenDamage(attackerUuid, targetUuid, skillId, syncDamageInfo.OwnerLevel, damage, hpLessen, shieldBreak, syncDamageInfo.Property, syncDamageInfo.Type, syncDamageInfo.DamageMode, isCrit, isLucky, isCauseLucky, isMiss, isDead, syncDamageInfo.DamagePos, extraData);
                }

                // 畳めずバフIDのまま出す行は、名前を GetBuffName で引く必要がある。
                // SkillTable と BuffTable は90IDが重複するので、種別を持たないと取り違える。
                if (foldedSource.IsBuffSource)
                {
                    EncounterManager.Current.MarkBuffSourcedSkill(
                        isHeal ? (isAttackerPlayer ? attackerUuid : 0) : attackerUuid, skillId);
                    EncounterManager.Current.MarkBuffSourcedSkill(targetUuid, skillId);
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
            EncounterManager.SetSceneId(levelMapId, force: true);
            EncounterManager.SetChannelLineId(lineId);

            if (resetForSceneChange)
            {
                // メーター(DPS/HPS)・プレイヤーリスト・エンティティリストを揃えて刷新する。
                // メーターだけ ProcessSyncContainerData に置いたままだと、
                // フルコンテナが来ない切替(実測: ギルドハウス)でメーターだけ取り残される。
                BattleStateMachine.StartNewMap();
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
            if (professionList != null && professionList.CurProfessionId != 0)
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
                    if (ser.ProfessionList.CurProfessionId is { } professionId)
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
                EncounterManager.Current.SetDungeonDifficulty(vData.DungeonSceneInfo.Difficulty);
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

            if (!Settings.Instance.UseAutomaticWipeDetection)
            {
                return;
            }

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

                if (!Settings.Instance.UseLegacyWipeDetection)
                {
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
                    return;
                }

                bool useNoTeleportWipePattern = Settings.Instance.SkipTeleportStateCheckInAutomaticWipeDetection;
                bool isStateWipePattern = false;
                int stateCount = PlayerStateHistory.Count();
                if (useNoTeleportWipePattern == false && stateCount >= 3 && PlayerStateHistory.ElementAt(stateCount - 1) == EActorState.ActorStateTelePort)
                {
                    if (PlayerStateHistory.ElementAt(stateCount - 2) == EActorState.ActorStateResurrection)
                    {
                        if (PlayerStateHistory.ElementAt(stateCount - 3) == EActorState.ActorStateDead)
                        {
                            isStateWipePattern = true;
                        }
                    }
                    else if (PlayerStateHistory.ElementAt(stateCount - 2) == EActorState.ActorStateDead)
                    {
                        isStateWipePattern = true;
                    }
                }
                else if (useNoTeleportWipePattern && stateCount >= 2 && PlayerStateHistory.ElementAt(stateCount - 1) == EActorState.ActorStateResurrection)
                {
                    if (PlayerStateHistory.ElementAt(stateCount - 2) == EActorState.ActorStateDead)
                    {
                        isStateWipePattern = true;
                    }
                }

                if (EncounterManager.Current.HasStatsBeenRecorded())
                {
                    var characterList = EncounterManager.Current.Entities.AsValueEnumerable().Where(x => x.Value.EntityType == EEntityType.EntChar);
                    bool areAllCharactersDead = true;
                    foreach (var character in characterList)
                    {
                        var charState = character.Value.GetAttrKV("AttrState");
                        if (charState != null)
                        {
                            EActorState actorState = (EActorState)charState;
                            if (actorState != EActorState.ActorStateDead && actorState != EActorState.ActorStateResurrection && character.Value.Hp > 0)
                            {

                                if (actorState == EActorState.ActorStateTelePort && character.Value.RecentHpHistory.Count > 0)
                                {
                                    long lowestHp = character.Value.MaxHp;

                                    int stackSize = character.Value.RecentHpHistory.Count > 3 ? 3 : character.Value.RecentHpHistory.Count;
                                    for (int i = 0; i < stackSize; i++)
                                    {
                                        long historicalHp = character.Value.RecentHpHistory.ElementAt(i);
                                        if (historicalHp < lowestHp)
                                        {
                                            lowestHp = historicalHp;
                                        }
                                    }

                                    if (lowestHp == 0)
                                    {

                                        continue;
                                    }
                                }

                                areAllCharactersDead = false;
                            }
                        }
                        else if (character.Value.Hp > 0 || character.Value.MaxHp == 0)
                        {
                            areAllCharactersDead = false;
                        }
                    }
                    if (areAllCharactersDead && !isStateWipePattern)
                    {
                        Log.Debug($"All characters were reported as actively dead in current Encounter. Overriding isStateWipePattern to true.");
                        isStateWipePattern = true;
                    }
                    if (!Settings.Instance.DisableWipeRecalculationOverwriting && !areAllCharactersDead && isStateWipePattern)
                    {
                        Log.Debug($"Not all characters were reported as actively dead in current Encounter. Overriding isStateWipePattern to false.");
                        isStateWipePattern = false;
                    }
                }
                else
                {

                    isStateWipePattern = false;
                    if (EncounterManager.Current.IsWipe)
                    {
                        EncounterManager.Current.SetWipeState(false);
                    }
                }

                if (isStateWipePattern)
                {

                    var bosses = EncounterManager.Current.Entities.AsValueEnumerable().Where(x => x.Value.MonsterType == EMonsterType.Boss);

                    if (bosses.Count() > 0)
                    {
                        int bossesAtMaxHp = 0;
                        foreach (var boss in bosses)
                        {

                            long? hp = boss.Value.GetAttrKV("AttrHp") as long?;
                            long? maxHp = boss.Value.GetAttrKV("AttrMaxHp") as long?;
                            var bossState = boss.Value.GetAttrKV("AttrState");
                                                        if ((bossState != null && (EActorState)bossState == EActorState.ActorStateBorn) || (boss.Value.RecentHpHistory.Count > 1 && (hp != null && maxHp != null && hp > 0 && maxHp > 0 && hp >= maxHp)))
                            {
                                EncounterManager.Current.SetWipeState(true);
                                System.Diagnostics.Debug.WriteLine($"We've hit a wipe (bossesAtMaxHp = {bossesAtMaxHp})! Start up a new encounter");
                                EncounterManager.EnterDungeon(false, EncounterStartReason.Wipe);
                            }
                            else
                            {
                                System.Diagnostics.Debug.WriteLine($"We didn't hit a wipe yet {boss.Value.UUID} - {boss.Value.Name} {hp} / {maxHp}");
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
