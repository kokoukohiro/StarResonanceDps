using StarResonanceDps.Core.CombatRuntime;

namespace StarResonanceDps.App.Services;

public sealed class SkillCooldownTracker
{
    private static readonly Lazy<SkillCooldownTracker> LazyInstance = new(() => new SkillCooldownTracker());

    private SkillCooldownTracker()
    {
    }

    public static SkillCooldownTracker Instance => LazyInstance.Value;

    public double? GetRemainingSeconds(long entityUuid, int skillId)
    {
        return SkillCooldownStateStore.GetSelfRemainingSeconds(entityUuid, skillId);
    }
}
