using System.Windows.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using StarResonanceDps.App.Config;
using StarResonanceDps.App.Localization;
using StarResonanceDps.App.Services;

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
        WindowColors.PaletteChanged += WindowColors_PaletteChanged;

        var settings = _configManager.GetSettingsSnapshot();
        _lastSavedSettings = settings.Clone();
        LoadFromSettings(settings, applyLanguage: false, applyPreview: false);
    }

    public ColorPaletteViewModel WindowColors { get; }

    public bool HasUnsavedChanges => !SettingsEquals(CreateSettings(), _lastSavedSettings);

    public void Dispose()
    {
        _configManager.ClearSettingsPreview();
        WindowColors.PaletteChanged -= WindowColors_PaletteChanged;
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
        LoadFromSettings(AppConfigDefaults.CreateSettings(), applyLanguage: true, applyPreview: true);
        ApplyCurrentGlobalTheme();
        OnPropertyChanged(nameof(HasUnsavedChanges));
    }

    public void RestoreSavedSettingsPreview()
    {
        LocalizationManager.Instance.ApplyLanguageIndex(_lastSavedSettings.LanguageIndex);
        _configManager.ClearSettingsPreview();
        ThemeManager.Instance.ApplyGlobalTheme(_lastSavedSettings);
    }

    public Color GetSelectedWindowColor()
    {
        return WindowColors.SelectedColor;
    }

    public void ApplyWindowColor(Color color)
    {
        WindowColors.AddOrSelect(color);
    }

    private SettingsConfig CreateSettings()
    {
        var settings = new SettingsConfig
        {
            LanguageIndex = LanguageIndex,
            NumberDisplayFormatIndex = NumberDisplayFormatIndex,
            PlayerNameDisplayModeIndex = PlayerNameDisplayModeIndex,
            WindowColorIndex = WindowColors.SelectedIndex,
            WindowColors = [.. WindowColors.GetHexColors()]
        };

        AppConfigDefaults.NormalizeSettings(settings);
        return settings;
    }

    private void LoadFromSettings(SettingsConfig settings, bool applyLanguage, bool applyPreview)
    {
        AppConfigDefaults.NormalizeSettings(settings);

        _isLoadingSettings = true;
        try
        {
            LanguageIndex = settings.LanguageIndex;
            NumberDisplayFormatIndex = settings.NumberDisplayFormatIndex;
            PlayerNameDisplayModeIndex = settings.PlayerNameDisplayModeIndex;
            WindowColors.Load(settings.WindowColors, settings.WindowColorIndex);
        }
        finally
        {
            _isLoadingSettings = false;
        }

        if (applyLanguage)
        {
            LocalizationManager.Instance.ApplyLanguageIndex(LanguageIndex);
        }

        if (applyPreview)
        {
            ApplySettingsPreview();
        }
    }

    private void WindowColors_PaletteChanged(object? sender, EventArgs e)
    {
        OnPropertyChanged(nameof(HasUnsavedChanges));

        if (_isLoadingSettings)
        {
            return;
        }

        ApplySettingsPreview();
        ApplyCurrentGlobalTheme();
    }

    private void ApplySettingsPreview()
    {
        _configManager.SetSettingsPreview(CreateSettings());
    }

    private void ApplyCurrentGlobalTheme()
    {
        ThemeManager.Instance.ApplyGlobalTheme(CreateSettings());
    }

    private static bool SettingsEquals(SettingsConfig left, SettingsConfig right)
    {
        AppConfigDefaults.NormalizeSettings(left);
        AppConfigDefaults.NormalizeSettings(right);

        return left.LanguageIndex == right.LanguageIndex
            && left.NumberDisplayFormatIndex == right.NumberDisplayFormatIndex
            && left.PlayerNameDisplayModeIndex == right.PlayerNameDisplayModeIndex
            && left.WindowColorIndex == right.WindowColorIndex
            && left.WindowColors.SequenceEqual(right.WindowColors, StringComparer.OrdinalIgnoreCase);
    }

    partial void OnLanguageIndexChanged(int value)
    {
        if (!_isLoadingSettings)
        {
            LocalizationManager.Instance.ApplyLanguageIndex(value);
            ApplySettingsPreview();
        }

        OnPropertyChanged(nameof(HasUnsavedChanges));
    }

    partial void OnNumberDisplayFormatIndexChanged(int value)
    {
        if (!_isLoadingSettings)
        {
            ApplySettingsPreview();
        }

        OnPropertyChanged(nameof(HasUnsavedChanges));
    }

    partial void OnPlayerNameDisplayModeIndexChanged(int value)
    {
        if (!_isLoadingSettings)
        {
            ApplySettingsPreview();
        }

        OnPropertyChanged(nameof(HasUnsavedChanges));
    }
}
