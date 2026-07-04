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
    private SolidColorBrush _classBrush = CreateBrush(Color.FromRgb(0xA8, 0xA8, 0xA8));

    public bool IsHealthFull => HealthRatio >= 1d;

    public static PlayerListEntry Create(PlayerRosterEntry player, ClassColorSettingsConfig classColors)
    {
        var entry = new PlayerListEntry(player.CharacterId);
        entry.Update(player, classColors);
        return entry;
    }

    public void Update(PlayerRosterEntry player, ClassColorSettingsConfig classColors)
    {
        ProfessionKey = PlayerProfession.GetKey(player.ProfessionId);
        ClassSpecDisplayName = LocalizationManager.Instance.GetString($"ClassSpec_{player.ClassSpec}");
        DisplayName = $"{player.Name}（{player.CombatPower}-S{player.SeasonStrength}）";
        HealthRatio = GetHealthRatio(player.CurrentHp, player.MaxHp);

        var classColor = GetClassColor(classColors, ProfessionKey);
        if (ClassBrush.Color != classColor)
        {
            ClassBrush = CreateBrush(classColor);
        }
    }

    partial void OnHealthRatioChanged(double value)
    {
        OnPropertyChanged(nameof(IsHealthFull));
    }

    private static double GetHealthRatio(long currentHp, long maxHp)
    {
        if (maxHp <= 0)
        {
            return 1d;
        }

        return Math.Clamp(currentHp / (double)maxHp, 0d, 1d);
    }

    private static Color GetClassColor(ClassColorSettingsConfig classColors, string professionKey)
    {
        var palette = classColors.ClassColorPalettes.TryGetValue(professionKey, out var colors)
            ? colors
            : AppConfigDefaults.CreateDefaultClassColors(professionKey);
        var selectedIndex = classColors.ClassColorIndexes.TryGetValue(professionKey, out var index)
            ? index
            : AppConfigDefaults.MinClassColorIndex;
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
