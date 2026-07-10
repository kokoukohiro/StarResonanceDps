using CommunityToolkit.Mvvm.ComponentModel;
using StarResonanceDps.App.Services;
using StarResonanceDps.Core.CombatRuntime;

namespace StarResonanceDps.App.Models.Widgets;

public sealed partial class PlayerSkillInfoEntry : ObservableObject
{
    [ObservableProperty]
    private string _skillIdText = string.Empty;

    [ObservableProperty]
    private string _name = string.Empty;

    [ObservableProperty]
    private string _currentLevelText = string.Empty;

    [ObservableProperty]
    private string _tierText = string.Empty;

    [ObservableProperty]
    private string? _iconPath;

    public PlayerSkillInfoEntry(PlayerSkillInfoSnapshot snapshot)
    {
        Update(snapshot);
    }

    public void Update(PlayerSkillInfoSnapshot snapshot)
    {
        SkillIdText = snapshot.SkillId.ToString();
        Name = snapshot.Name ?? string.Empty;
        CurrentLevelText = snapshot.CurrentLevel.ToString();
        TierText = snapshot.Tier.ToString();
        IconPath = CombatIconResolver.ResolveSkillIcon(snapshot.IconName, snapshot.IsImagine);
    }
}
