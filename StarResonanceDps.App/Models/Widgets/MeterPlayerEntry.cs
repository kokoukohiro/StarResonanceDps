using System.Windows.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using StarResonanceDps.App.Config;
using StarResonanceDps.App.Services;
using StarResonanceDps.Core.CombatRuntime;
using StarResonanceDps.Core.Models;

namespace StarResonanceDps.App.Models.Widgets;

public sealed partial class MeterPlayerEntry : ObservableObject
{
    private MeterPlayerEntry(long characterId)
    {
        CharacterId = characterId;
    }

    public long CharacterId { get; }

    public long PlayerId { get; private set; }

    [ObservableProperty]
    private int _rank;

    [ObservableProperty]
    private string _professionKey = "Unknown";

    [ObservableProperty]
    private string _displayName = string.Empty;

    [ObservableProperty]
    private string _valueText = string.Empty;

    [ObservableProperty]
    private double _barRatio;

    [ObservableProperty]
    private SolidColorBrush _classBrush = CreateBrush(Color.FromRgb(0xA8, 0xA8, 0xA8));

    [ObservableProperty]
    private bool _isPlayerSelectionMenuOpen;

    public static MeterPlayerEntry Create(
        MeterPlayerSnapshot player,
        int rank,
        MeterWidgetSettingsConfig settings,
        WidgetKind widgetKind,
        int numberDisplayFormatIndex,
        PlayerNameDisplayMode playerNameDisplayMode)
    {
        var entry = new MeterPlayerEntry(player.CharacterId);
        entry.Update(player, rank, settings, widgetKind, numberDisplayFormatIndex, playerNameDisplayMode);
        return entry;
    }

    public void Update(
        MeterPlayerSnapshot player,
        int rank,
        MeterWidgetSettingsConfig settings,
        WidgetKind widgetKind,
        int numberDisplayFormatIndex,
        PlayerNameDisplayMode playerNameDisplayMode)
    {
        Rank = rank;
        PlayerId = player.UserId != 0 ? player.UserId : player.CharacterId;
        ProfessionKey = PlayerProfession.GetKey(player.ProfessionId);
        DisplayName = PlayerInfoFormatFormatter.Format(player, settings.PlayerInfoFormatString, playerNameDisplayMode);
        ValueText = $"{MeterNumberFormatter.Format(player.TotalValue, numberDisplayFormatIndex)} ({MeterNumberFormatter.Format(player.ValuePerSecond, numberDisplayFormatIndex)}) {player.Contribution:F0}%";
        BarRatio = player.BarRatio;

        var classColor = GetClassColor(settings, widgetKind, ProfessionKey);
        if (ClassBrush.Color != classColor)
        {
            ClassBrush = CreateBrush(classColor);
        }
    }

    private static Color GetClassColor(MeterWidgetSettingsConfig settings, WidgetKind widgetKind, string professionKey)
    {
        var palette = settings.ClassColorPalettes.TryGetValue(professionKey, out var colors)
            ? colors
            : WidgetConfigDefaults.CreateDefaultClassColors(widgetKind, professionKey);
        var selectedIndex = settings.ClassColorIndexes.TryGetValue(professionKey, out var index)
            ? index
            : WidgetConfigDefaults.MinClassColorIndex;
        var selectedColor = palette.Count == 0
            ? "#A8A8A8"
            : palette[Math.Clamp(selectedIndex, 0, palette.Count - 1)];
        var color = ColorUtilities.TryParseHex(selectedColor, out var parsed)
            ? parsed
            : Color.FromRgb(0xA8, 0xA8, 0xA8);
        var opacity = Math.Clamp(settings.ClassColorOpacity, WidgetConfigDefaults.MinClassColorOpacity, WidgetConfigDefaults.MaxClassColorOpacity);

        return Color.FromArgb(
            (byte)Math.Round(opacity / 100d * byte.MaxValue, MidpointRounding.AwayFromZero),
            color.R,
            color.G,
            color.B);
    }

    private static SolidColorBrush CreateBrush(Color color)
    {
        var brush = new SolidColorBrush(color);
        brush.Freeze();
        return brush;
    }
}
