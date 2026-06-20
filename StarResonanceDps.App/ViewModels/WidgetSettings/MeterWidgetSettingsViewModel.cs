using CommunityToolkit.Mvvm.ComponentModel;
using System.Windows.Media;
using StarResonanceDps.App.Config;
using StarResonanceDps.App.ViewModels;

namespace StarResonanceDps.App.ViewModels.WidgetSettings;

public sealed partial class MeterWidgetSettingsViewModel : ObservableObject
{
    public const string ShieldKnightKey = "ShieldKnight";
    public const string HeavyGuardianKey = "HeavyGuardian";
    public const string VerdantOracleKey = "VerdantOracle";
    public const string SoulMusicianKey = "SoulMusician";
    public const string FlameBerserkerKey = "FlameBerserker";
    public const string StormbladeKey = "Stormblade";
    public const string FrostMageKey = "FrostMage";
    public const string WindKnightKey = "WindKnight";
    public const string MarksmanKey = "Marksman";
    public const string TransformationKey = "Transformation";
    public const string EnemyKey = "Enemy";
    public const string UnknownKey = "Unknown";

    private MeterWidgetSettingsConfig _lastSaved;

    [ObservableProperty]
    private double _classColorOpacity = 100;

    public MeterWidgetSettingsViewModel(MeterWidgetSettingsConfig? config)
    {
        ShieldKnightClassColors = CreateClassPalette(ShieldKnightKey);
        HeavyGuardianClassColors = CreateClassPalette(HeavyGuardianKey);
        VerdantOracleClassColors = CreateClassPalette(VerdantOracleKey);
        SoulMusicianClassColors = CreateClassPalette(SoulMusicianKey);
        FlameBerserkerClassColors = CreateClassPalette(FlameBerserkerKey);
        StormbladeClassColors = CreateClassPalette(StormbladeKey);
        FrostMageClassColors = CreateClassPalette(FrostMageKey);
        WindKnightClassColors = CreateClassPalette(WindKnightKey);
        MarksmanClassColors = CreateClassPalette(MarksmanKey);
        TransformationClassColors = CreateClassPalette(TransformationKey);
        EnemyClassColors = CreateClassPalette(EnemyKey);
        UnknownClassColors = CreateClassPalette(UnknownKey);

        foreach (var palette in GetAllPalettes())
        {
            palette.PaletteChanged += (_, _) => NotifyChanged();
        }

        _lastSaved = WidgetConfigDefaults.CloneNormalizedMeter(config);
        LoadFromConfig(_lastSaved);
    }

    public bool HasUnsavedChanges => !SettingsEquals(CreateConfig(), _lastSaved);

    public ColorPaletteViewModel ShieldKnightClassColors { get; }
    public ColorPaletteViewModel HeavyGuardianClassColors { get; }
    public ColorPaletteViewModel VerdantOracleClassColors { get; }
    public ColorPaletteViewModel SoulMusicianClassColors { get; }
    public ColorPaletteViewModel FlameBerserkerClassColors { get; }
    public ColorPaletteViewModel StormbladeClassColors { get; }
    public ColorPaletteViewModel FrostMageClassColors { get; }
    public ColorPaletteViewModel WindKnightClassColors { get; }
    public ColorPaletteViewModel MarksmanClassColors { get; }
    public ColorPaletteViewModel TransformationClassColors { get; }
    public ColorPaletteViewModel EnemyClassColors { get; }
    public ColorPaletteViewModel UnknownClassColors { get; }

    public MeterWidgetSettingsConfig CreateConfig()
    {
        var config = new MeterWidgetSettingsConfig
        {
            ClassColorIndexes = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase),
            ClassColorPalettes = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase),
            ClassColorOpacity = Math.Clamp(
                (int)Math.Round(ClassColorOpacity, MidpointRounding.AwayFromZero),
                WidgetConfigDefaults.MinClassColorOpacity,
                WidgetConfigDefaults.MaxClassColorOpacity)
        };

        AddPalette(config, ShieldKnightKey, ShieldKnightClassColors);
        AddPalette(config, HeavyGuardianKey, HeavyGuardianClassColors);
        AddPalette(config, VerdantOracleKey, VerdantOracleClassColors);
        AddPalette(config, SoulMusicianKey, SoulMusicianClassColors);
        AddPalette(config, FlameBerserkerKey, FlameBerserkerClassColors);
        AddPalette(config, StormbladeKey, StormbladeClassColors);
        AddPalette(config, FrostMageKey, FrostMageClassColors);
        AddPalette(config, WindKnightKey, WindKnightClassColors);
        AddPalette(config, MarksmanKey, MarksmanClassColors);
        AddPalette(config, TransformationKey, TransformationClassColors);
        AddPalette(config, EnemyKey, EnemyClassColors);
        AddPalette(config, UnknownKey, UnknownClassColors);

        WidgetConfigDefaults.NormalizeMeter(config);
        return config;
    }

    public void ResetToDefaults()
    {
        LoadFromConfig(WidgetConfigDefaults.CreateMeterSettings());
        OnPropertyChanged(nameof(HasUnsavedChanges));
    }

    public void MarkSaved(MeterWidgetSettingsConfig config)
    {
        _lastSaved = WidgetConfigDefaults.CloneNormalizedMeter(config);
        LoadFromConfig(_lastSaved);
        OnPropertyChanged(nameof(HasUnsavedChanges));
    }

    public Color GetSelectedClassColor(string key)
    {
        return GetPalette(key).SelectedColor;
    }

    public void ApplyClassColor(string key, Color color)
    {
        GetPalette(key).AddOrSelect(color);
        NotifyChanged();
    }

    public ColorPaletteViewModel GetPalette(string key)
    {
        return key switch
        {
            ShieldKnightKey => ShieldKnightClassColors,
            HeavyGuardianKey => HeavyGuardianClassColors,
            VerdantOracleKey => VerdantOracleClassColors,
            SoulMusicianKey => SoulMusicianClassColors,
            FlameBerserkerKey => FlameBerserkerClassColors,
            StormbladeKey => StormbladeClassColors,
            FrostMageKey => FrostMageClassColors,
            WindKnightKey => WindKnightClassColors,
            MarksmanKey => MarksmanClassColors,
            TransformationKey => TransformationClassColors,
            EnemyKey => EnemyClassColors,
            UnknownKey => UnknownClassColors,
            _ => UnknownClassColors
        };
    }

    private void LoadFromConfig(MeterWidgetSettingsConfig config)
    {
        var normalized = WidgetConfigDefaults.CloneNormalizedMeter(config);
        LoadPalette(normalized, ShieldKnightKey, ShieldKnightClassColors);
        LoadPalette(normalized, HeavyGuardianKey, HeavyGuardianClassColors);
        LoadPalette(normalized, VerdantOracleKey, VerdantOracleClassColors);
        LoadPalette(normalized, SoulMusicianKey, SoulMusicianClassColors);
        LoadPalette(normalized, FlameBerserkerKey, FlameBerserkerClassColors);
        LoadPalette(normalized, StormbladeKey, StormbladeClassColors);
        LoadPalette(normalized, FrostMageKey, FrostMageClassColors);
        LoadPalette(normalized, WindKnightKey, WindKnightClassColors);
        LoadPalette(normalized, MarksmanKey, MarksmanClassColors);
        LoadPalette(normalized, TransformationKey, TransformationClassColors);
        LoadPalette(normalized, EnemyKey, EnemyClassColors);
        LoadPalette(normalized, UnknownKey, UnknownClassColors);
        ClassColorOpacity = normalized.ClassColorOpacity;
    }

    private IEnumerable<ColorPaletteViewModel> GetAllPalettes()
    {
        yield return ShieldKnightClassColors;
        yield return HeavyGuardianClassColors;
        yield return VerdantOracleClassColors;
        yield return SoulMusicianClassColors;
        yield return FlameBerserkerClassColors;
        yield return StormbladeClassColors;
        yield return FrostMageClassColors;
        yield return WindKnightClassColors;
        yield return MarksmanClassColors;
        yield return TransformationClassColors;
        yield return EnemyClassColors;
        yield return UnknownClassColors;
    }

    private static ColorPaletteViewModel CreateClassPalette(string key)
    {
        return new ColorPaletteViewModel(
            WidgetConfigDefaults.CreateDefaultClassColors(key),
            WidgetConfigDefaults.MaxPaletteColorCount);
    }

    private static void LoadPalette(MeterWidgetSettingsConfig config, string key, ColorPaletteViewModel palette)
    {
        var colors = config.ClassColorPalettes.TryGetValue(key, out var value)
            ? value
            : WidgetConfigDefaults.CreateDefaultClassColors(key);
        var index = config.ClassColorIndexes.TryGetValue(key, out var savedIndex)
            ? savedIndex
            : WidgetConfigDefaults.MinClassColorIndex;
        palette.Load(colors, index);
    }

    private static void AddPalette(MeterWidgetSettingsConfig config, string key, ColorPaletteViewModel palette)
    {
        config.ClassColorIndexes[key] = palette.SelectedIndex;
        config.ClassColorPalettes[key] = [.. palette.GetHexColors()];
    }

    private static bool SettingsEquals(MeterWidgetSettingsConfig left, MeterWidgetSettingsConfig right)
    {
        var normalizedLeft = WidgetConfigDefaults.CloneNormalizedMeter(left);
        var normalizedRight = WidgetConfigDefaults.CloneNormalizedMeter(right);

        if (normalizedLeft.ClassColorOpacity != normalizedRight.ClassColorOpacity)
        {
            return false;
        }

        foreach (var key in WidgetConfigDefaults.ClassColorKeys)
        {
            if (!normalizedLeft.ClassColorIndexes.TryGetValue(key, out var leftIndex)
                || !normalizedRight.ClassColorIndexes.TryGetValue(key, out var rightIndex)
                || leftIndex != rightIndex)
            {
                return false;
            }

            var leftColors = normalizedLeft.ClassColorPalettes[key];
            var rightColors = normalizedRight.ClassColorPalettes[key];
            if (!leftColors.SequenceEqual(rightColors, StringComparer.OrdinalIgnoreCase))
            {
                return false;
            }
        }

        return true;
    }

    private void NotifyChanged()
    {
        OnPropertyChanged(nameof(HasUnsavedChanges));
    }

    partial void OnClassColorOpacityChanged(double value) => NotifyChanged();
}
