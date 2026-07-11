using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using StarResonanceDps.App.Services;
using StarResonanceDps.Core.CombatRuntime;

namespace StarResonanceDps.App.Models.Widgets;

public sealed partial class PlayerImagineRoleSkillEntry : ObservableObject
{
    [ObservableProperty]
    private string _nameLevelText = string.Empty;

    [ObservableProperty]
    private string? _iconPath;

    [ObservableProperty]
    private string _cooldownText = string.Empty;

    [ObservableProperty]
    private bool _hasCooldown;

    public void Update(
        PlayerCooldownSkillSnapshot? snapshot,
        double? remainingSeconds)
    {
        if (snapshot is null)
        {
            NameLevelText = string.Empty;
            IconPath = null;
            CooldownText = string.Empty;
            HasCooldown = false;
            return;
        }

        NameLevelText = $"{snapshot.Name} Lv.{snapshot.CurrentLevel}";
        IconPath = CombatIconResolver.ResolveSkillIcon(snapshot.IconName, snapshot.IsImagine);

        if (remainingSeconds is not > 0)
        {
            CooldownText = string.Empty;
            HasCooldown = false;
            return;
        }

        CooldownText = remainingSeconds.Value.ToString("0.00", CultureInfo.InvariantCulture) + "s";
        HasCooldown = true;
    }
}
