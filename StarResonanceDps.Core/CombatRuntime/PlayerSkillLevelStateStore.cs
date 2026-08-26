using System.Collections.Frozen;
using StarResonanceDps.Core.CombatRuntime.DataTypes.Enums;
using StarResonanceDps.Core.Models;
using BinaryDutyInfo = StarResonanceDps.Core.Protocols.Game.Binary.DutyInfo;
using BinaryDutyList = StarResonanceDps.Core.Protocols.Game.Binary.DutyList;
using BinaryProfessionInfo = StarResonanceDps.Core.Protocols.Game.Binary.ProfessionInfo;
using BinaryProfessionList = StarResonanceDps.Core.Protocols.Game.Binary.ProfessionList;
using BinaryProfessionSkillInfo = StarResonanceDps.Core.Protocols.Game.Binary.ProfessionSkillInfo;
using BinaryProtocol = StarResonanceDps.Core.Protocols.Game.Binary;
using Zproto;

namespace StarResonanceDps.Core.CombatRuntime;

public readonly record struct PlayerRoleSkillLevelState(
    int SkillId,
    int CurrentLevel,
    int Tier);

public static class PlayerSkillLevelStateStore
{
    private static readonly object StateLock = new();
    private static readonly Dictionary<int, ProfessionState> SelfProfessionStates = [];
    private static readonly Dictionary<int, DutyState> SelfDutyStates = [];
    private static readonly Dictionary<int, int> SelfTalentStageIds = [];
    private static readonly Dictionary<int, int> ProjectProfessionIds = [];
    private static Dictionary<int, SkillState> _selfImagineSkills = [];
    private static Dictionary<int, SkillState> _selfRawSelectedImagineSkills = [];
    private static int _selfCurrentProfessionId;
    private static int _selfCurrentDutyId;
    private static int _selfCurrentProjectId;
    private static bool _hasSelfDutyState;
    private static FrozenDictionary<int, int> _selfSkillLevels =
        new Dictionary<int, int>().ToFrozenDictionary();
    private static DataTypes.Skills.SkillLevelInfo[] _selfCurrentSkillLevels = [];
    private static PlayerRoleSkillLevelState[] _selfRoleSkillLevels = [];

    public static void ReplaceSelfSkillLevels(
        Zproto.ProfessionList? professionList,
        Zproto.DutyList? dutyList)
    {
        lock (StateLock)
        {
            SelfProfessionStates.Clear();
            SelfTalentStageIds.Clear();
            ProjectProfessionIds.Clear();
            _selfImagineSkills = [];
            _selfRawSelectedImagineSkills = [];
            _selfCurrentProjectId = 0;
            _selfCurrentProfessionId = professionList?.CurProfessionId ?? 0;

            if (professionList is not null)
            {
                foreach (var pair in professionList.ProfessionList_)
                {
                    SelfProfessionStates[pair.Key] = CreateProfessionState(pair.Value);
                }

                foreach (var pair in professionList.TalentList)
                {
                    SelfTalentStageIds[pair.Key] = pair.Value.TalentStageCfgId;
                }

                foreach (var pair in professionList.AoyiSkillInfoMap)
                {
                    var skillId = ResolveSkillId(pair.Key, pair.Value.SkillId);
                    if (skillId > 0)
                    {
                        _selfImagineSkills[skillId] = new SkillState(
                            skillId,
                            pair.Value.Level,
                            pair.Value.RemodelLevel);
                    }
                }
            }

            SelfDutyStates.Clear();
            _selfCurrentDutyId = dutyList?.CurProfessionDutyId ?? 0;
            _hasSelfDutyState = dutyList is not null;

            if (dutyList is not null)
            {
                foreach (var pair in dutyList.DutyInfoMap)
                {
                    SelfDutyStates[pair.Key] = CreateDutyState(pair.Value);
                }
            }

            PublishState();
        }
    }

    public static void ApplySelfProfessionListChanges(BinaryProfessionList professionList)
    {
        lock (StateLock)
        {
            if (professionList.CurProfessionId is { } currentProfessionId)
            {
                _selfCurrentProfessionId = currentProfessionId;
            }

            if (professionList.ProfessionInfoChanges is { } professionChanges)
            {
                if (professionChanges.ReplacesExisting)
                {
                    SelfProfessionStates.Clear();
                }

                foreach (var professionId in professionChanges.Removed)
                {
                    SelfProfessionStates.Remove(professionId);
                }

                foreach (var pair in professionChanges.Added)
                {
                    var state = new ProfessionState();
                    ApplyProfessionInfoChanges(state, pair.Value);
                    SelfProfessionStates[pair.Key] = state;
                }

                foreach (var pair in professionChanges.Updated)
                {
                    if (!SelfProfessionStates.TryGetValue(pair.Key, out var state))
                    {
                        state = new ProfessionState();
                        SelfProfessionStates[pair.Key] = state;
                    }

                    ApplyProfessionInfoChanges(state, pair.Value);
                }
            }

            if (professionList.AoyiSkillInfoChanges is { } imagineChanges)
            {
                ApplySkillMapChanges(_selfImagineSkills, imagineChanges);
            }

            if (professionList.TalentInfoChanges is { } talentChanges)
            {
                if (talentChanges.ReplacesExisting)
                {
                    SelfTalentStageIds.Clear();
                }

                foreach (var professionId in talentChanges.Removed)
                {
                    SelfTalentStageIds.Remove(professionId);
                }

                foreach (var pair in talentChanges.Added)
                {
                    SelfTalentStageIds[pair.Key] = pair.Value.TalentStageCfgId ?? 0;
                }

                foreach (var pair in talentChanges.Updated)
                {
                    if (pair.Value.TalentStageCfgId is { } stageId)
                    {
                        SelfTalentStageIds[pair.Key] = stageId;
                    }

                }
            }

            PublishState();
        }
    }

    public static void ApplySelfDutyListChanges(BinaryDutyList dutyList)
    {
        lock (StateLock)
        {
            _hasSelfDutyState = true;
            if (dutyList.CurProfessionDutyId is { } currentDutyId)
            {
                _selfCurrentDutyId = currentDutyId;
            }

            if (dutyList.DutyInfoChanges is { } dutyChanges)
            {
                if (dutyChanges.ReplacesExisting)
                {
                    SelfDutyStates.Clear();
                }

                foreach (var dutyId in dutyChanges.Removed)
                {
                    SelfDutyStates.Remove(dutyId);
                }

                foreach (var pair in dutyChanges.Added)
                {
                    var dutyState = new DutyState();
                    ApplyDutyInfoChanges(dutyState, pair.Value);
                    SelfDutyStates[pair.Key] = dutyState;
                }

                foreach (var pair in dutyChanges.Updated)
                {
                    if (!SelfDutyStates.TryGetValue(pair.Key, out var dutyState))
                    {
                        dutyState = new DutyState();
                        SelfDutyStates[pair.Key] = dutyState;
                    }

                    ApplyDutyInfoChanges(dutyState, pair.Value);
                }
            }

            PublishState();
        }
    }

    public static void UpdateSelfRawSkillLevels(
        IReadOnlyList<DataTypes.Skills.SkillLevelInfo> skillLevels)
    {
        lock (StateLock)
        {
            var selectedImagineSkills = new Dictionary<int, SkillState>();
            foreach (var skill in skillLevels)
            {
                if (skill.SkillId <= 0
                    || !CombatDataCatalog.IsSkillImagine(skill.SkillId, skill.Icon))
                {
                    continue;
                }

                selectedImagineSkills[skill.SkillId] = new SkillState(
                    skill.SkillId,
                    skill.CurrentLevel,
                    skill.Tier);
            }

            _selfRawSelectedImagineSkills = selectedImagineSkills;
            PublishState();
        }
    }

    public static void ReplaceSelfProjectList(
        ProfessionProjectList? projectList,
        ProjectExtraSyncData? currentProjectSyncData)
    {
        lock (StateLock)
        {
            ProjectProfessionIds.Clear();
            ProfessionProjectCommonSyncData? currentProjectCommonData = null;
            if (projectList is not null)
            {
                foreach (var pair in projectList.ProfessionProjectList_)
                {
                    ProjectProfessionIds[pair.Key] = pair.Value.ProfessionId;
                }

                _selfCurrentProjectId = projectList.CurrentProjectId;
                projectList.ProfessionProjectList_.TryGetValue(
                    _selfCurrentProjectId,
                    out currentProjectCommonData);
            }

            ApplyCurrentProjectStateLocked(
                _selfCurrentProjectId,
                currentProjectSyncData,
                currentProjectCommonData);
            PublishState();
        }
    }

    public static void ApplySelfProjectState(
        int projectId,
        ProjectExtraSyncData? projectSyncData,
        ProfessionProjectCommonSyncData? commonSyncData = null)
    {
        lock (StateLock)
        {
            if (commonSyncData is not null && projectId > 0)
            {
                ProjectProfessionIds[projectId] = commonSyncData.ProfessionId;
            }

            _selfCurrentProjectId = projectId;
            ApplyCurrentProjectStateLocked(projectId, projectSyncData, commonSyncData);
            PublishState();
        }
    }

    public static void ApplySelfSavedProjectState(
        int projectId,
        ProjectExtraSyncData? currentProjectSyncData,
        ProfessionProjectCommonSyncData? savedProjectData)
    {
        lock (StateLock)
        {
            if (savedProjectData is not null && projectId > 0)
            {
                ProjectProfessionIds[projectId] = savedProjectData.ProfessionId;
            }

            if (projectId > 0 && projectId == _selfCurrentProjectId)
            {
                ApplyCurrentProjectStateLocked(
                    projectId,
                    currentProjectSyncData,
                    savedProjectData);
            }

            PublishState();
        }
    }

    public static void SetSelfCurrentProfessionId(int professionId)
    {
        if (professionId <= 0)
        {
            return;
        }

        lock (StateLock)
        {
            _selfCurrentProfessionId = professionId;
            PublishState();
        }
    }

    public static void SetSelfCurrentProjectId(int projectId)
    {
        if (projectId <= 0)
        {
            return;
        }

        lock (StateLock)
        {
            _selfCurrentProjectId = projectId;
            PublishState();
        }
    }

    public static void SetSelfTalentStage(int professionId, int talentStageId)
    {
        if (professionId <= 0)
        {
            return;
        }

        lock (StateLock)
        {
            SelfTalentStageIds[professionId] = talentStageId;
            PublishState();
        }
    }

    public static bool TryGetSelfCurrentProfessionId(out int professionId)
    {
        lock (StateLock)
        {
            professionId = _selfCurrentProfessionId;
            return professionId > 0;
        }
    }

    public static bool TryGetSelfCurrentSkillLevels(
        out IReadOnlyList<DataTypes.Skills.SkillLevelInfo> skillLevels)
    {
        lock (StateLock)
        {
            skillLevels = _selfCurrentSkillLevels;
            return _selfCurrentProfessionId > 0
                && SelfProfessionStates.ContainsKey(_selfCurrentProfessionId);
        }
    }

    public static bool TryGetSelfSkillLevel(int skillId, out int level)
    {
        return Volatile.Read(ref _selfSkillLevels).TryGetValue(skillId, out level);
    }

    public static bool TryGetSelfRoleSkillLevels(
        out IReadOnlyList<PlayerRoleSkillLevelState> roleSkillLevels)
    {
        lock (StateLock)
        {
            roleSkillLevels = _selfRoleSkillLevels;
            return roleSkillLevels.Count > 0;
        }
    }

    private static void ApplyCurrentProjectStateLocked(
        int projectId,
        ProjectExtraSyncData? projectSyncData,
        ProfessionProjectCommonSyncData? commonSyncData)
    {
        var professionId = commonSyncData?.ProfessionId ?? 0;
        if (professionId <= 0 && projectId > 0)
        {
            ProjectProfessionIds.TryGetValue(projectId, out professionId);
        }

        var hasTalentState = projectSyncData?.CurrentTalentIdList is not null
            || commonSyncData is not null;
        var talentStageId = projectSyncData?.CurrentTalentIdList?.TalentStageCfgId
            ?? commonSyncData?.CurrentTalentStageCfgId
            ?? 0;
        if (professionId <= 0 && talentStageId > 0)
        {
            professionId = DataTypes.Professions.GetProfessionIdFromTalentId(talentStageId);
        }

        if (professionId > 0)
        {
            if (projectId > 0)
            {
                ProjectProfessionIds[projectId] = professionId;
            }

            _selfCurrentProfessionId = professionId;
            if (hasTalentState)
            {
                SelfTalentStageIds[professionId] = talentStageId;
            }
        }

    }

    private static ProfessionState CreateProfessionState(Zproto.ProfessionInfo professionInfo)
    {
        var state = new ProfessionState();
        foreach (var pair in professionInfo.SkillInfoMap)
        {
            var skillId = ResolveSkillId(pair.Key, pair.Value.SkillId);
            state.Skills[pair.Key] = new SkillState(
                skillId,
                pair.Value.Level,
                pair.Value.RemodelLevel);
        }

        return state;
    }

    private static void ApplyProfessionInfoChanges(
        ProfessionState state,
        BinaryProfessionInfo changes)
    {
        if (changes.SkillInfoChanges is { } skillChanges)
        {
            ApplySkillMapChanges(state.Skills, skillChanges);
        }

    }

    private static DutyState CreateDutyState(Zproto.DutyInfo dutyInfo)
    {
        var state = new DutyState();
        foreach (var pair in dutyInfo.DutySkillInfoMap)
        {
            state.Skills[pair.Key] = new SkillState(
                ResolveSkillId(pair.Key, pair.Value.SkillId),
                pair.Value.Level,
                pair.Value.RemodelLevel);
        }

        foreach (var pair in dutyInfo.DutySkillSlotInfoMap)
        {
            state.Slots[pair.Key] = pair.Value;
        }

        return state;
    }

    private static void ApplyDutyInfoChanges(
        DutyState dutyState,
        BinaryDutyInfo dutyInfo)
    {
        if (dutyInfo.DutySkillInfoChanges is { } skillChanges)
        {
            ApplySkillMapChanges(dutyState.Skills, skillChanges);
        }

        if (dutyInfo.DutySkillSlotInfoChanges is { } slotChanges)
        {
            ApplyMapChanges(dutyState.Slots, slotChanges);
        }
    }

    private static void ApplySkillMapChanges(
        IDictionary<int, SkillState> skills,
        BinaryProtocol.BlobHashMapDelta<int, BinaryProfessionSkillInfo> changes)
    {
        if (changes.ReplacesExisting)
        {
            skills.Clear();
        }

        foreach (var skillKey in changes.Removed)
        {
            skills.Remove(skillKey);
        }

        foreach (var pair in changes.Added)
        {
            ApplySkillChange(skills, pair.Key, pair.Value, false);
        }

        foreach (var pair in changes.Updated)
        {
            ApplySkillChange(skills, pair.Key, pair.Value, true);
        }
    }

    private static void ApplyMapChanges<TKey, TValue>(
        IDictionary<TKey, TValue> values,
        BinaryProtocol.BlobHashMapDelta<TKey, TValue> changes)
        where TKey : notnull
    {
        if (changes.ReplacesExisting)
        {
            values.Clear();
        }

        foreach (var key in changes.Removed)
        {
            values.Remove(key);
        }

        foreach (var pair in changes.Added)
        {
            values[pair.Key] = pair.Value;
        }

        foreach (var pair in changes.Updated)
        {
            values[pair.Key] = pair.Value;
        }
    }

    private static void ApplySkillChange(
        IDictionary<int, SkillState> skills,
        int skillKey,
        BinaryProfessionSkillInfo change,
        bool preserveExisting)
    {
        var existing = preserveExisting && skills.TryGetValue(skillKey, out var current)
            ? current
            : default;
        var skillId = change.SkillId
            ?? (existing.SkillId > 0 ? existing.SkillId : skillKey);
        skills[skillKey] = new SkillState(
            skillId,
            change.Level ?? existing.Level,
            change.RemodelLevel ?? existing.Tier);
    }

    private static void PublishState()
    {
        var roleSkills = BuildCurrentRoleSkillLevels();
        var currentSkills = BuildCurrentSkillLevels(roleSkills);
        var levels = new Dictionary<int, int>();
        foreach (var skill in currentSkills)
        {
            if (skill.SkillId > 0 && skill.CurrentLevel > 0)
            {
                levels[skill.SkillId] = skill.CurrentLevel;
            }
        }

        Volatile.Write(ref _selfRoleSkillLevels, roleSkills);
        Volatile.Write(ref _selfCurrentSkillLevels, currentSkills);
        Volatile.Write(ref _selfSkillLevels, levels.ToFrozenDictionary());
    }

    private static DataTypes.Skills.SkillLevelInfo[] BuildCurrentSkillLevels(
        IReadOnlyList<PlayerRoleSkillLevelState> roleSkills)
    {
        var result = new List<DataTypes.Skills.SkillLevelInfo>();
        var includedSkillIds = new HashSet<int>();

        if (_selfCurrentProfessionId > 0
            && SelfProfessionStates.TryGetValue(_selfCurrentProfessionId, out var professionState))
        {
            foreach (var pair in professionState.Skills.OrderBy(pair => pair.Key))
            {
                AddSkill(result, includedSkillIds, pair.Value);
            }
        }

        foreach (var roleSkill in roleSkills)
        {
            AddSkill(result, includedSkillIds, new SkillState(
                roleSkill.SkillId,
                roleSkill.CurrentLevel,
                roleSkill.Tier));
        }

        foreach (var imagineSkill in EnumerateSelectedImagineSkills())
        {
            AddSkill(result, includedSkillIds, imagineSkill);
        }

        return [.. result];
    }

    private static void AddSkill(
        ICollection<DataTypes.Skills.SkillLevelInfo> result,
        ISet<int> includedSkillIds,
        SkillState skill)
    {
        if (skill.SkillId <= 0 || !includedSkillIds.Add(skill.SkillId))
        {
            return;
        }

        result.Add(new DataTypes.Skills.SkillLevelInfo
        {
            SkillId = skill.SkillId,
            CurrentLevel = skill.Level,
            Tier = skill.Tier
        });
    }

    private static IEnumerable<SkillState> EnumerateSelectedImagineSkills()
    {
        foreach (var skillId in _selfRawSelectedImagineSkills.Keys.Distinct())
        {
            if (_selfImagineSkills.TryGetValue(skillId, out var imagineSkill))
            {
                yield return imagineSkill;
            }
            else if (_selfRawSelectedImagineSkills.TryGetValue(skillId, out imagineSkill))
            {
                yield return imagineSkill;
            }
            else
            {
                yield return new SkillState(skillId, 0, 0);
            }
        }
    }

    private static PlayerRoleSkillLevelState[] BuildCurrentRoleSkillLevels()
    {
        if (!_hasSelfDutyState)
        {
            return [];
        }

        DutyState? dutyState = null;
        var professionDutyId = ResolveDutyIdForProfession(_selfCurrentProfessionId);
        if (professionDutyId != 0)
        {
            SelfDutyStates.TryGetValue(professionDutyId, out dutyState);
        }

        if (dutyState is null && _selfCurrentDutyId != 0)
        {
            SelfDutyStates.TryGetValue(_selfCurrentDutyId, out dutyState);
        }

        if (dutyState is null && SelfDutyStates.Count == 1)
        {
            dutyState = SelfDutyStates.Values.First();
        }

        if (dutyState is null)
        {
            return [];
        }

        return EnumerateSelectedDutySkills(dutyState)
            .Where(skill => skill.SkillId > 0)
            .DistinctBy(skill => skill.SkillId)
            .Select(skill => new PlayerRoleSkillLevelState(
                skill.SkillId,
                skill.Level,
                skill.Tier))
            .ToArray();
    }

    private static IEnumerable<SkillState> EnumerateSelectedDutySkills(DutyState dutyState)
    {
        // スロット割り当てが空 = 1つも装備していない。
        // 実測(2026-08-25): フルコンテナは全職務のスロットを同時に運んでおり、装備0の職務だけが
        // 空で届く(duty2=4件・duty3=0件が同一パケットで到着)。1つ装備すると即座に1件で届く。
        // つまり空は「未受信」ではなく「装備なし」。習得済みで埋めない。
        if (dutyState.Slots.Count == 0)
        {
            return [];
        }

        if (dutyState.Skills.Count <= 4)
        {
            return dutyState.Skills
                .OrderBy(pair => pair.Key)
                .Select(pair => pair.Value);
        }

        var skillKeyById = dutyState.Skills
            .Where(pair => pair.Value.SkillId > 0)
            .GroupBy(pair => pair.Value.SkillId)
            .ToDictionary(group => group.Key, group => group.First().Key);
        var directMatches = dutyState.Slots.Count(pair =>
            dutyState.Skills.ContainsKey(pair.Key)
            || skillKeyById.ContainsKey(pair.Key));
        var valueMatches = dutyState.Slots.Count(pair =>
            dutyState.Skills.ContainsKey(pair.Value)
            || skillKeyById.ContainsKey(pair.Value));
        var keysLookLikeSlots = dutyState.Slots.Keys.All(IsDutySlotId);
        var valuesLookLikeSlots = dutyState.Slots.Values.All(IsDutySlotId);

        IEnumerable<int> selectedKeys;
        if ((valuesLookLikeSlots && !keysLookLikeSlots)
            || (directMatches > 0
                && directMatches >= valueMatches
                && !keysLookLikeSlots))
        {
            selectedKeys = dutyState.Slots
                .Select(pair => new
                {
                    Slot = pair.Value,
                    SkillKey = dutyState.Skills.ContainsKey(pair.Key)
                        ? pair.Key
                        : skillKeyById.GetValueOrDefault(pair.Key)
                })
                .Where(pair => pair.SkillKey != 0)
                .OrderBy(pair => pair.Slot)
                .ThenBy(pair => pair.SkillKey)
                .Select(pair => pair.SkillKey);
        }
        else if ((keysLookLikeSlots && !valuesLookLikeSlots)
            || valueMatches > 0)
        {
            selectedKeys = dutyState.Slots
                .Select(pair => new
                {
                    Slot = pair.Key,
                    SkillKey = dutyState.Skills.ContainsKey(pair.Value)
                        ? pair.Value
                        : skillKeyById.GetValueOrDefault(pair.Value)
                })
                .Where(pair => pair.SkillKey != 0)
                .OrderBy(pair => pair.Slot)
                .Select(pair => pair.SkillKey);
        }
        else
        {
            return dutyState.Skills
                .OrderBy(pair => pair.Key)
                .Select(pair => pair.Value);
        }

        return selectedKeys
            .Distinct()
            .Select(skillKey => dutyState.Skills[skillKey]);
    }

    private static int ResolveDutyIdForProfession(int professionId)
    {
        return DataTypes.Professions.GetRoleFromBaseProfessionId(professionId) switch
        {
            Professions.ERoleType.DPS => 1,
            Professions.ERoleType.Healer => 2,
            Professions.ERoleType.Tank => 3,
            _ => 0
        };
    }

    private static bool IsDutySlotId(int value)
    {
        return value is >= 0 and <= 4 or >= 21 and <= 24;
    }

    private static int ResolveSkillId(int skillKey, int skillId)
    {
        return skillId > 0 ? skillId : skillKey;
    }

    private sealed class ProfessionState
    {
        public Dictionary<int, SkillState> Skills { get; } = [];
    }

    private sealed class DutyState
    {
        public Dictionary<int, SkillState> Skills { get; } = [];
        public Dictionary<int, int> Slots { get; } = [];
    }

    private readonly record struct SkillState(
        int SkillId,
        int Level,
        int Tier);
}
