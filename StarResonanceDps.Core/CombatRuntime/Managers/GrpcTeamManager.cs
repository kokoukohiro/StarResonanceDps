using StarResonanceDps.Core.CombatRuntime.Protocols;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using Zproto;

namespace StarResonanceDps.Core.CombatRuntime
{
    public static class GrpcTeamManager
    {
        private const string TeamFastSyncSceneIdAttribute = "TeamMemberFastSyncSceneId";
        private const string TeamFastSyncStateAttribute = "TeamMemberFastSyncState";
        private const string TeamFastSyncSceneAreaIdAttribute = "TeamMemberFastSyncSceneAreaId";
        private const string TeamFastSyncDirectionAttribute = "TeamMemberFastSyncDirection";
        private const string TeamFastSyncPositionXAttribute = "TeamMemberFastSyncPositionX";
        private const string TeamFastSyncPositionYAttribute = "TeamMemberFastSyncPositionY";
        private const string TeamFastSyncPositionZAttribute = "TeamMemberFastSyncPositionZ";
        private const string TeamFastSyncHpAttribute = "TeamMemberFastSyncHp";
        private const string TeamFastSyncMaxHpAttribute = "TeamMemberFastSyncMaxHp";
        private const string TeamMemberBotAiIdAttribute = "TeamMemberBotAiId";

        private static readonly ConcurrentDictionary<long, TeamMemberFastSyncData> PendingFastSyncData = new();
        private static readonly ConcurrentDictionary<long, EquipNine[]> PendingEquipmentData = new();
        private static readonly ConcurrentDictionary<long, uint> KnownTeamBotAiIds = new();

        internal static void ResetMemberState()
        {
            PendingFastSyncData.Clear();
            PendingEquipmentData.Clear();
            KnownTeamBotAiIds.Clear();
        }

        internal static bool IsKnownTeamNpc(long entityUuid)
        {
            return entityUuid != 0
                && KnownTeamBotAiIds.TryGetValue(entityUuid, out var botAiId)
                && botAiId > 0;
        }

        internal static void ApplyKnownMemberData(long entityUuid, Entity entity)
        {
            if (!IsLiveOtherPlayer(entityUuid, entity))
            {
                return;
            }

            if (KnownTeamBotAiIds.TryGetValue(entityUuid, out var botAiId) && botAiId > 0)
            {
                SetSourceAttributeIfChanged(entity, TeamMemberBotAiIdAttribute, botAiId);
            }

            ApplyPendingMemberData(entityUuid, entity);
        }

        public static void ProcessNoticeUpdateTeamInfo(GrpcTeamNtf.Types.NoticeUpdateTeamInfo vData, ExtraPacketData extraData)
        {
            var teamId = vData.VRequest.BaseInfo.TeamId;
            if (AppState.PartyTeamId != 0 && AppState.PartyTeamId != teamId)
            {
                ResetMemberState();
            }

            AppState.PartyTeamId = teamId;
            if (AppState.PlayerUUID != 0 && EncounterManager.Current != null)
            {
                EncounterManager.Current.SetAttrKV(AppState.PlayerUUID, "AttrTeamId", teamId);
            }
        }

        public static void ProcessNoticeUpdateTeamMemberInfo(GrpcTeamNtf.Types.NoticeUpdateTeamMemberInfo vData, ExtraPacketData extraData)
        {
            HashSet<long> rosterPlayersToUpsert = [];

            foreach (var member in vData.VRequest.TeamMemberSocialDatas)
            {
                ApplyTeamMemberSocialData(member, rosterPlayersToUpsert);
            }

            foreach (var fastSyncData in vData.VRequest.TeamMemberSyncDatas)
            {
                ApplyTeamMemberFastSyncData(
                    fastSyncData.CharId,
                    fastSyncData,
                    rosterPlayersToUpsert);
            }

            PublishRosterChanges(rosterPlayersToUpsert);
        }

        public static void ProcessNotifyJoinTeam(GrpcTeamNtf.Types.NotifyJoinTeam vData, ExtraPacketData extraData)
        {
            var teamId = vData.VRequest.BaseInfo.TeamId;
            if (AppState.PartyTeamId != 0 && AppState.PartyTeamId != teamId)
            {
                ResetMemberState();
            }

            AppState.PartyTeamId = teamId;
            if (AppState.PlayerUUID != 0 && EncounterManager.Current != null)
            {
                EncounterManager.Current.SetAttrKV(AppState.PlayerUUID, "AttrTeamId", teamId);
            }

            HashSet<long> rosterPlayersToUpsert = [];

            foreach (var member in vData.VRequest.MemberData)
            {
                ApplyTeamMemberSocialData(member, rosterPlayersToUpsert);
            }

            foreach (var memberSyncData in vData.VRequest.MemberSyncDatas)
            {
                ApplyTeamMemberFastSyncData(
                    memberSyncData.Key,
                    memberSyncData.Value,
                    rosterPlayersToUpsert);
            }

            PublishRosterChanges(rosterPlayersToUpsert);
        }

        public static void ProcessNotifyLeaveTeam(GrpcTeamNtf.Types.NotifyLeaveTeam vData, ExtraPacketData extraData)
        {
            var leavingUuid = vData.VRequest.CharId > 0
                ? Utils.EntityIdToUuid(vData.VRequest.CharId, (long)EEntityType.EntChar, false, false)
                : 0;

            if (leavingUuid != 0)
            {
                PendingFastSyncData.TryRemove(leavingUuid, out _);
                PendingEquipmentData.TryRemove(leavingUuid, out _);
                KnownTeamBotAiIds.TryRemove(leavingUuid, out _);
            }

            if (vData.VRequest.CharId == AppState.PlayerUID)
            {
                ResetMemberState();
                AppState.PartyTeamId = 0;
                if (AppState.PlayerUUID != 0 && EncounterManager.Current != null)
                {
                    EncounterManager.Current.SetAttrKV(AppState.PlayerUUID, "AttrTeamId", 0);
                }
            }
        }

        public static void ProcessNoticeTeamDissolve(GrpcTeamNtf.Types.NoticeTeamDissolve vData, ExtraPacketData extraData)
        {
            ResetMemberState();
            AppState.PartyTeamId = 0;
            if (AppState.PlayerUUID != 0 && EncounterManager.Current != null)
            {
                EncounterManager.Current.SetAttrKV(AppState.PlayerUUID, "AttrTeamId", 0);
            }
        }

        public static void ProcessNotifyBeTransferLeader(GrpcTeamNtf.Types.NotifyBeTransferLeader vData, ExtraPacketData extraData)
        {
            var teamId = vData.VRequest.LeaderData.TeamData.TeamId;
            if (AppState.PartyTeamId != 0 && AppState.PartyTeamId != teamId)
            {
                ResetMemberState();
            }

            AppState.PartyTeamId = teamId;
            if (AppState.PlayerUUID != 0 && EncounterManager.Current != null)
            {
                EncounterManager.Current.SetAttrKV(AppState.PlayerUUID, "AttrTeamId", teamId);
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

        private static void ApplyTeamMemberSocialData(TeamMemData member, HashSet<long> rosterPlayersToUpsert)
        {
            if (member.CharId <= 0 || member.SocialData == null)
            {
                return;
            }

            var entityUuid = Utils.EntityIdToUuid(member.CharId, (long)EEntityType.EntChar, false, false);
            if (entityUuid == 0)
            {
                return;
            }

            var socialData = member.SocialData;
            var cached = EntityCache.Instance.GetOrCreate(entityUuid);
            var npcEvidenceChanged = false;

            if (socialData.BasicData != null)
            {
                if (socialData.BasicData.BotAiId > 0)
                {
                    npcEvidenceChanged = !KnownTeamBotAiIds.TryGetValue(entityUuid, out var knownBotAiId)
                        || knownBotAiId != socialData.BasicData.BotAiId;
                    KnownTeamBotAiIds[entityUuid] = socialData.BasicData.BotAiId;
                }

                var name = socialData.BasicData.Name;
                if (!string.IsNullOrEmpty(name))
                {
                    cached.Name = name;
                }

                if (socialData.BasicData.Level > 0)
                {
                    cached.Level = socialData.BasicData.Level;
                }

                if (socialData.BasicData.SeasonLevel > 0)
                {
                    cached.SeasonLevel = socialData.BasicData.SeasonLevel;
                }
            }

            if (socialData.UserAttrData != null && socialData.UserAttrData.FightPoint > 0)
            {
                cached.AbilityScore = ToInt32Saturating(socialData.UserAttrData.FightPoint);
            }

            if (socialData.ProfessionData != null && socialData.ProfessionData.ProfessionId > 0)
            {
                cached.ProfessionId = socialData.ProfessionData.ProfessionId;
            }

            var receivedEquipment = socialData.EquipData != null
                && socialData.EquipData.EquipInfos.Count > 0
                    ? CloneEquipment(socialData.EquipData.EquipInfos)
                    : null;

            if (!TryGetLiveOtherPlayer(entityUuid, out var entity))
            {
                if (receivedEquipment != null)
                {
                    PendingEquipmentData[entityUuid] = receivedEquipment;
                }

                return;
            }

            var rosterChanged = false;
            var encounter = EncounterManager.Current!;

            if (socialData.BasicData != null)
            {
                if (socialData.BasicData.BotAiId > 0)
                {
                    SetSourceAttributeIfChanged(
                        entity,
                        TeamMemberBotAiIdAttribute,
                        socialData.BasicData.BotAiId);
                }

                if (!string.IsNullOrEmpty(socialData.BasicData.Name))
                {
                    rosterChanged |= SetAttributeIfChanged(
                        encounter,
                        entity,
                        "AttrName",
                        socialData.BasicData.Name);
                }

                if (socialData.BasicData.Level > 0)
                {
                    rosterChanged |= SetAttributeIfChanged(
                        encounter,
                        entity,
                        "AttrLevel",
                        socialData.BasicData.Level);
                }

                if (socialData.BasicData.SeasonLevel > 0)
                {
                    rosterChanged |= SetAttributeIfChanged(
                        encounter,
                        entity,
                        "AttrSeasonLevel",
                        socialData.BasicData.SeasonLevel);
                }
            }

            if (socialData.UserAttrData != null && socialData.UserAttrData.FightPoint > 0)
            {
                rosterChanged |= SetAttributeIfChanged(
                    encounter,
                    entity,
                    "AttrFightPoint",
                    ToInt32Saturating(socialData.UserAttrData.FightPoint));
            }

            if (socialData.ProfessionData != null && socialData.ProfessionData.ProfessionId > 0)
            {
                rosterChanged |= SetAttributeIfChanged(
                    encounter,
                    entity,
                    "AttrProfessionId",
                    socialData.ProfessionData.ProfessionId);
            }

            if (receivedEquipment != null)
            {
                PendingEquipmentData.TryRemove(entityUuid, out _);
                rosterChanged |= ApplyEquipmentData(entityUuid, entity, receivedEquipment);
            }
            else
            {
                rosterChanged |= ApplyPendingEquipmentData(entityUuid, entity);
            }

            rosterChanged |= ApplyPendingFastSyncData(entityUuid, entity);
            rosterChanged |= npcEvidenceChanged;

            if (rosterChanged)
            {
                rosterPlayersToUpsert.Add(entityUuid);
            }
        }

        private static void ApplyTeamMemberFastSyncData(
            long fallbackCharId,
            TeamMemberFastSyncData fastSyncData,
            HashSet<long> rosterPlayersToUpsert)
        {
            var charId = fastSyncData.CharId > 0 ? fastSyncData.CharId : fallbackCharId;
            if (charId <= 0)
            {
                return;
            }

            var entityUuid = Utils.EntityIdToUuid(charId, (long)EEntityType.EntChar, false, false);
            if (entityUuid == 0)
            {
                return;
            }

            if (!TryGetLiveOtherPlayer(entityUuid, out var entity))
            {
                PendingFastSyncData[entityUuid] = fastSyncData.Clone();
                return;
            }

            PendingFastSyncData.TryRemove(entityUuid, out _);
            if (ApplyFastSyncData(entity, fastSyncData))
            {
                rosterPlayersToUpsert.Add(entityUuid);
            }
        }

        private static bool ApplyFastSyncData(Entity entity, TeamMemberFastSyncData fastSyncData)
        {
            SetSourceAttributeIfChanged(entity, TeamFastSyncSceneIdAttribute, fastSyncData.SceneId);
            SetSourceAttributeIfChanged(entity, TeamFastSyncStateAttribute, fastSyncData.State);
            SetSourceAttributeIfChanged(entity, TeamFastSyncSceneAreaIdAttribute, fastSyncData.SceneAreaId);
            var fastSyncHpChanged = SetSourceAttributeIfChanged(
                entity,
                TeamFastSyncHpAttribute,
                fastSyncData.Hp);
            var fastSyncMaxHpChanged = SetSourceAttributeIfChanged(
                entity,
                TeamFastSyncMaxHpAttribute,
                fastSyncData.MaxHp);

            if (fastSyncData.Position != null)
            {
                SetSourceAttributeIfChanged(
                    entity,
                    TeamFastSyncPositionXAttribute,
                    fastSyncData.Position.X);
                SetSourceAttributeIfChanged(
                    entity,
                    TeamFastSyncPositionYAttribute,
                    fastSyncData.Position.Y);
                SetSourceAttributeIfChanged(
                    entity,
                    TeamFastSyncPositionZAttribute,
                    fastSyncData.Position.Z);
                SetSourceAttributeIfChanged(
                    entity,
                    TeamFastSyncDirectionAttribute,
                    fastSyncData.Position.Dir);
            }

            if (!TryGetFastSyncHealthFallback(entity, out _, out _))
            {
                return false;
            }

            return fastSyncMaxHpChanged
                || (!TryGetNonNegativeInt64(entity.GetAttrKV("AttrHp"), out _)
                    && fastSyncHpChanged);
        }

        private static void ApplyPendingMemberData(long entityUuid, Entity entity)
        {
            ApplyPendingEquipmentData(entityUuid, entity);
            ApplyPendingFastSyncData(entityUuid, entity);
        }

        private static bool ApplyPendingEquipmentData(long entityUuid, Entity entity)
        {
            return PendingEquipmentData.TryRemove(entityUuid, out var equipment)
                && ApplyEquipmentData(entityUuid, entity, equipment);
        }

        private static bool ApplyPendingFastSyncData(long entityUuid, Entity entity)
        {
            return PendingFastSyncData.TryRemove(entityUuid, out var fastSyncData)
                && ApplyFastSyncData(entity, fastSyncData);
        }

        internal static bool TryGetFastSyncHealthFallback(
            Entity entity,
            out long hp,
            out long maxHp)
        {
            hp = 0;
            maxHp = 0;

            if (entity.HasNpcEvidence
                || TryGetPositiveInt64(entity.GetAttrKV("AttrMaxHp"), out _))
            {
                return false;
            }

            if (!TryGetPositiveInt64(entity.GetAttrKV(TeamFastSyncMaxHpAttribute), out maxHp)
                || (!TryGetNonNegativeInt64(entity.GetAttrKV("AttrHp"), out hp)
                    && !TryGetNonNegativeInt64(entity.GetAttrKV(TeamFastSyncHpAttribute), out hp)))
            {
                hp = 0;
                maxHp = 0;
                return false;
            }

            return true;
        }

        private static bool ApplyEquipmentData(long entityUuid, Entity entity, IReadOnlyList<EquipNine> equipment)
        {
            if (equipment.Count == 0 || EquipmentMatches(entity.GetAttrKV("AttrEquipData"), equipment))
            {
                return false;
            }

            if (EncounterManager.Current is not { } encounter)
            {
                return false;
            }

            encounter.SetAttrKV(
                entityUuid,
                "AttrEquipData",
                equipment.Select(item => item.Clone()).ToList());
            return true;
        }

        private static bool SetAttributeIfChanged(Encounter encounter, Entity entity, string key, object value)
        {
            if (Equals(entity.GetAttrKV(key), value))
            {
                return false;
            }

            encounter.SetAttrKV(entity.UUID, key, value);
            return true;
        }

        private static bool SetSourceAttributeIfChanged(Entity entity, string key, object value)
        {
            if (Equals(entity.GetAttrKV(key), value))
            {
                return false;
            }

            entity.SetAttrKV(key, value);
            return true;
        }

        private static bool TryGetPositiveInt64(object? value, out long result)
        {
            if (TryGetNonNegativeInt64(value, out result) && result > 0)
            {
                return true;
            }

            result = 0;
            return false;
        }

        private static bool TryGetNonNegativeInt64(object? value, out long result)
        {
            switch (value)
            {
                case long integer when integer >= 0:
                    result = integer;
                    return true;
                case int integer when integer >= 0:
                    result = integer;
                    return true;
                case uint integer:
                    result = integer;
                    return true;
                case ulong integer when integer <= long.MaxValue:
                    result = (long)integer;
                    return true;
                case short integer when integer >= 0:
                    result = integer;
                    return true;
                case ushort integer:
                    result = integer;
                    return true;
                case byte integer:
                    result = integer;
                    return true;
                case sbyte integer when integer >= 0:
                    result = integer;
                    return true;
                default:
                    result = 0;
                    return false;
            }
        }

        private static bool TryGetLiveOtherPlayer(long entityUuid, out Entity entity)
        {
            entity = null!;
            var encounter = EncounterManager.Current;
            return encounter != null
                && encounter.Entities.TryGetValue(entityUuid, out entity)
                && IsLiveOtherPlayer(entityUuid, entity);
        }

        private static bool IsLiveOtherPlayer(long entityUuid, Entity entity)
        {
            if (entity.EntityType != EEntityType.EntChar)
            {
                return false;
            }

            if (entityUuid == AppState.PlayerUUID)
            {
                return false;
            }

            return AppState.PlayerUID == 0 || Utils.UuidToEntityId(entityUuid) != AppState.PlayerUID;
        }

        private static void PublishRosterChanges(HashSet<long> rosterPlayersToUpsert)
        {
            foreach (var entityUuid in rosterPlayersToUpsert)
            {
                PlayerRosterProjection.UpsertPlayer(entityUuid);
            }
        }

        private static EquipNine[] CloneEquipment(IEnumerable<EquipNine> equipment)
        {
            return equipment.Select(item => item.Clone()).ToArray();
        }

        private static bool EquipmentMatches(object? rawEquipment, IReadOnlyList<EquipNine> equipment)
        {
            if (rawEquipment is not IEnumerable<EquipNine> currentEquipment)
            {
                return false;
            }

            using var currentEnumerator = currentEquipment.GetEnumerator();
            for (var index = 0; index < equipment.Count; index++)
            {
                if (!currentEnumerator.MoveNext())
                {
                    return false;
                }

                var current = currentEnumerator.Current;
                var expected = equipment[index];
                if (current.Slot != expected.Slot || current.EquipID != expected.EquipID)
                {
                    return false;
                }
            }

            return !currentEnumerator.MoveNext();
        }

        private static int ToInt32Saturating(long value)
        {
            return value > int.MaxValue ? int.MaxValue : (int)value;
        }
    }
}
