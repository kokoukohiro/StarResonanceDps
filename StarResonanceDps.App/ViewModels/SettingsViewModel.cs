using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using StarResonanceDps.App.Config;

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

    [ObservableProperty]
    private int _windowColorIndex;

    [ObservableProperty]
    private int _textColorIndex;

    public SettingsViewModel()
    {
        var settings = _configManager.GetSettingsSnapshot();
        _lastSavedSettings = settings.Clone();
        LoadFromSettings(settings);
    }

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
        OnPropertyChanged(nameof(HasUnsavedChanges));
    }

    public void ResetToDefaults()
    {
        LoadFromSettings(AppConfigDefaults.CreateSettings());
        OnPropertyChanged(nameof(HasUnsavedChanges));
    }

    private SettingsConfig CreateSettings()
    {
        var settings = new SettingsConfig
        {
            NetworkAdapterIndex = NetworkAdapterIndex,
            LanguageIndex = LanguageIndex,
            NumberDisplayFormatIndex = NumberDisplayFormatIndex,
            WindowColorIndex = WindowColorIndex,
            TextColorIndex = TextColorIndex
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
        WindowColorIndex = settings.WindowColorIndex;
        TextColorIndex = settings.TextColorIndex;
    }

    private static bool SettingsEquals(SettingsConfig left, SettingsConfig right)
    {
        return left.NetworkAdapterIndex == right.NetworkAdapterIndex
            && left.LanguageIndex == right.LanguageIndex
            && left.NumberDisplayFormatIndex == right.NumberDisplayFormatIndex
            && left.WindowColorIndex == right.WindowColorIndex
            && left.TextColorIndex == right.TextColorIndex;
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

    partial void OnWindowColorIndexChanged(int value)
    {
        OnPropertyChanged(nameof(HasUnsavedChanges));
    }

    partial void OnTextColorIndexChanged(int value)
    {
        OnPropertyChanged(nameof(HasUnsavedChanges));
    }
}
