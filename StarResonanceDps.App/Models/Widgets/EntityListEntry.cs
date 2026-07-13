using System.Windows.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using StarResonanceDps.App.Config;
using StarResonanceDps.App.Localization;
using StarResonanceDps.App.Services;
using StarResonanceDps.Core.CombatRuntime;
using StarResonanceDps.Core.Models;
using Zproto;
using MonsterClassification = StarResonanceDps.Core.CombatRuntime.EMonsterType;

namespace StarResonanceDps.App.Models.Widgets;

public sealed partial class EntityListEntry : ObservableObject
{
    private EntityListEntry(long entityUuid)
    {
        EntityUuid = entityUuid;
    }

    public long EntityUuid { get; }

    [ObservableProperty]
    private string _classificationKey = "Unknown";

    [ObservableProperty]
    private string _classificationDisplayName = string.Empty;

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
    private bool _hasBreakGauge;

    [ObservableProperty]
    private double _breakRatio;

    [ObservableProperty]
    private string _breakText = string.Empty;

    [ObservableProperty]
    private bool _isInvulnerable;

    [ObservableProperty]
    private bool _isBreakLocked;

    [ObservableProperty]
    private EntityCampRelation _campRelation = EntityCampRelation.NonHostile;

    [ObservableProperty]
    private SolidColorBrush _classBrush = CreateBrush(Color.FromRgb(0xA8, 0xA8, 0xA8));

    public bool UsesFriendlyHealthBar => CampRelation != EntityCampRelation.Hostile;

    public static EntityListEntry Create(
        NearbyEntityEntry entity,
        MeterWidgetSettingsConfig settings)
    {
        var entry = new EntityListEntry(entity.EntityUuid);
        entry.Update(entity, settings);
        return entry;
    }

    public void Update(
        NearbyEntityEntry entity,
        MeterWidgetSettingsConfig settings)
    {
        ClassificationKey = GetClassificationKey(entity);
        ClassificationDisplayName = LocalizationManager.Instance.GetString($"Classes_{ClassificationKey}");
        DisplayName = EntityInfoFormatFormatter.Format(entity, settings.PlayerInfoFormatString);
        HealthRatio = GetRatio(entity.CurrentHp, entity.MaxHp);
        UpdateShieldGeometry(entity.CurrentHp, entity.MaxHp, entity.CurrentShield);
        HealthText = FormatHealthText(
            entity.CurrentHp,
            entity.MaxHp,
            entity.CurrentShield,
            settings.HealthValueDisplayModeIndex);
        HasBreakGauge = entity.MaxBreak > 0;
        BreakRatio = HasBreakGauge ? GetRatio(entity.CurrentBreak, entity.MaxBreak) : 0d;
        BreakText = HasBreakGauge ? FormatValuePair(entity.CurrentBreak, entity.MaxBreak) : string.Empty;
        IsInvulnerable = entity.IsInvulnerable;
        IsBreakLocked = entity.IsBreakLocked;
        CampRelation = entity.CampRelation;

        var classColor = GetClassColor(settings, ClassificationKey);
        if (ClassBrush.Color != classColor)
        {
            ClassBrush = CreateBrush(classColor);
        }
    }

    partial void OnCampRelationChanged(EntityCampRelation value)
    {
        OnPropertyChanged(nameof(UsesFriendlyHealthBar));
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
        var availableHealthCapacity = maxHp - displayedHp;
        var foldsEntireShield = shield > availableHealthCapacity;

        var visibleShield = foldsEntireShield
            ? 0L
            : shield;

        var overflowShield = foldsEntireShield
            ? Math.Min(shield, maxHp)
            : 0L;

        ShieldVisibleRatio = visibleShield / (double)maxHp;
        ShieldOverflowRatio = overflowShield / (double)maxHp;
        ShieldOverflowStartRatio = 1d - ShieldOverflowRatio;
    }

    private static string GetClassificationKey(NearbyEntityEntry entity)
    {
        if (entity.EntityType == EEntityType.EntMonster)
        {
            return entity.MonsterType switch
            {
                MonsterClassification.Boss => "Boss",
                MonsterClassification.Elite => "Elite",
                MonsterClassification.Monster => "Monster",
                _ => "Unknown"
            };
        }

        return "Unknown";
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

    private static string FormatValuePair(long currentValue, long maxValue)
    {
        return $"{currentValue}/{maxValue}";
    }

    private static Color GetClassColor(MeterWidgetSettingsConfig classColors, string classificationKey)
    {
        var palette = classColors.ClassColorPalettes.TryGetValue(classificationKey, out var colors)
            ? colors
            : WidgetConfigDefaults.CreateDefaultClassColors(WidgetKind.EntityList, classificationKey);
        var selectedIndex = classColors.ClassColorIndexes.TryGetValue(classificationKey, out var index)
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
