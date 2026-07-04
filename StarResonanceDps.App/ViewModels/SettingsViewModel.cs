using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Windows.Media;
using StarResonanceDps.App.Config;
using StarResonanceDps.App.Localization;
using StarResonanceDps.App.Services;
using StarResonanceDps.Core.Models;
using StarResonanceDps.Core.Services;

namespace StarResonanceDps.App.ViewModels;

public sealed partial class SettingsViewModel : ViewModelBase, IDisposable
{
    private readonly ConfigManager _configManager = ConfigManager.Instance;
    private SettingsConfig _lastSavedSettings;
    private bool _isLoadingSettings;

    [ObservableProperty]
    private int _languageIndex;

    [ObservableProperty]
    private int _numberDisplayFormatIndex;

    [ObservableProperty]
    private int _playerNameDisplayModeIndex;

    public SettingsViewModel()
    {
        WindowColors = new ColorPaletteViewModel(AppConfigDefaults.CreateDefaultWindowColors(), AppConfigDefaults.MaxPaletteColorCount);
        WindowColors.PaletteChanged += (_, _) => OnPropertyChanged(nameof(HasUnsavedChanges));

        var settings = _configManager.GetSettingsSnapshot();
        ClassColors = new ClassColorSettingsViewModel(settings.ClassColors);
        ClassColors.SettingsChanged += ClassColors_SettingsChanged;
        _lastSavedSettings = settings.Clone();
        LoadFromSettings(settings, applyLanguage: false);
    }

    public ColorPaletteViewModel WindowColors { get; }

    public ClassColorSettingsViewModel ClassColors { get; }

    public bool HasUnsavedChanges => !SettingsEquals(CreateSettings(), _lastSavedSettings);

    public void Dispose()
    {
        ClassColors.SettingsChanged -= ClassColors_SettingsChanged;
        ClassColors.Dispose();
    }

    [RelayCommand]
    private void Save()
    {
        SaveSettings();
    }

    [RelayCommand]
    private void Reset()
    {
        ResetToDefaults();
    }

    public void SaveSettings()
    {
        var settings = CreateSettings();
        _configManager.SaveSettings(settings);
        _lastSavedSettings = settings.Clone();
        ApplyCurrentGlobalTheme();
        OnPropertyChanged(nameof(HasUnsavedChanges));
    }

    public void ResetToDefaults()
    {
        LoadFromSettings(AppConfigDefaults.CreateSettings(), applyLanguage: true);
        ApplyCurrentGlobalTheme();
        OnPropertyChanged(nameof(HasUnsavedChanges));
    }

    public void RestoreSavedSettingsPreview()
    {
        LocalizationManager.Instance.ApplyLanguageIndex(_lastSavedSettings.LanguageIndex);
        ApplyPlayerNameDisplayModePreview(_lastSavedSettings.PlayerNameDisplayModeIndex);
        ThemeManager.Instance.ApplyGlobalTheme(_lastSavedSettings);
    }

    public Color GetSelectedWindowColor()
    {
        return WindowColors.SelectedColor;
    }

    public void ApplyWindowColor(Color color)
    {
        WindowColors.AddOrSelect(color);
        ApplyCurrentGlobalTheme();
        OnPropertyChanged(nameof(HasUnsavedChanges));
    }

    private SettingsConfig CreateSettings()
    {
        var settings = new SettingsConfig
        {
            LanguageIndex = LanguageIndex,
            NumberDisplayFormatIndex = NumberDisplayFormatIndex,
            PlayerNameDisplayModeIndex = PlayerNameDisplayModeIndex,
            WindowColorIndex = WindowColors.SelectedIndex,
            WindowColors = [.. WindowColors.GetHexColors()],
            ClassColors = ClassColors.CreateConfig()
        };

        AppConfigDefaults.NormalizeSettings(settings);
        return settings;
    }

    private void LoadFromSettings(SettingsConfig settings, bool applyLanguage)
    {
        AppConfigDefaults.NormalizeSettings(settings);

        _isLoadingSettings = true;
        try
        {
            LanguageIndex = settings.LanguageIndex;
            NumberDisplayFormatIndex = settings.NumberDisplayFormatIndex;
            PlayerNameDisplayModeIndex = settings.PlayerNameDisplayModeIndex;
            WindowColors.Load(settings.WindowColors, settings.WindowColorIndex);
            ClassColors.Load(settings.ClassColors);
        }
        finally
        {
            _isLoadingSettings = false;
        }

        ApplyPlayerNameDisplayModePreview(PlayerNameDisplayModeIndex);

        if (applyLanguage)
        {
            LocalizationManager.Instance.ApplyLanguageIndex(LanguageIndex);
        }
    }

    private void ApplyCurrentGlobalTheme()
    {
        ThemeManager.Instance.ApplyGlobalTheme(CreateSettings());
    }

    private static void ApplyPlayerNameDisplayModePreview(int playerNameDisplayModeIndex)
    {
        PlayerRosterPresentationStore.Instance.SetNameDisplayMode(
            (PlayerNameDisplayMode)playerNameDisplayModeIndex);
    }

    private static bool SettingsEquals(SettingsConfig left, SettingsConfig right)
    {
        AppConfigDefaults.NormalizeSettings(left);
        AppConfigDefaults.NormalizeSettings(right);

        return left.LanguageIndex == right.LanguageIndex
            && left.NumberDisplayFormatIndex == right.NumberDisplayFormatIndex
            && left.PlayerNameDisplayModeIndex == right.PlayerNameDisplayModeIndex
            && left.WindowColorIndex == right.WindowColorIndex
            && left.WindowColors.SequenceEqual(right.WindowColors, StringComparer.OrdinalIgnoreCase)
            && ClassColorsEqual(left.ClassColors, right.ClassColors);
    }

    private static bool ClassColorsEqual(ClassColorSettingsConfig left, ClassColorSettingsConfig right)
    {
        var normalizedLeft = AppConfigDefaults.CloneNormalizedClassColorSettings(left);
        var normalizedRight = AppConfigDefaults.CloneNormalizedClassColorSettings(right);

        foreach (var key in AppConfigDefaults.ClassColorKeys)
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

    private void ClassColors_SettingsChanged(object? sender, EventArgs e)
    {
        if (!_isLoadingSettings)
        {
            OnPropertyChanged(nameof(HasUnsavedChanges));
        }
    }

    partial void OnLanguageIndexChanged(int value)
    {
        if (!_isLoadingSettings)
        {
            LocalizationManager.Instance.ApplyLanguageIndex(value);
        }

        OnPropertyChanged(nameof(HasUnsavedChanges));
    }

    partial void OnNumberDisplayFormatIndexChanged(int value)
    {
        OnPropertyChanged(nameof(HasUnsavedChanges));
    }

    partial void OnPlayerNameDisplayModeIndexChanged(int value)
    {
        if (!_isLoadingSettings)
        {
            ApplyPlayerNameDisplayModePreview(value);
        }

        OnPropertyChanged(nameof(HasUnsavedChanges));
    }
}
