using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Windows.Media;
using StarResonanceDps.App.Config;
using StarResonanceDps.App.Services;

namespace StarResonanceDps.App.ViewModels;

public sealed partial class SettingsViewModel : ViewModelBase
{
    private readonly ConfigManager _configManager = ConfigManager.Instance;
    private SettingsConfig _lastSavedSettings;

    [ObservableProperty]
    private int _networkAdapterIndex;

    [ObservableProperty]
    private int _languageIndex;

    [ObservableProperty]
    private int _numberDisplayFormatIndex;

    public SettingsViewModel()
    {
        WindowColors = new ColorPaletteViewModel(AppConfigDefaults.CreateDefaultWindowColors(), AppConfigDefaults.MaxPaletteColorCount);
        WindowColors.PaletteChanged += (_, _) => OnPropertyChanged(nameof(HasUnsavedChanges));

        var settings = _configManager.GetSettingsSnapshot();
        _lastSavedSettings = settings.Clone();
        LoadFromSettings(settings);
    }

    public ColorPaletteViewModel WindowColors { get; }

    public bool HasUnsavedChanges => !SettingsEquals(CreateSettings(), _lastSavedSettings);

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
        LoadFromSettings(AppConfigDefaults.CreateSettings());
        ApplyCurrentGlobalTheme();
        OnPropertyChanged(nameof(HasUnsavedChanges));
    }

    public void RestoreSavedGlobalTheme()
    {
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
            NetworkAdapterIndex = NetworkAdapterIndex,
            LanguageIndex = LanguageIndex,
            NumberDisplayFormatIndex = NumberDisplayFormatIndex,
            WindowColorIndex = WindowColors.SelectedIndex,
            WindowColors = [.. WindowColors.GetHexColors()]
        };

        AppConfigDefaults.NormalizeSettings(settings);
        return settings;
    }

    private void LoadFromSettings(SettingsConfig settings)
    {
        AppConfigDefaults.NormalizeSettings(settings);

        NetworkAdapterIndex = settings.NetworkAdapterIndex;
        LanguageIndex = settings.LanguageIndex;
        NumberDisplayFormatIndex = settings.NumberDisplayFormatIndex;
        WindowColors.Load(settings.WindowColors, settings.WindowColorIndex);
    }

    private void ApplyCurrentGlobalTheme()
    {
        ThemeManager.Instance.ApplyGlobalTheme(CreateSettings());
    }

    private static bool SettingsEquals(SettingsConfig left, SettingsConfig right)
    {
        AppConfigDefaults.NormalizeSettings(left);
        AppConfigDefaults.NormalizeSettings(right);

        return left.NetworkAdapterIndex == right.NetworkAdapterIndex
            && left.LanguageIndex == right.LanguageIndex
            && left.NumberDisplayFormatIndex == right.NumberDisplayFormatIndex
            && left.WindowColorIndex == right.WindowColorIndex
            && left.WindowColors.SequenceEqual(right.WindowColors, StringComparer.OrdinalIgnoreCase);
    }

    partial void OnNetworkAdapterIndexChanged(int value)
    {
        OnPropertyChanged(nameof(HasUnsavedChanges));
    }

    partial void OnLanguageIndexChanged(int value)
    {
        OnPropertyChanged(nameof(HasUnsavedChanges));
    }

    partial void OnNumberDisplayFormatIndexChanged(int value)
    {
        OnPropertyChanged(nameof(HasUnsavedChanges));
    }
}
