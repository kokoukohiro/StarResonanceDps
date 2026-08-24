using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using StarResonanceDps.App.Services;
using StarResonanceDps.Core.CombatRuntime;

namespace StarResonanceDps.App.Models.Widgets;

public sealed partial class PlayerImagineRoleSkillEntry : ObservableObject
{
    public PlayerImagineRoleSkillEntry(bool isImagine = false)
    {
        IsImagine = isImagine;
    }

    public bool IsImagine { get; }

    private bool _usesImagineAsset;

    public bool UsesImagineAsset
    {
        get => _usesImagineAsset;
        private set => SetProperty(ref _usesImagineAsset, value);
    }

    private PlayerBuffEntry? _buffEntry;

    public PlayerBuffEntry? BuffEntry
    {
        get => _buffEntry;
        private set => SetProperty(ref _buffEntry, value);
    }

    private PlayerBuffEntry? _debuffEntry;

    public PlayerBuffEntry? DebuffEntry
    {
        get => _debuffEntry;
        private set => SetProperty(ref _debuffEntry, value);
    }

    [ObservableProperty]
    private int _skillId;

    [ObservableProperty]
    private bool _hasSkill;

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
        UpdateSkill(snapshot);
        UpdateCooldown(snapshot, cooldownState);
    }

    public void UpdateSkill(PlayerCooldownSkillSnapshot? snapshot)
    {
        if (snapshot is null)
        {
            SkillId = 0;
            HasSkill = false;
            UsesImagineAsset = false;
            NameDisplayText = string.Empty;
            IconPath = null;
            HasChargeCount = false;
            UpdateEffects(null, null);
            return;
        }

        if (SkillId != snapshot.SkillId)
        {
            UpdateEffects(null, null);
        }

        SkillId = snapshot.SkillId;
        HasSkill = true;
        UsesImagineAsset = snapshot.IsImagine || snapshot.ShowLevel;
        NameDisplayText = snapshot.IsImagine
            ? $"{snapshot.Name} Tier{snapshot.Tier}"
            : snapshot.ShowLevel
                ? $"{snapshot.Name} Lv.{snapshot.CurrentLevel}"
                : snapshot.Name;
        IconPath = CombatIconResolver.ResolveSkillIcon(snapshot.IconName, snapshot.IsImagine);
        HasChargeCount = snapshot.MaxCharges > 1;
    }

    public void UpdateCooldown(
        PlayerCooldownSkillSnapshot? snapshot,
        SkillCooldownDisplayState cooldownState)
    {
        if (snapshot is null)
        {
            ChargeCountText = string.Empty;
            CooldownText = string.Empty;
            HasCooldown = false;
            return;
        }

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
            .ToString("0.0", CultureInfo.InvariantCulture);
        HasCooldown = true;
    }

    public void UpdateEffects(
        PlayerBuffSnapshot? buff,
        PlayerBuffSnapshot? debuff)
    {
        BuffEntry = UpdateEffect(BuffEntry, buff);
        DebuffEntry = UpdateEffect(DebuffEntry, debuff);
    }

    private static PlayerBuffEntry? UpdateEffect(
        PlayerBuffEntry? entry,
        PlayerBuffSnapshot? snapshot)
    {
        if (snapshot is null)
        {
            return null;
        }

        if (entry is null
            || !string.Equals(entry.Key, snapshot.Key, StringComparison.Ordinal))
        {
            return new PlayerBuffEntry(snapshot);
        }

        entry.Update(snapshot);
        return entry;
    }
}
