using System.Windows.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using StarResonanceDps.App.Config;
using StarResonanceDps.App.Localization;
using StarResonanceDps.App.Services;
using StarResonanceDps.Core.Models;

namespace StarResonanceDps.App.Models.Widgets;

public sealed partial class PlayerListEntry : ObservableObject
{
    private PlayerListEntry(long characterId)
    {
        CharacterId = characterId;
    }

    public long CharacterId { get; }

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
    private bool _showStaminaGauge;

    [ObservableProperty]
    private double _staminaRatio;

    [ObservableProperty]
    private string _staminaText = string.Empty;

    [ObservableProperty]
    private bool _isNpc;

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

        var displayPlayer = IsNpc
            ? player with
            {
                Name = LocalizationManager.Instance.GetString($"Classes_{ProfessionKey}")
            }
            : player;
        DisplayName = PlayerInfoFormatFormatter.Format(
            displayPlayer,
            settings.PlayerInfoFormatString,
            playerNameDisplayMode);

        HealthRatio = GetRatio(player.CurrentHp, player.MaxHp, 1d);
        UpdateShieldGeometry(player.CurrentHp, player.MaxHp, player.CurrentShield);
        HealthText = FormatHealthText(
            player.CurrentHp,
            player.MaxHp,
            player.CurrentShield,
            settings.HealthValueDisplayModeIndex);

        ShowStaminaGauge = settings.StaminaGaugeDisplayModeIndex
            == WidgetConfigDefaults.VisibleStaminaGaugeDisplayModeIndex;
        StaminaRatio = GetRatio(player.CurrentStamina, player.MaxStamina, 0d);
        StaminaText = FormatValuePair(player.CurrentStamina, player.MaxStamina);

        var classColor = GetClassColor(settings, ProfessionKey);
        if (ClassBrush.Color != classColor)
        {
            ClassBrush = CreateBrush(classColor);
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

    private static double GetRatio(long currentValue, long maxValue, double valueWhenMaximumIsUnavailable)
    {
        if (maxValue <= 0)
        {
            return valueWhenMaximumIsUnavailable;
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
