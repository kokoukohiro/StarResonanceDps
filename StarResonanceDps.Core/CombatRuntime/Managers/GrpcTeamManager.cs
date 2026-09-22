using StarResonanceDps.Core.CombatRuntime.Protocols;
using StarResonanceDps.Core.Models;
using StarResonanceDps.Core.Services;
using System;
using System.Collections.Generic;
using System.Linq;
using Zproto;

namespace StarResonanceDps.Core.CombatRuntime
{
    public static class GrpcTeamManager
    {
        private static readonly PartyStateStore PartyState = PartyStateStore.Instance;
        private static ETeamMemberType? _currentTeamMemberType;

        internal static void ResetMemberState()
        {
            AppState.PartyTeamId = 0;
            _currentTeamMemberType = null;
            PartyState.ResetUnknown();
        }

        internal static void ProcessEntityTeamId(long entityUuid, long teamId)
        {
            var characterId = Utils.UuidToEntityId(entityUuid);
            var isSelf = entityUuid == MessageManager.currentUserUuid
                || entityUuid == AppState.PlayerUUID
                || (AppState.PlayerUID != 0 && characterId == AppState.PlayerUID);
            var party = PartyState.Current;

            if (isSelf)
            {
                var hasKnownPartyMember = party.MemberIds.Any(memberId => memberId != characterId);
                if (teamId == 0
                    && (AppState.PartyTeamId > 0 || party.TeamId > 0 || hasKnownPartyMember))
                {
                    PlayerRosterProjection.RebuildRoster();
                    return;
                }

                if (teamId == 0 || (AppState.PartyTeamId != 0 && AppState.PartyTeamId != teamId))
                {
                    _currentTeamMemberType = null;
                }

                AppState.PartyTeamId = teamId;
                if (teamId == 0)
                {
                    PartyState.SetNoParty();
                }
                else
                {
                    PartyState.ApplyKnownMembers(teamId, [characterId]);
                }

                PlayerRosterProjection.RebuildRoster();
                return;
            }

            if (!party.HasCompleteMembership)
            {
                if (teamId != 0 && teamId == AppState.PartyTeamId)
                {
                    PartyState.ApplyKnownMembers(teamId, [characterId]);
                }
                else if (AppState.PartyTeamId != 0)
                {
                    PartyState.MarkNonMember(characterId);
                }

                PlayerRosterProjection.RebuildRoster();
                return;
            }

            PlayerRosterProjection.UpsertPlayer(entityUuid);
        }

        internal static void ProcessGetTeamInfo(GetTeamInfoReply reply, ExtraPacketData extraData)
        {
            if (reply.ErrCode != EErrorCode.ErrSuccess)
            {
                return;
            }

            var baseInfo = reply.BaseInfo;
            if (baseInfo == null || baseInfo.TeamId <= 0)
            {
                ResetMemberState();
                PartyState.SetNoParty();
                PlayerRosterProjection.RebuildRoster();
                return;
            }

            var teamId = baseInfo.TeamId;
            if (AppState.PartyTeamId != 0 && AppState.PartyTeamId != teamId)
            {
                ResetMemberState();
            }

            AppState.PartyTeamId = teamId;
            _currentTeamMemberType = baseInfo.TeamMemberType;

            var groups = baseInfo.TeamMemberGroupInfos.Values.ToArray();
            var memberIds = groups
                .SelectMany(group => group.CharIds)
                .Concat(reply.MemberData.Select(member => member.CharId))
                .Concat(reply.MemberFastSyncData.Keys)
                .Where(characterId => characterId > 0)
                .Distinct()
                .ToArray();
            PartyState.ApplyCompleteMembership(
                teamId,
                _currentTeamMemberType == ETeamMemberType.Five,
                memberIds);
            PartyState.ApplyMemberEnterTimes(
                teamId,
                reply.MemberData.Select(member => (member.CharId, member.EnterTime)));

            foreach (var member in reply.MemberData)
            {
                ApplyTeamMemberSocialData(member, "GetTeamInfo");
            }

            foreach (var memberSyncData in reply.MemberFastSyncData)
            {
                ApplyTeamMemberFastSyncData(memberSyncData.Key, memberSyncData.Value);
            }

            if (groups.Length > 0)
            {
                ApplyAuthoritativeMembership(teamId, _currentTeamMemberType, groups);
            }
            PublishCurrentTeam(teamId);
        }

        internal static void ProcessSocialTeamData(SocialData? socialData)
        {
            var teamData = socialData?.TeamData;
            if (socialData == null
                || AppState.PlayerUID == 0
                || socialData.CharId != AppState.PlayerUID
                || teamData == null
                || teamData.TeamId <= 0
                || !teamData.CharIds.Contains(AppState.PlayerUID))
            {
                return;
            }

            var teamId = teamData.TeamId;
            if (AppState.PartyTeamId != 0 && AppState.PartyTeamId != teamId)
            {
                ResetMemberState();
            }

            AppState.PartyTeamId = teamId;
            _currentTeamMemberType = teamData.TeamMemberType;

            var memberIds = teamData.CharIds
                .Where(characterId => characterId > 0)
                .Distinct()
                .ToArray();
            var memberIdSet = memberIds.ToHashSet();
            var groupAssignments = teamData.TeamMemberData.Values
                .Where(member => memberIdSet.Contains(member.CharId) && member.GroupId > 0)
                .Select(member => (member.CharId, member.GroupId));
            PartyState.ApplyCompleteMembership(
                teamId,
                _currentTeamMemberType == ETeamMemberType.Five,
                memberIds,
                groupAssignments);
            PartyState.ApplyMemberEnterTimes(
                teamId,
                teamData.TeamMemberData.Values
                    .Where(member => memberIdSet.Contains(member.CharId))
                    .Select(member => (member.CharId, member.EnterTime)));
            foreach (var member in teamData.TeamMemberData.Values)
            {
                if (memberIdSet.Contains(member.CharId))
                {
                    ApplyTeamMemberSocialData(member, "NotifySocialData");
                }
            }

            PublishCurrentTeam(teamId);
        }

        public static void ProcessNoticeUpdateTeamInfo(GrpcTeamNtf.Types.NoticeUpdateTeamInfo vData, ExtraPacketData extraData)
        {
            var teamId = vData.VRequest.BaseInfo.TeamId;
            if (AppState.PartyTeamId != 0 && AppState.PartyTeamId != teamId)
            {
                ResetMemberState();
            }

            AppState.PartyTeamId = teamId;
            _currentTeamMemberType = vData.VRequest.BaseInfo.TeamMemberType;
            ApplyAuthoritativeMembership(
                teamId,
                _currentTeamMemberType,
                vData.VRequest.BaseInfo.TeamMemberGroupInfos.Values);
            PlayerRosterProjection.RebuildRoster();
            if (AppState.PlayerUUID != 0 && EncounterManager.Current != null)
            {
                EncounterManager.Current.SetAttrKV(AppState.PlayerUUID, "AttrTeamId", teamId);
            }
        }

        public static void ProcessNoticeUpdateTeamMemberInfo(GrpcTeamNtf.Types.NoticeUpdateTeamMemberInfo vData, ExtraPacketData extraData)
        {
            PartyState.ApplyKnownMembers(
                AppState.PartyTeamId,
                vData.VRequest.TeamMemberSocialDatas.Select(member => member.CharId)
                    .Concat(vData.VRequest.TeamMemberSyncDatas.Select(member => member.CharId)));
            PartyState.ApplyMemberEnterTimes(
                AppState.PartyTeamId,
                vData.VRequest.TeamMemberSocialDatas.Select(member => (member.CharId, member.EnterTime)));

            foreach (var member in vData.VRequest.TeamMemberSocialDatas)
            {
                ApplyTeamMemberSocialData(member, "NoticeUpdateTeamMemberInfo");
            }

            foreach (var fastSyncData in vData.VRequest.TeamMemberSyncDatas)
            {
                ApplyTeamMemberFastSyncData(
                    fastSyncData.CharId,
                    fastSyncData);
            }

            PlayerRosterProjection.RebuildRoster();
        }

        public static void ProcessNotifyJoinTeam(GrpcTeamNtf.Types.NotifyJoinTeam vData, ExtraPacketData extraData)
        {
            var teamId = vData.VRequest.BaseInfo.TeamId;
            if (AppState.PartyTeamId != 0 && AppState.PartyTeamId != teamId)
            {
                ResetMemberState();
            }

            AppState.PartyTeamId = teamId;
            _currentTeamMemberType = vData.VRequest.BaseInfo.TeamMemberType;

            var groups = vData.VRequest.BaseInfo.TeamMemberGroupInfos.Values.ToArray();
            PartyState.ApplyCompleteMembership(
                teamId,
                _currentTeamMemberType == ETeamMemberType.Five,
                groups.SelectMany(group => group.CharIds));
            PartyState.ApplyMemberEnterTimes(
                teamId,
                vData.VRequest.MemberData.Select(member => (member.CharId, member.EnterTime)));

            foreach (var member in vData.VRequest.MemberData)
            {
                ApplyTeamMemberSocialData(member, "NotifyJoinTeam");
            }

            foreach (var memberSyncData in vData.VRequest.MemberSyncDatas)
            {
                ApplyTeamMemberFastSyncData(
                    memberSyncData.Key,
                    memberSyncData.Value);
            }

            ApplyAuthoritativeMembership(
                teamId,
                _currentTeamMemberType,
                groups);
            PublishCurrentTeam(teamId);
        }

        public static void ProcessNotifyLeaveTeam(GrpcTeamNtf.Types.NotifyLeaveTeam vData, ExtraPacketData extraData)
        {
            if (vData.VRequest.CharId > 0)
            {
                PartyState.RemoveMember(vData.VRequest.CharId);
            }

            if (vData.VRequest.CharId == AppState.PlayerUID)
            {
                ResetMemberState();
                AppState.PartyTeamId = 0;
                PartyState.SetNoParty();
                if (AppState.PlayerUUID != 0 && EncounterManager.Current != null)
                {
                    EncounterManager.Current.SetAttrKV(AppState.PlayerUUID, "AttrTeamId", 0);
                }
            }

            PlayerRosterProjection.RebuildRoster();
        }

        public static void ProcessNoticeTeamDissolve(GrpcTeamNtf.Types.NoticeTeamDissolve vData, ExtraPacketData extraData)
        {
            ResetMemberState();
            AppState.PartyTeamId = 0;
            PartyState.SetNoParty();
            if (AppState.PlayerUUID != 0 && EncounterManager.Current != null)
            {
                EncounterManager.Current.SetAttrKV(AppState.PlayerUUID, "AttrTeamId", 0);
            }

            PlayerRosterProjection.RebuildRoster();
        }

        public static void ProcessNotifyBeTransferLeader(GrpcTeamNtf.Types.NotifyBeTransferLeader vData, ExtraPacketData extraData)
        {
            var teamId = vData.VRequest.LeaderData.TeamData.TeamId;
            if (AppState.PartyTeamId != 0 && AppState.PartyTeamId != teamId)
            {
                ResetMemberState();
            }

            AppState.PartyTeamId = teamId;
            _currentTeamMemberType = vData.VRequest.LeaderData.TeamData.TeamMemberType;
            PartyState.ApplyKnownMembers(teamId, vData.VRequest.LeaderData.TeamData.CharIds);
            PlayerRosterProjection.RebuildRoster();
            if (AppState.PlayerUUID != 0 && EncounterManager.Current != null)
            {
                EncounterManager.Current.SetAttrKV(AppState.PlayerUUID, "AttrTeamId", teamId);
            }
        }

        public static void ProcessNotifyTeamGroupUpdate(GrpcTeamNtf.Types.NotifyTeamGroupUpdate vData, ExtraPacketData extraData)
        {
            if (vData.VRequest.ErrCode != EErrorCode.ErrSuccess)
            {
                return;
            }

            ApplyAuthoritativeMembership(
                AppState.PartyTeamId,
                _currentTeamMemberType,
                vData.VRequest.TeamMemberGroupInfos.Values);
            PlayerRosterProjection.RebuildRoster();
        }

        public static void ProcessNotifyTeamActivityState(GrpcTeamNtf.Types.NotifyTeamActivityState vData, ExtraPacketData extraData)
        {
            if (vData.VRequest.State.State == ETeamActivityState.EteamActivityVoting)
            {

                if (vData.VRequest.State.AssignSceneParams.CreatorCharId == AppState.PlayerUID)
                {

                    System.Diagnostics.Debug.WriteLine("Current Player is TeamActivity creator");
                }
                else
                {

                    System.Diagnostics.Debug.WriteLine("Current Player is TeamActivity member and needs to vote");
                }
            }
            else if (vData.VRequest.State.State == ETeamActivityState.EteamActivityNo)
            {

                System.Diagnostics.Debug.WriteLine("ProcessNotifyTeamActivityState State is No, activity vote state ended");
            }
        }

        public static void ProcessTeamActivityResult(GrpcTeamNtf.Types.TeamActivityResult vData, ExtraPacketData extraData)
        {

        }

        public static void ProcessTeamActivityListResult(GrpcTeamNtf.Types.TeamActivityListResult vData, ExtraPacketData extraData)
        {

        }

        public static void ProcessTeamActivityVoteResult(GrpcTeamNtf.Types.TeamActivityVoteResult vData, ExtraPacketData extraData)
        {
            if (vData.VRequest.VCharId == AppState.PlayerUID)
            {
                if (vData.VRequest.Code == ETeamVoteRet.Agree)
                {

                }
                else
                {

                }
            }
        }

        public static void ProcessNotifyCharMatchResult(ReadOnlySpan<byte> payloadBuffer, ExtraPacketData extraData)
        {

        }

        public static void ProcessNotifyTeamMatchResult(ReadOnlySpan<byte> payloadBuffer, ExtraPacketData extraData)
        {

        }

        public static void ProcessNotifyCharAbortMatch(ReadOnlySpan<byte> payloadBuffer, ExtraPacketData extraData)
        {

        }

        public static void ProcessNotifyTeamEnterErr(ReadOnlySpan<byte> payloadBuffer, ExtraPacketData extraData)
        {

        }

        /// <summary>
        /// NotifySocialData に同梱されるメンバーの social data はサーバ側の非正規化スナップショットで、
        /// <b>メンバーがクラスを変更しても追従しない。</b>AOI や GetTeamInfo が現在値を返していても、
        /// この経路だけ変更前の職業を送り続けて表示を巻き戻す。
        /// より確かなソースから取得済みなら採用せず、まだ何も無いときだけ初期値として使う。
        /// </summary>
        private const string StaleSocialDataSource = "NotifySocialData";

        private static void ApplyTeamMemberSocialData(TeamMemData member, string source)
        {
            if (member.CharId <= 0 || member.SocialData == null)
            {
                return;
            }

            var isTrustedSource = !string.Equals(source, StaleSocialDataSource, StringComparison.Ordinal);
            if (!isTrustedSource
                && PartyState.Current.TryGetSupplement(member.CharId, out var existing)
                && existing.HasTrustedSocialData)
            {
                return;
            }

            var socialData = member.SocialData;
            var receivedEquipment = socialData.EquipData != null
                && socialData.EquipData.EquipInfos.Count > 0
                    ? PlayerEquipmentData.Create(
                        socialData.EquipData.EquipInfos.Select(
                            item => new PlayerEquipmentItem(item.Slot, item.EquipID)))
                    : null;

            PartyState.UpdateSupplement(
                AppState.PartyTeamId,
                member.CharId,
                current => current with
                {
                    Name = !string.IsNullOrEmpty(socialData.BasicData?.Name)
                        ? socialData.BasicData.Name
                        : current.Name,
                    Level = socialData.BasicData?.Level > 0
                        ? socialData.BasicData.Level
                        : current.Level,
                    SeasonLevel = socialData.BasicData?.SeasonLevel > 0
                        ? socialData.BasicData.SeasonLevel
                        : current.SeasonLevel,
                    CombatPower = socialData.UserAttrData?.FightPoint > 0
                        ? ToInt32Saturating(socialData.UserAttrData.FightPoint)
                        : current.CombatPower,
                    SeasonStrength = socialData.UserAttrData?.SeasonStrength > 0
                        ? socialData.UserAttrData.SeasonStrength
                        : current.SeasonStrength,
                    ProfessionId = socialData.ProfessionData?.ProfessionId > 0
                        ? socialData.ProfessionData.ProfessionId
                        : current.ProfessionId,
                    EquipmentData = receivedEquipment ?? current.EquipmentData,
                    IsNpc = socialData.BasicData != null
                        ? socialData.BasicData.BotAiId > 0
                        : current.IsNpc,
                    HasTrustedSocialData = current.HasTrustedSocialData || isTrustedSource
                });
        }

        private static void ApplyTeamMemberFastSyncData(
            long fallbackCharId,
            TeamMemberFastSyncData fastSyncData)
        {
            var charId = fastSyncData.CharId > 0 ? fastSyncData.CharId : fallbackCharId;
            if (charId <= 0)
            {
                return;
            }

            // 死亡状態の同期は HP を運んでいても 0 として控える。ダメージの無い即死では、
            // サーバは死亡中も死ぬ前の HP を送ってくる。
            var isDead = fastSyncData.State == (int)EActorState.ActorStateDead;

            PartyState.UpdateSupplement(
                AppState.PartyTeamId,
                charId,
                current => current with
                {
                    CurrentHp = isDead
                        ? 0
                        : fastSyncData.Hp > 0 || fastSyncData.MaxHp > 0
                            ? Math.Max(fastSyncData.Hp, 0)
                            : current.CurrentHp,
                    MaxHp = fastSyncData.MaxHp > 0 ? fastSyncData.MaxHp : current.MaxHp
                });
        }

        private static void ApplyAuthoritativeMembership(
            long teamId,
            ETeamMemberType? teamMemberType,
            IEnumerable<TeamMemberGroupInfo> groups)
        {
            ApplyAuthoritativeMembership(
                teamId,
                teamMemberType,
                groups.Select(group => (
                    group.GroupId,
                    CharacterIds: (IEnumerable<long>)group.CharIds)));
        }

        private static void ApplyAuthoritativeMembership(
            long teamId,
            ETeamMemberType? teamMemberType,
            IEnumerable<(int GroupId, IEnumerable<long> CharacterIds)> groups)
        {
            var materializedGroups = groups
                .Select(group => (
                    group.GroupId,
                    CharacterIds: group.CharacterIds.ToArray()))
                .ToArray();

            PartyState.ApplyAuthoritativeMembership(
                teamId,
                teamMemberType == ETeamMemberType.Five,
                materializedGroups.Select(group => (
                    group.GroupId,
                    CharacterIds: (IEnumerable<long>)group.CharacterIds)));
        }

        private static void PublishCurrentTeam(long teamId)
        {
            PlayerRosterProjection.RebuildRoster();
            if (AppState.PlayerUUID != 0 && EncounterManager.Current != null)
            {
                EncounterManager.Current.SetAttrKV(AppState.PlayerUUID, "AttrTeamId", teamId);
            }
        }

        private static int ToInt32Saturating(long value)
        {
            return value > int.MaxValue ? int.MaxValue : (int)value;
        }
    }
}
