using Google.Protobuf;
using StarResonanceDps.Core.Combat;
using StarResonanceDps.Core.Protocols.Game.Binary;
using StarResonanceDps.Core.Protocols.Game.Notifications;
using EStreamType = Zproto.EStreamType;
using EDungeonState = Zproto.EDungeonState;
using EnterScene = Zproto.WorldNtfCsharp.Types.EnterScene;
using SyncContainerData = Zproto.WorldNtfCsharp.Types.SyncContainerData;
using SyncContainerDirtyData = Zproto.WorldNtfCsharp.Types.SyncContainerDirtyData;
using SyncDungeonData = Zproto.WorldNtfCsharp.Types.SyncDungeonData;
using SyncDungeonDirtyData = Zproto.WorldNtfCsharp.Types.SyncDungeonDirtyData;
using SyncNearDeltaInfo = Zproto.WorldNtfCsharp.Types.SyncNearDeltaInfo;
using SyncNearEntities = Zproto.WorldNtfCsharp.Types.SyncNearEntities;
using SyncSceneEvents = Zproto.WorldNtfCsharp.Types.SyncSceneEvents;
using SyncToMeDeltaInfo = Zproto.WorldNtfCsharp.Types.SyncToMeDeltaInfo;
using WorldActActivityData = Zproto.WorldActActivityData;
using DirtyContainer = StarResonanceDps.Core.Protocols.Game.Binary.CharSerialize;
using DirtyDungeon = StarResonanceDps.Core.Protocols.Game.Binary.DungeonDirtyData;
using ChatNotification = StarResonanceDps.Core.Protocols.Game.Notifications.ChitChatNtf;
using MatchNotification = StarResonanceDps.Core.Protocols.Game.Notifications.MatchNtf;
using SocialNotification = StarResonanceDps.Core.Protocols.Game.Notifications.SocialNtf;
using TeamNotification = StarResonanceDps.Core.Protocols.Game.Notifications.GrpcTeamNtf;
using WorldActivityNotification = StarResonanceDps.Core.Protocols.Game.Notifications.WorldActivityNtf;
using WorldActNotification = StarResonanceDps.Core.Protocols.Game.Notifications.WorldActNtf;
using WorldNotification = StarResonanceDps.Core.Protocols.Game.Notifications.WorldNtf;

namespace StarResonanceDps.Core.Services;

internal enum GameNotificationProcessResult
{
    Ignored,
    Processed,
    InvalidPayload
}

internal readonly record struct GameNotificationProcessOutcome(
    GameNotificationProcessResult Result,
    string Source,
    int VisibleRosterSize,
    int CombatEventCount);

internal sealed class GameNotificationProcessor
{
    private readonly GameCombatStore _combatStore;
    private readonly GameMapStateMachine _mapStateMachine;

    public GameNotificationProcessor(GameCombatStore combatStore)
    {
        _combatStore = combatStore;
        _mapStateMachine = new GameMapStateMachine(combatStore);
    }

    public void Reset()
    {
        _combatStore.Reset();
    }

    public GameNotificationProcessOutcome Process(
        ulong serviceId,
        uint methodId,
        ReadOnlySpan<byte> payload,
        DateTimeOffset receivedAtUtc)
    {
        if (serviceId == (ulong)EServiceId.SocialNtf
            && methodId == (uint)SocialNotification.NotifySocialData)
        {
            return ProcessNotifySocialData(payload);
        }

        if (serviceId == (ulong)EServiceId.WorldActivityNtf
            && methodId == (uint)WorldActivityNotification.SyncHitInfo)
        {
            return Capture(payload, "SyncHitInfo", Zproto.WorldCsharp.Types.SyncHitInfo.Parser);
        }

        if (serviceId == (ulong)EServiceId.WorldActNtf
            && methodId == (uint)WorldActNotification.SyncWorldActData)
        {
            return Capture(payload, "SyncWorldActData", WorldActActivityData.Parser);
        }

        if (serviceId == (ulong)EServiceId.MatchNtf)
        {
            return ProcessMatchNotification(methodId, payload);
        }

        if (serviceId == (ulong)EServiceId.GrpcTeamNtf)
        {
            return ProcessTeamNotification(methodId, payload);
        }

        if (serviceId == (ulong)EServiceId.ChitChatNtf
            && methodId == (uint)ChatNotification.NotifyNewestChitChatMsgs)
        {
            return Capture(payload, "NotifyNewestChitChatMsgs", Zproto.ChitChatNtf.Types.NotifyNewestChitChatMsgs.Parser);
        }

        if (serviceId != (ulong)EServiceId.WorldNtf)
        {
            return Ignored(serviceId, methodId);
        }

        return (WorldNotification)methodId switch
        {
            WorldNotification.EnterScene => ProcessEnterScene(payload),
            WorldNotification.SyncNearEntities => ProcessSyncNearEntities(payload),
            WorldNotification.SyncSceneEvents => ProcessSyncSceneEvents(payload),
            WorldNotification.SyncContainerData => ProcessSyncContainerData(payload),
            WorldNotification.SyncContainerDirtyData => ProcessSyncContainerDirtyData(payload),
            WorldNotification.SyncDungeonData => ProcessSyncDungeonData(payload, receivedAtUtc),
            WorldNotification.SyncDungeonDirtyData => ProcessSyncDungeonDirtyData(payload, receivedAtUtc),
            WorldNotification.SyncNearDeltaInfo => ProcessSyncNearDeltaInfo(payload, receivedAtUtc),
            WorldNotification.SyncToMeDeltaInfo => ProcessSyncToMeDeltaInfo(payload, receivedAtUtc),
            WorldNotification.NotifyAllMemberReady => Capture(payload, "NotifyAllMemberReady", Zproto.WorldNtf.Types.NotifyAllMemberReady.Parser),
            WorldNotification.NotifyCaptainReady => Capture(payload, "NotifyCaptainReady", Zproto.WorldNtf.Types.NotifyCaptainReady.Parser),
            _ => Ignored(serviceId, methodId)
        };
    }

    private GameNotificationProcessOutcome ProcessNotifySocialData(ReadOnlySpan<byte> payload)
    {
        try
        {
            var message = Zproto.SocialNtf.Types.NotifySocialData.Parser.ParseFrom(payload.ToArray());
            _combatStore.CaptureAuxiliaryProtocolMessage("NotifySocialData", message);
            _combatStore.ApplySocialScene(message.VRequest?.Data?.SceneData);
            return Processed("NotifySocialData", 0);
        }
        catch (InvalidProtocolBufferException)
        {
            return Invalid("NotifySocialData");
        }
    }

    private GameNotificationProcessOutcome ProcessEnterScene(ReadOnlySpan<byte> payload)
    {
        try
        {
            _combatStore.ApplyEnterScene(EnterScene.Parser.ParseFrom(payload.ToArray()));
            return Processed("EnterScene", 0);
        }
        catch (InvalidProtocolBufferException)
        {
            return Invalid("EnterScene");
        }
    }

    private GameNotificationProcessOutcome ProcessSyncNearEntities(ReadOnlySpan<byte> payload)
    {
        try
        {
            _combatStore.ApplyNearbyEntities(SyncNearEntities.Parser.ParseFrom(payload.ToArray()));
            return Processed("SyncNearEntities", 0);
        }
        catch (InvalidProtocolBufferException)
        {
            return Invalid("SyncNearEntities");
        }
    }

    private GameNotificationProcessOutcome ProcessSyncSceneEvents(ReadOnlySpan<byte> payload)
    {
        return Capture(payload, "SyncSceneEvents", SyncSceneEvents.Parser);
    }

    private GameNotificationProcessOutcome ProcessSyncContainerData(ReadOnlySpan<byte> payload)
    {
        _mapStateMachine.StartNewMap();
        try
        {
            var message = SyncContainerData.Parser.ParseFrom(payload.ToArray());
            if (message.VData is null)
            {
                return Invalid("SyncContainerData");
            }

            _combatStore.ApplyContainer(message.VData);
            return Processed("SyncContainerData", 0);
        }
        catch (InvalidProtocolBufferException)
        {
            return Invalid("SyncContainerData");
        }
    }

    private GameNotificationProcessOutcome ProcessSyncContainerDirtyData(ReadOnlySpan<byte> payload)
    {
        try
        {
            var message = SyncContainerDirtyData.Parser.ParseFrom(payload.ToArray());
            _combatStore.CaptureAuxiliaryProtocolMessage("SyncContainerDirtyData", message);
            if (message.VData?.Buffer is null || message.VData.Buffer.IsEmpty)
            {
                return Invalid("SyncContainerDirtyData");
            }

            var isStreamSafe = message.VData.StreamType == EStreamType.StreamTypeDeltaDirtySafe;
            var dirty = new DirtyContainer(new BlobReader(message.VData.Buffer.ToByteArray(), isStreamSafe));
            _combatStore.ApplyContainerDirty(dirty);
            return Processed("SyncContainerDirtyData", 0);
        }
        catch (InvalidProtocolBufferException)
        {
            return Invalid("SyncContainerDirtyData");
        }
    }

    private GameNotificationProcessOutcome ProcessSyncDungeonData(ReadOnlySpan<byte> payload, DateTimeOffset receivedAtUtc)
    {
        try
        {
            var message = SyncDungeonData.Parser.ParseFrom(payload.ToArray());
            if (message.VData is null)
            {
                return Invalid("SyncDungeonData");
            }

            _combatStore.ApplyDungeon(message.VData);
            if (message.VData.FlowInfo is not null)
            {
                _mapStateMachine.RecordDungeonState((int)message.VData.FlowInfo.State, receivedAtUtc);
            }

            return Processed("SyncDungeonData", 0);
        }
        catch (InvalidProtocolBufferException)
        {
            return Invalid("SyncDungeonData");
        }
    }

    private GameNotificationProcessOutcome ProcessSyncDungeonDirtyData(ReadOnlySpan<byte> payload, DateTimeOffset receivedAtUtc)
    {
        try
        {
            var message = SyncDungeonDirtyData.Parser.ParseFrom(payload.ToArray());
            _combatStore.CaptureAuxiliaryProtocolMessage("SyncDungeonDirtyData", message);
            if (message.VData?.Buffer is null || message.VData.Buffer.IsEmpty)
            {
                return Invalid("SyncDungeonDirtyData");
            }

            var isStreamSafe = message.VData.StreamType == EStreamType.StreamTypeDeltaDirtySafe;
            var dirty = new DirtyDungeon(new BlobReader(message.VData.Buffer.ToByteArray(), isStreamSafe));
            _combatStore.ApplyDungeonDirty(dirty);
            if (dirty.FlowInfo?.State is EDungeonState state)
            {
                _mapStateMachine.RecordDungeonState((int)state, receivedAtUtc);
            }

            return Processed("SyncDungeonDirtyData", 0);
        }
        catch (InvalidProtocolBufferException)
        {
            return Invalid("SyncDungeonDirtyData");
        }
    }

    private GameNotificationProcessOutcome ProcessSyncNearDeltaInfo(ReadOnlySpan<byte> payload, DateTimeOffset receivedAtUtc)
    {
        try
        {
            var eventCount = _combatStore.ApplyDeltas(SyncNearDeltaInfo.Parser.ParseFrom(payload.ToArray()), receivedAtUtc);
            return Processed("SyncNearDeltaInfo", eventCount);
        }
        catch (InvalidProtocolBufferException)
        {
            return Invalid("SyncNearDeltaInfo");
        }
    }

    private GameNotificationProcessOutcome ProcessSyncToMeDeltaInfo(ReadOnlySpan<byte> payload, DateTimeOffset receivedAtUtc)
    {
        try
        {
            var eventCount = _combatStore.ApplySelfDelta(SyncToMeDeltaInfo.Parser.ParseFrom(payload.ToArray()), receivedAtUtc);
            return Processed("SyncToMeDeltaInfo", eventCount);
        }
        catch (InvalidProtocolBufferException)
        {
            return Invalid("SyncToMeDeltaInfo");
        }
    }

    private GameNotificationProcessOutcome ProcessMatchNotification(uint methodId, ReadOnlySpan<byte> payload)
    {
        return (MatchNotification)methodId switch
        {
            MatchNotification.EnterMatchResult => Capture(payload, "EnterMatchResult", Zproto.MatchNtf.Types.EnterMatchResultNtf.Parser),
            MatchNotification.CancelMatchResult => Capture(payload, "CancelMatchResult", Zproto.MatchNtf.Types.CancelMatchResultNtf.Parser),
            MatchNotification.MatchReadyStatus => Capture(payload, "MatchReadyStatus", Zproto.MatchNtf.Types.MatchReadyStatusNtf.Parser),
            _ => Ignored((ulong)EServiceId.MatchNtf, methodId)
        };
    }

    private GameNotificationProcessOutcome ProcessTeamNotification(uint methodId, ReadOnlySpan<byte> payload)
    {
        return (TeamNotification)methodId switch
        {
            TeamNotification.NoticeUpdateTeamInfo => Capture(payload, "NoticeUpdateTeamInfo", Zproto.GrpcTeamNtf.Types.NoticeUpdateTeamInfo.Parser),
            TeamNotification.NoticeUpdateTeamMemberInfo => Capture(payload, "NoticeUpdateTeamMemberInfo", Zproto.GrpcTeamNtf.Types.NoticeUpdateTeamMemberInfo.Parser),
            TeamNotification.NotifyJoinTeam => Capture(payload, "NotifyJoinTeam", Zproto.GrpcTeamNtf.Types.NotifyJoinTeam.Parser),
            TeamNotification.NotifyLeaveTeam => Capture(payload, "NotifyLeaveTeam", Zproto.GrpcTeamNtf.Types.NotifyLeaveTeam.Parser),
            TeamNotification.NotifyBeTransferLeader => Capture(payload, "NotifyBeTransferLeader", Zproto.GrpcTeamNtf.Types.NotifyBeTransferLeader.Parser),
            TeamNotification.NoticeTeamDissolve => Capture(payload, "NoticeTeamDissolve", Zproto.GrpcTeamNtf.Types.NoticeTeamDissolve.Parser),
            TeamNotification.NotifyTeamActivityState => Capture(payload, "NotifyTeamActivityState", Zproto.GrpcTeamNtf.Types.NotifyTeamActivityState.Parser),
            TeamNotification.TeamActivityResult => Capture(payload, "TeamActivityResult", Zproto.GrpcTeamNtf.Types.TeamActivityResult.Parser),
            TeamNotification.TeamActivityListResult => Capture(payload, "TeamActivityListResult", Zproto.GrpcTeamNtf.Types.TeamActivityListResult.Parser),
            TeamNotification.TeamActivityVoteResult => Capture(payload, "TeamActivityVoteResult", Zproto.GrpcTeamNtf.Types.TeamActivityVoteResult.Parser),
            TeamNotification.NotifyCharMatchResult => Capture(payload, "NotifyCharMatchResult", Zproto.GrpcTeamNtf.Types.NotifyCharMatchResult.Parser),
            TeamNotification.NotifyTeamMatchResult => Capture(payload, "NotifyTeamMatchResult", Zproto.GrpcTeamNtf.Types.NotifyTeamMatchResult.Parser),
            TeamNotification.NotifyCharAbortMatch => Capture(payload, "NotifyCharAbortMatch", Zproto.GrpcTeamNtf.Types.NotifyCharAbortMatch.Parser),
            TeamNotification.UpdateTeamMemBeCall => Capture(payload, "UpdateTeamMemBeCall", Zproto.GrpcTeamNtf.Types.UpdateTeamMemBeCall.Parser),
            TeamNotification.NotifyTeamMemBeCall => Capture(payload, "NotifyTeamMemBeCall", Zproto.GrpcTeamNtf.Types.NotifyTeamMemBeCall.Parser),
            TeamNotification.NotifyTeamMemBeCallResult => Capture(payload, "NotifyTeamMemBeCallResult", Zproto.GrpcTeamNtf.Types.NotifyTeamMemBeCallResult.Parser),
            TeamNotification.NotifyTeamEnterErr => Capture(payload, "NotifyTeamEnterErr", Zproto.GrpcTeamNtf.Types.NotifyTeamEnterErr.Parser),
            _ => Ignored((ulong)EServiceId.GrpcTeamNtf, methodId)
        };
    }

    private GameNotificationProcessOutcome Capture<T>(ReadOnlySpan<byte> payload, string source, MessageParser<T> parser)
        where T : class, IMessage<T>
    {
        try
        {
            _combatStore.CaptureAuxiliaryProtocolMessage(source, parser.ParseFrom(payload.ToArray()));
            return Processed(source, 0);
        }
        catch (InvalidProtocolBufferException)
        {
            return Invalid(source);
        }
    }

    private static GameNotificationProcessOutcome Ignored(ulong serviceId, uint methodId)
    {
        return new GameNotificationProcessOutcome(
            GameNotificationProcessResult.Ignored,
            $"ServiceId={serviceId},MethodId={methodId}",
            PlayerRosterStore.Instance.Current.Entries.Count,
            0);
    }

    private static GameNotificationProcessOutcome Invalid(string source)
    {
        return new GameNotificationProcessOutcome(
            GameNotificationProcessResult.InvalidPayload,
            source,
            PlayerRosterStore.Instance.Current.Entries.Count,
            0);
    }

    private static GameNotificationProcessOutcome Processed(string source, int combatEventCount)
    {
        return new GameNotificationProcessOutcome(
            GameNotificationProcessResult.Processed,
            source,
            PlayerRosterStore.Instance.Current.Entries.Count,
            combatEventCount);
    }
}
