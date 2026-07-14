using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using StarResonanceDps.App.Services;
using StarResonanceDps.Core.CombatRuntime;

namespace StarResonanceDps.App.Models.Widgets;

public sealed partial class PlayerImagineRoleSkillEntry : ObservableObject
{
    [ObservableProperty]
    private string _nameDisplayText = string.Empty;

    [ObservableProperty]
    private string? _iconPath;

    [ObservableProperty]
    private string _chargeCountText = string.Empty;

    [ObservableProperty]
    private bool _hasChargeCount;

    [ObservableProperty]
    private string _cooldownText = string.Empty;

    [ObservableProperty]
    private bool _hasCooldown;

    public void Update(
        PlayerCooldownSkillSnapshot? snapshot,
        SkillCooldownDisplayState cooldownState)
    {
        if (snapshot is null)
        {
            NameDisplayText = string.Empty;
            IconPath = null;
            ChargeCountText = string.Empty;
            HasChargeCount = false;
            CooldownText = string.Empty;
            HasCooldown = false;
            return;
        }

        NameDisplayText = snapshot.IsImagine
            ? $"{snapshot.Name} Tier{snapshot.Tier}"
            : snapshot.ShowLevel
                ? $"{snapshot.Name} Lv.{snapshot.CurrentLevel}"
                : snapshot.Name;
        IconPath = CombatIconResolver.ResolveSkillIcon(snapshot.IconName, snapshot.IsImagine);

        HasChargeCount = snapshot.MaxCharges > 1;
        ChargeCountText = HasChargeCount
            ? Math.Clamp(
                    cooldownState.AvailableCharges ?? snapshot.MaxCharges,
                    0,
                    snapshot.MaxCharges)
                .ToString(CultureInfo.InvariantCulture)
            : string.Empty;

        var remainingSeconds = cooldownState.CooldownRemainingSeconds;
        if (remainingSeconds is not > 0)
        {
            CooldownText = string.Empty;
            HasCooldown = false;
            return;
        }

        CooldownText = remainingSeconds.Value
            .ToString("0.0", CultureInfo.InvariantCulture) + "s";
        HasCooldown = true;
    }
}
