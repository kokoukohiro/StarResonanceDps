using System.Collections.ObjectModel;
using System.Windows.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using StarResonanceDps.App.Config;
using StarResonanceDps.App.Localization;
using StarResonanceDps.App.Services;
using StarResonanceDps.Core.CombatRuntime;
using StarResonanceDps.Core.Models;

namespace StarResonanceDps.App.Models.Widgets;

public sealed partial class PlayerListEntry : ObservableObject
{
    private readonly ObservableCollection<PlayerImagineRoleSkillEntry> _imagineSkillEntries = [];
    private readonly ObservableCollection<PlayerImagineRoleSkillEntry> _roleSkillEntries = [];
    private IReadOnlyList<PlayerCooldownSkillSnapshot> _imagineSkillSnapshots =
        Array.Empty<PlayerCooldownSkillSnapshot>();
    private IReadOnlyList<PlayerCooldownSkillSnapshot> _roleSkillSnapshots =
        Array.Empty<PlayerCooldownSkillSnapshot>();
    private readonly HashSet<int> _trackedSkillIds = [];
    private long _skillEntityUuid;
    private int _skillProfessionId;
    private object? _skillSourceToken;
    private object? _roleFilterToken;
    private bool _skillLoadoutInitialized;

    private PlayerListEntry(long characterId)
    {
        CharacterId = characterId;
        FillSkillSlots(_imagineSkillEntries, 2, isImagine: true);
        FillSkillSlots(_roleSkillEntries, 4, isImagine: false);
        ImagineSkillEntries = new ReadOnlyObservableCollection<PlayerImagineRoleSkillEntry>(_imagineSkillEntries);
        RoleSkillEntries = new ReadOnlyObservableCollection<PlayerImagineRoleSkillEntry>(_roleSkillEntries);
    }

    public long CharacterId { get; }

    public ReadOnlyObservableCollection<PlayerImagineRoleSkillEntry> ImagineSkillEntries { get; }

    public ReadOnlyObservableCollection<PlayerImagineRoleSkillEntry> RoleSkillEntries { get; }

    [ObservableProperty]
    private string _professionKey = string.Empty;

    [ObservableProperty]
    private string _classSpecDisplayName = string.Empty;

    [ObservableProperty]
    private string _displayName = string.Empty;

    [ObservableProperty]
    private double _healthRatio;

    [ObservableProperty]
    private double _shieldVisibleRatio;

    [ObservableProperty]
    private double _shieldOverflowRatio;

    [ObservableProperty]
    private double _shieldOverflowStartRatio = 1d;

    [ObservableProperty]
    private string _healthText = string.Empty;

    [ObservableProperty]
    private bool _isNpc;

    [ObservableProperty]
    private bool _isPartyMember;

    [ObservableProperty]
    private string _partyNumberText = string.Empty;

    [ObservableProperty]
    private SolidColorBrush _classBrush = CreateBrush(Color.FromRgb(0xA8, 0xA8, 0xA8));

    [ObservableProperty]
    private bool _isPlayerSelectionMenuOpen;

    public bool IsHealthFull => HealthRatio >= 1d;

    public static PlayerListEntry Create(
        PlayerRosterEntry player,
        MeterWidgetSettingsConfig settings,
        PlayerNameDisplayMode playerNameDisplayMode)
    {
        var entry = new PlayerListEntry(player.CharacterId);
        entry.Update(player, settings, playerNameDisplayMode);
        entry.RefreshSkillDisplay(refreshEffects: true);
        return entry;
    }

    public void Update(
        PlayerRosterEntry player,
        MeterWidgetSettingsConfig settings,
        PlayerNameDisplayMode playerNameDisplayMode)
    {
        ProfessionKey = PlayerProfession.GetKey(player.ProfessionId);
        ClassSpecDisplayName = LocalizationManager.Instance.GetString($"ClassSpec_{player.ClassSpec}");
        IsNpc = player.IsNpc;
        IsPartyMember = player.IsPartyMember;
        PartyNumberText = player.IsPartyMember
            ? player.PartyNumber?.ToString(System.Globalization.CultureInfo.InvariantCulture) ?? "？"
            : string.Empty;

        DisplayName = PlayerInfoFormatFormatter.Format(
            player,
            settings.PlayerInfoFormatString,
            playerNameDisplayMode);

        HealthRatio = GetRatio(player.CurrentHp, player.MaxHp);
        UpdateShieldGeometry(player.CurrentHp, player.MaxHp, player.CurrentShield);
        HealthText = FormatHealthText(
            player.CurrentHp,
            player.MaxHp,
            player.CurrentShield,
            settings.HealthValueDisplayModeIndex);

        var classColor = GetClassColor(settings, ProfessionKey);
        if (ClassBrush.Color != classColor)
        {
            ClassBrush = CreateBrush(classColor);
        }
    }

    public void RefreshSkillDisplay(bool refreshEffects)
    {
        if (RefreshSkillLoadoutIfChanged())
        {
            UpdateSkillMetadata(_imagineSkillEntries, _imagineSkillSnapshots);
            UpdateSkillMetadata(_roleSkillEntries, _roleSkillSnapshots);
        }

        UpdateSkillCooldowns(
            _imagineSkillEntries,
            _imagineSkillSnapshots,
            _skillEntityUuid);
        UpdateSkillCooldowns(
            _roleSkillEntries,
            _roleSkillSnapshots,
            _skillEntityUuid);

        if (!refreshEffects)
        {
            return;
        }

        var effectsBySkillId = MeterSnapshotProvider.GetPlayerListSkillEffects(
            CharacterId,
            _skillEntityUuid,
            _trackedSkillIds);
        UpdateSkillEffects(_imagineSkillEntries, effectsBySkillId);
        UpdateSkillEffects(_roleSkillEntries, effectsBySkillId);
    }

    private bool RefreshSkillLoadoutIfChanged()
    {
        if (!MeterSnapshotProvider.TryGetPlayerListImagineRoleSkillSourceState(
                CharacterId,
                _skillEntityUuid,
                out var entityUuid,
                out var professionId,
                out var skillSourceToken,
                out var roleFilterToken))
        {
            return ResetSkillLoadoutCache();
        }

        if (_skillLoadoutInitialized
            && entityUuid == _skillEntityUuid
            && professionId == _skillProfessionId
            && ReferenceEquals(skillSourceToken, _skillSourceToken)
            && ReferenceEquals(roleFilterToken, _roleFilterToken))
        {
            return false;
        }

        var snapshot = MeterSnapshotProvider.GetPlayerListImagineRoleSkills(
            CharacterId,
            entityUuid);
        _skillEntityUuid = snapshot.EntityUuid;
        _skillProfessionId = professionId;
        _skillSourceToken = skillSourceToken;
        _roleFilterToken = roleFilterToken;
        _imagineSkillSnapshots = snapshot.ImagineSkills;
        _roleSkillSnapshots = snapshot.RoleSkills;
        RebuildTrackedSkillIds();
        _skillLoadoutInitialized = true;
        return true;
    }

    private bool ResetSkillLoadoutCache()
    {
        var changed = _skillLoadoutInitialized
            || _imagineSkillSnapshots.Count != 0
            || _roleSkillSnapshots.Count != 0;
        _skillEntityUuid = 0;
        _skillProfessionId = 0;
        _skillSourceToken = null;
        _roleFilterToken = null;
        _imagineSkillSnapshots = Array.Empty<PlayerCooldownSkillSnapshot>();
        _roleSkillSnapshots = Array.Empty<PlayerCooldownSkillSnapshot>();
        _trackedSkillIds.Clear();
        _skillLoadoutInitialized = false;
        return changed;
    }

    private void RebuildTrackedSkillIds()
    {
        _trackedSkillIds.Clear();
        foreach (var snapshot in _imagineSkillSnapshots)
        {
            if (snapshot.SkillId > 0)
            {
                _trackedSkillIds.Add(snapshot.SkillId);
            }
        }

        foreach (var snapshot in _roleSkillSnapshots)
        {
            if (snapshot.SkillId > 0)
            {
                _trackedSkillIds.Add(snapshot.SkillId);
            }
        }
    }

    private static void FillSkillSlots(
        ObservableCollection<PlayerImagineRoleSkillEntry> entries,
        int count,
        bool isImagine)
    {
        for (var index = 0; index < count; index++)
        {
            entries.Add(new PlayerImagineRoleSkillEntry(isImagine));
        }
    }

    private static void UpdateSkillMetadata(
        IReadOnlyList<PlayerImagineRoleSkillEntry> entries,
        IReadOnlyList<PlayerCooldownSkillSnapshot> snapshots)
    {
        for (var index = 0; index < entries.Count; index++)
        {
            entries[index].UpdateSkill(index < snapshots.Count ? snapshots[index] : null);
        }
    }

    private static void UpdateSkillCooldowns(
        IReadOnlyList<PlayerImagineRoleSkillEntry> entries,
        IReadOnlyList<PlayerCooldownSkillSnapshot> snapshots,
        long entityUuid)
    {
        for (var index = 0; index < entries.Count; index++)
        {
            var snapshot = index < snapshots.Count ? snapshots[index] : null;
            var cooldownState = snapshot is null || entityUuid == 0
                ? default
                : SkillCooldownTracker.Instance.GetDisplayState(
                    entityUuid,
                    snapshot.SkillId,
                    snapshot.CooldownSeconds,
                    snapshot.IsImagine,
                    snapshot.MaxCharges,
                    snapshot.ChargeCooldownSeconds);
            entries[index].UpdateCooldown(snapshot, cooldownState);
        }
    }

    private static void UpdateSkillEffects(
        IReadOnlyList<PlayerImagineRoleSkillEntry> entries,
        IReadOnlyDictionary<int, PlayerSkillEffectSnapshot> effectsBySkillId)
    {
        foreach (var entry in entries)
        {
            if (entry.SkillId > 0
                && effectsBySkillId.TryGetValue(entry.SkillId, out var effects))
            {
                entry.UpdateEffects(effects.Buff, effects.Debuff);
            }
            else
            {
                entry.UpdateEffects(null, null);
            }
        }
    }

    partial void OnHealthRatioChanged(double value)
    {
        OnPropertyChanged(nameof(IsHealthFull));
    }

    private void UpdateShieldGeometry(long currentHp, long maxHp, long currentShield)
    {
        if (maxHp <= 0)
        {
            ShieldVisibleRatio = 0d;
            ShieldOverflowRatio = 0d;
            ShieldOverflowStartRatio = 1d;
            return;
        }

        var displayedHp = Math.Clamp(currentHp, 0L, maxHp);
        var shield = Math.Max(currentShield, 0L);
        var shieldGaugeCapacity = (double)maxHp * 2d;
        var availableShieldCapacity = (maxHp - displayedHp) * 2d;
        var foldsEntireShield = shield > availableShieldCapacity;

        var visibleShield = foldsEntireShield
            ? 0d
            : shield;

        var overflowShield = foldsEntireShield
            ? Math.Min(shield, shieldGaugeCapacity)
            : 0d;

        ShieldVisibleRatio = visibleShield / shieldGaugeCapacity;
        ShieldOverflowRatio = overflowShield / shieldGaugeCapacity;
        ShieldOverflowStartRatio = 1d - ShieldOverflowRatio;
    }

    private static double GetRatio(long currentValue, long maxValue)
    {
        if (maxValue <= 0)
        {
            return 0d;
        }

        return Math.Clamp(currentValue / (double)maxValue, 0d, 1d);
    }

    private static string FormatHealthText(
        long currentHp,
        long maxHp,
        long currentShield,
        int displayModeIndex)
    {
        var shield = Math.Max(currentShield, 0L);
        return displayModeIndex == WidgetConfigDefaults.SeparateShieldHealthValueDisplayModeIndex
            ? $"{currentHp}({shield})/{maxHp}"
            : $"{AddSaturating(currentHp, shield)}/{maxHp}";
    }

    private static long AddSaturating(long value, long nonNegativeAddition)
    {
        return nonNegativeAddition > 0 && value > long.MaxValue - nonNegativeAddition
            ? long.MaxValue
            : value + nonNegativeAddition;
    }

    private static Color GetClassColor(MeterWidgetSettingsConfig classColors, string professionKey)
    {
        var palette = classColors.ClassColorPalettes.TryGetValue(professionKey, out var colors)
            ? colors
            : WidgetConfigDefaults.CreateDefaultClassColors(WidgetKind.PlayerList, professionKey);
        var selectedIndex = classColors.ClassColorIndexes.TryGetValue(professionKey, out var index)
            ? index
            : WidgetConfigDefaults.MinClassColorIndex;
        var selectedColor = palette.Count == 0
            ? "#A8A8A8"
            : palette[Math.Clamp(selectedIndex, 0, palette.Count - 1)];

        return ColorUtilities.TryParseHex(selectedColor, out var color)
            ? color
            : Color.FromRgb(0xA8, 0xA8, 0xA8);
    }

    private static SolidColorBrush CreateBrush(Color color)
    {
        var brush = new SolidColorBrush(color);
        brush.Freeze();
        return brush;
    }
}
