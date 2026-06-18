using CommunityToolkit.Mvvm.ComponentModel;
using StarResonanceDps.App.Config;

namespace StarResonanceDps.App.ViewModels.WidgetSettings;

public sealed partial class MeterWidgetSettingsViewModel : ObservableObject
{
    private const string ShieldKnightKey = "ShieldKnight";
    private const string HeavyGuardianKey = "HeavyGuardian";
    private const string VerdantOracleKey = "VerdantOracle";
    private const string SoulMusicianKey = "SoulMusician";
    private const string FlameBerserkerKey = "FlameBerserker";
    private const string StormbladeKey = "Stormblade";
    private const string FrostMageKey = "FrostMage";
    private const string WindKnightKey = "WindKnight";
    private const string MarksmanKey = "Marksman";
    private const string TransformationKey = "Transformation";
    private const string UnknownKey = "Unknown";

    private MeterWidgetSettingsConfig _lastSaved;

    [ObservableProperty]
    private int _shieldKnightClassColorIndex;

    [ObservableProperty]
    private int _heavyGuardianClassColorIndex;

    [ObservableProperty]
    private int _verdantOracleClassColorIndex;

    [ObservableProperty]
    private int _soulMusicianClassColorIndex;

    [ObservableProperty]
    private int _flameBerserkerClassColorIndex;

    [ObservableProperty]
    private int _stormbladeClassColorIndex;

    [ObservableProperty]
    private int _frostMageClassColorIndex;

    [ObservableProperty]
    private int _windKnightClassColorIndex;

    [ObservableProperty]
    private int _marksmanClassColorIndex;

    [ObservableProperty]
    private int _transformationClassColorIndex;

    [ObservableProperty]
    private int _unknownClassColorIndex;

    [ObservableProperty]
    private double _classColorOpacity = 100;

    public MeterWidgetSettingsViewModel(MeterWidgetSettingsConfig? config)
    {
        _lastSaved = WidgetConfigDefaults.CloneNormalizedMeter(config);
        LoadFromConfig(_lastSaved);
    }

    public bool HasUnsavedChanges => !SettingsEquals(CreateConfig(), _lastSaved);

    public MeterWidgetSettingsConfig CreateConfig()
    {
        var config = new MeterWidgetSettingsConfig
        {
            ClassColorIndexes = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)
            {
                [ShieldKnightKey] = ClampClassColorIndex(ShieldKnightClassColorIndex),
                [HeavyGuardianKey] = ClampClassColorIndex(HeavyGuardianClassColorIndex),
                [VerdantOracleKey] = ClampClassColorIndex(VerdantOracleClassColorIndex),
                [SoulMusicianKey] = ClampClassColorIndex(SoulMusicianClassColorIndex),
                [FlameBerserkerKey] = ClampClassColorIndex(FlameBerserkerClassColorIndex),
                [StormbladeKey] = ClampClassColorIndex(StormbladeClassColorIndex),
                [FrostMageKey] = ClampClassColorIndex(FrostMageClassColorIndex),
                [WindKnightKey] = ClampClassColorIndex(WindKnightClassColorIndex),
                [MarksmanKey] = ClampClassColorIndex(MarksmanClassColorIndex),
                [TransformationKey] = ClampClassColorIndex(TransformationClassColorIndex),
                [UnknownKey] = ClampClassColorIndex(UnknownClassColorIndex)
            },
            ClassColorOpacity = Math.Clamp(
                (int)Math.Round(ClassColorOpacity, MidpointRounding.AwayFromZero),
                WidgetConfigDefaults.MinClassColorOpacity,
                WidgetConfigDefaults.MaxClassColorOpacity)
        };

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

    private void LoadFromConfig(MeterWidgetSettingsConfig config)
    {
        var normalized = WidgetConfigDefaults.CloneNormalizedMeter(config);
        ShieldKnightClassColorIndex = GetIndex(normalized, ShieldKnightKey);
        HeavyGuardianClassColorIndex = GetIndex(normalized, HeavyGuardianKey);
        VerdantOracleClassColorIndex = GetIndex(normalized, VerdantOracleKey);
        SoulMusicianClassColorIndex = GetIndex(normalized, SoulMusicianKey);
        FlameBerserkerClassColorIndex = GetIndex(normalized, FlameBerserkerKey);
        StormbladeClassColorIndex = GetIndex(normalized, StormbladeKey);
        FrostMageClassColorIndex = GetIndex(normalized, FrostMageKey);
        WindKnightClassColorIndex = GetIndex(normalized, WindKnightKey);
        MarksmanClassColorIndex = GetIndex(normalized, MarksmanKey);
        TransformationClassColorIndex = GetIndex(normalized, TransformationKey);
        UnknownClassColorIndex = GetIndex(normalized, UnknownKey);
        ClassColorOpacity = normalized.ClassColorOpacity;
    }

    private static int GetIndex(MeterWidgetSettingsConfig config, string key)
    {
        return config.ClassColorIndexes.TryGetValue(key, out var index)
            ? ClampClassColorIndex(index)
            : WidgetConfigDefaults.MinClassColorIndex;
    }

    private static int ClampClassColorIndex(int index)
    {
        return Math.Clamp(
            index,
            WidgetConfigDefaults.MinClassColorIndex,
            WidgetConfigDefaults.MaxClassColorIndex);
    }

    private static bool SettingsEquals(MeterWidgetSettingsConfig left, MeterWidgetSettingsConfig right)
    {
        var normalizedLeft = WidgetConfigDefaults.CloneNormalizedMeter(left);
        var normalizedRight = WidgetConfigDefaults.CloneNormalizedMeter(right);

        return GetIndex(normalizedLeft, ShieldKnightKey) == GetIndex(normalizedRight, ShieldKnightKey)
            && GetIndex(normalizedLeft, HeavyGuardianKey) == GetIndex(normalizedRight, HeavyGuardianKey)
            && GetIndex(normalizedLeft, VerdantOracleKey) == GetIndex(normalizedRight, VerdantOracleKey)
            && GetIndex(normalizedLeft, SoulMusicianKey) == GetIndex(normalizedRight, SoulMusicianKey)
            && GetIndex(normalizedLeft, FlameBerserkerKey) == GetIndex(normalizedRight, FlameBerserkerKey)
            && GetIndex(normalizedLeft, StormbladeKey) == GetIndex(normalizedRight, StormbladeKey)
            && GetIndex(normalizedLeft, FrostMageKey) == GetIndex(normalizedRight, FrostMageKey)
            && GetIndex(normalizedLeft, WindKnightKey) == GetIndex(normalizedRight, WindKnightKey)
            && GetIndex(normalizedLeft, MarksmanKey) == GetIndex(normalizedRight, MarksmanKey)
            && GetIndex(normalizedLeft, TransformationKey) == GetIndex(normalizedRight, TransformationKey)
            && GetIndex(normalizedLeft, UnknownKey) == GetIndex(normalizedRight, UnknownKey)
            && normalizedLeft.ClassColorOpacity == normalizedRight.ClassColorOpacity;
    }

    private void NotifyChanged()
    {
        OnPropertyChanged(nameof(HasUnsavedChanges));
    }

    partial void OnShieldKnightClassColorIndexChanged(int value) => NotifyChanged();
    partial void OnHeavyGuardianClassColorIndexChanged(int value) => NotifyChanged();
    partial void OnVerdantOracleClassColorIndexChanged(int value) => NotifyChanged();
    partial void OnSoulMusicianClassColorIndexChanged(int value) => NotifyChanged();
    partial void OnFlameBerserkerClassColorIndexChanged(int value) => NotifyChanged();
    partial void OnStormbladeClassColorIndexChanged(int value) => NotifyChanged();
    partial void OnFrostMageClassColorIndexChanged(int value) => NotifyChanged();
    partial void OnWindKnightClassColorIndexChanged(int value) => NotifyChanged();
    partial void OnMarksmanClassColorIndexChanged(int value) => NotifyChanged();
    partial void OnTransformationClassColorIndexChanged(int value) => NotifyChanged();
    partial void OnUnknownClassColorIndexChanged(int value) => NotifyChanged();
    partial void OnClassColorOpacityChanged(double value) => NotifyChanged();
}
