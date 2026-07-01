using System.Windows.Media;
using StarResonanceDps.App.Config;
using StarResonanceDps.App.Localization;
using StarResonanceDps.App.Services;
using StarResonanceDps.Core.Models;

namespace StarResonanceDps.App.Models.Widgets;

public sealed class PlayerListEntry
{
    private PlayerListEntry(
        long characterId,
        string professionKey,
        string classSpecDisplayName,
        string displayName,
        double healthRatio,
        SolidColorBrush classBrush)
    {
        CharacterId = characterId;
        ProfessionKey = professionKey;
        ClassSpecDisplayName = classSpecDisplayName;
        DisplayName = displayName;
        HealthRatio = healthRatio;
        ClassBrush = classBrush;
    }

    public long CharacterId { get; }

    public string ProfessionKey { get; }

    public string ClassSpecDisplayName { get; }

    public string DisplayName { get; }

    public double HealthRatio { get; }

    public SolidColorBrush ClassBrush { get; }

    public static PlayerListEntry Create(PlayerRosterEntry player, MeterWidgetSettingsConfig meterSettings)
    {
        var professionKey = GetProfessionKey(player.ProfessionId);
        var classColor = GetClassColor(meterSettings, professionKey);
        var classSpecDisplayName = LocalizationManager.Instance.GetString($"ClassSpec_{player.ClassSpec}");
        var displayName = $"{player.Name}（{player.CombatPower}-{player.SeasonStrength}）";

        return new PlayerListEntry(
            player.CharacterId,
            professionKey,
            classSpecDisplayName,
            displayName,
            GetHealthRatio(player.CurrentHp, player.MaxHp),
            CreateBrush(classColor, meterSettings.ClassColorOpacity));
    }

    private static string GetProfessionKey(int professionId)
    {
        return professionId switch
        {
            1 => "Stormblade",
            2 => "FrostMage",
            3 => "FlameBerserker",
            4 => "WindKnight",
            5 => "VerdantOracle",
            9 => "HeavyGuardian",
            11 => "Marksman",
            12 => "ShieldKnight",
            13 => "SoulMusician",
            _ => "Unknown"
        };
    }

    private static double GetHealthRatio(long currentHp, long maxHp)
    {
        if (maxHp <= 0)
        {
            return 1d;
        }

        return Math.Clamp(currentHp / (double)maxHp, 0d, 1d);
    }

    private static Color GetClassColor(MeterWidgetSettingsConfig meterSettings, string professionKey)
    {
        var palette = meterSettings.ClassColorPalettes.TryGetValue(professionKey, out var colors)
            ? colors
            : WidgetConfigDefaults.CreateDefaultClassColors(professionKey);
        var selectedIndex = meterSettings.ClassColorIndexes.TryGetValue(professionKey, out var index)
            ? index
            : WidgetConfigDefaults.MinClassColorIndex;
        var selectedColor = palette.Count == 0
            ? "#A8A8A8"
            : palette[Math.Clamp(selectedIndex, 0, palette.Count - 1)];

        return ColorUtilities.TryParseHex(selectedColor, out var color)
            ? color
            : Color.FromRgb(0xA8, 0xA8, 0xA8);
    }

    private static SolidColorBrush CreateBrush(Color color, int opacityPercent)
    {
        var alpha = checked((byte)Math.Round(color.A * Math.Clamp(opacityPercent, 0, 100) / 100d));
        var brush = new SolidColorBrush(Color.FromArgb(alpha, color.R, color.G, color.B));
        brush.Freeze();
        return brush;
    }
}
