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
        DateTime? activationTime,
        DateTime now)
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

        if (activationTime is null || snapshot.CooldownSeconds <= 0)
        {
            CooldownText = string.Empty;
            HasCooldown = false;
            return;
        }

        var remainingSeconds = snapshot.CooldownSeconds
            - now.Subtract(activationTime.Value).TotalSeconds;
        if (remainingSeconds <= 0)
        {
            CooldownText = string.Empty;
            HasCooldown = false;
            return;
        }

        CooldownText = FormatDuration(remainingSeconds);
        HasCooldown = true;
    }

    private static string FormatDuration(double seconds)
    {
        var roundedSeconds = Math.Max(1, (int)Math.Ceiling(seconds));
        if (roundedSeconds < 60)
        {
            return $"{roundedSeconds}s";
        }

        var minutes = roundedSeconds / 60;
        var remainderSeconds = roundedSeconds % 60;
        return $"{minutes}m{remainderSeconds:00}s";
    }
}
