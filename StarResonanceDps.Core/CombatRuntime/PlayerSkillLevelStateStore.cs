using System.Collections.Frozen;
using Zproto;

namespace StarResonanceDps.Core.CombatRuntime;

public static class PlayerSkillLevelStateStore
{
    private static FrozenDictionary<int, int> _selfSkillLevels =
        new Dictionary<int, int>().ToFrozenDictionary();

    public static void ReplaceSelfSkillLevels(
        ProfessionList? professionList,
        DutyList? dutyList)
    {
        var levels = new Dictionary<int, int>();

        if (professionList is not null)
        {
            AddSkillLevels(levels, professionList.AoyiSkillInfoMap);
        }

        if (dutyList is not null)
        {
            if (dutyList.CurProfessionDutyId != 0
                && dutyList.DutyInfoMap.TryGetValue(
                    dutyList.CurProfessionDutyId,
                    out var currentDutyInfo))
            {
                AddSkillLevels(levels, currentDutyInfo.DutySkillInfoMap);
            }
            else
            {
                foreach (var dutyInfo in dutyList.DutyInfoMap.Values)
                {
                    AddSkillLevels(levels, dutyInfo.DutySkillInfoMap);
                }
            }
        }

        Volatile.Write(ref _selfSkillLevels, levels.ToFrozenDictionary());
    }

    public static bool TryGetSelfSkillLevel(int skillId, out int level)
    {
        return Volatile.Read(ref _selfSkillLevels).TryGetValue(skillId, out level);
    }

    private static void AddSkillLevels(
        IDictionary<int, int> levels,
        IEnumerable<KeyValuePair<int, ProfessionSkillInfo>> skills)
    {
        foreach (var pair in skills)
        {
            var skillId = pair.Value.SkillId > 0
                ? pair.Value.SkillId
                : pair.Key;
            if (skillId <= 0)
            {
                continue;
            }

            var level = pair.Value.Level;
            if (level <= 0)
            {
                continue;
            }

            levels[skillId] = level;
        }
    }
}
