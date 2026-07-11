using StarResonanceDps.App.Services;

namespace StarResonanceDps.App.Models.Widgets;

public sealed class MetricSkillTableEntry(
    string skillIdText,
    string skillName,
    string iconName,
    bool isImagine,
    string totalValueText,
    string activePerSecondText,
    string encounterPerSecondText,
    string hitCountText,
    string critRateText,
    string averageValueText,
    string shareText)
{
    public string SkillIdText { get; } = skillIdText;

    public string SkillName { get; } = skillName;

    public string? IconPath { get; } = CombatIconResolver.ResolveSkillIcon(iconName, isImagine);

    public string TotalValueText { get; } = totalValueText;

    public string ActivePerSecondText { get; } = activePerSecondText;

    public string EncounterPerSecondText { get; } = encounterPerSecondText;

    public string HitCountText { get; } = hitCountText;

    public string CritRateText { get; } = critRateText;

    public string AverageValueText { get; } = averageValueText;

    public string ShareText { get; } = shareText;
}
