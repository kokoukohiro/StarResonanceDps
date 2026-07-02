using StarResonanceDps.Core.CombatRuntime.Protocols;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Zproto;

namespace StarResonanceDps.Core.CombatRuntime
{
    public static class GrpcTeamManager
    {
        public static void ProcessNoticeUpdateTeamInfo(GrpcTeamNtf.Types.NoticeUpdateTeamInfo vData, ExtraPacketData extraData)
        {
            AppState.PartyTeamId = vData.VRequest.BaseInfo.TeamId;
            if (AppState.PlayerUUID != 0 && EncounterManager.Current != null)
            {
                EncounterManager.Current.SetAttrKV(AppState.PlayerUUID, "AttrTeamId", vData.VRequest.BaseInfo.TeamId);
            }
        }

        public static void ProcessNoticeUpdateTeamMemberInfo(GrpcTeamNtf.Types.NoticeUpdateTeamMemberInfo vData, ExtraPacketData extraData)
        {

        }

        public static void ProcessNotifyJoinTeam(GrpcTeamNtf.Types.NotifyJoinTeam vData, ExtraPacketData extraData)
        {
            AppState.PartyTeamId = vData.VRequest.BaseInfo.TeamId;
            if (AppState.PlayerUUID != 0 && EncounterManager.Current != null)
            {
                EncounterManager.Current.SetAttrKV(AppState.PlayerUUID, "AttrTeamId", vData.VRequest.BaseInfo.TeamId);
            }

            foreach (var member in vData.VRequest.MemberData)
            {
                long uuid = Utils.EntityIdToUuid(member.CharId, (long)EEntityType.EntChar, false, false);

                var cached = EntityCache.Instance.GetOrCreate(uuid);
                if (cached != null)
                {

                    if (member.SocialData.BasicData != null)
                    {
                        string name = member.SocialData.BasicData.Name;
                        if (!string.IsNullOrEmpty(name))
                        {
                            cached.Name = member.SocialData.BasicData.Name;
                        }

                        int level = member.SocialData.BasicData.Level;
                        if (level > 0)
                        {
                            cached.Level = level;
                        }
                    }

                    if (member.SocialData.UserAttrData != null)
                    {
                        long abilityscore = member.SocialData.UserAttrData.FightPoint;
                        if (abilityscore > 0)
                        {
                            cached.AbilityScore = (int)abilityscore;
                        }
                    }

                    if (member.SocialData.ProfessionData != null)
                    {
                        int professionId = member.SocialData.ProfessionData.ProfessionId;
                        if (professionId > 0)
                        {
                            cached.ProfessionId = professionId;
                        }
                    }
                }
            }
        }

        public static void ProcessNotifyLeaveTeam(GrpcTeamNtf.Types.NotifyLeaveTeam vData, ExtraPacketData extraData)
        {
            if (vData.VRequest.CharId == AppState.PlayerUID)
            {
                AppState.PartyTeamId = 0;
                if (AppState.PlayerUUID != 0 && EncounterManager.Current != null)
                {
                    EncounterManager.Current.SetAttrKV(AppState.PlayerUUID, "AttrTeamId", 0);
                }
            }
        }

        public static void ProcessNoticeTeamDissolve(GrpcTeamNtf.Types.NoticeTeamDissolve vData, ExtraPacketData extraData)
        {
            AppState.PartyTeamId = 0;
            if (AppState.PlayerUUID != 0 && EncounterManager.Current != null)
            {
                EncounterManager.Current.SetAttrKV(AppState.PlayerUUID, "AttrTeamId", 0);
            }
        }

        public static void ProcessNotifyBeTransferLeader(GrpcTeamNtf.Types.NotifyBeTransferLeader vData, ExtraPacketData extraData)
        {
            AppState.PartyTeamId = vData.VRequest.LeaderData.TeamData.TeamId;
            if (AppState.PlayerUUID != 0 && EncounterManager.Current != null)
            {
                EncounterManager.Current.SetAttrKV(AppState.PlayerUUID, "AttrTeamId", vData.VRequest.LeaderData.TeamData.TeamId);
            }
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
    }
}
