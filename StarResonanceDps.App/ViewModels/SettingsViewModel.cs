using System.ComponentModel;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Windows.Media;
using StarResonanceDps.App.Config;
using StarResonanceDps.App.Localization;
using StarResonanceDps.App.Services;
using StarResonanceDps.Core.Models;
using StarResonanceDps.Core.Services;

namespace StarResonanceDps.App.ViewModels;

public sealed partial class SettingsViewModel : ViewModelBase
{
    private static readonly bool IsInDesignMode =
        DesignerProperties.GetIsInDesignMode(new DependencyObject());

    private readonly ConfigManager _configManager = ConfigManager.Instance;
    private readonly NetworkAdapterSession _networkAdapterSession = NetworkAdapterSession.Instance;
    private SettingsConfig _lastSavedSettings;
    private bool _isLoadingSettings;
    private bool _isLoadingNetworkAdapters;

    [ObservableProperty]
    private IReadOnlyList<NetworkAdapterInfo> _availableNetworkAdapters = [];

    [ObservableProperty]
    private NetworkAdapterInfo? _selectedNetworkAdapter;

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
        LoadFromSettings(settings, applyLanguage: false);
        LoadNetworkAdapters();
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
        LoadFromSettings(AppConfigDefaults.CreateSettings(), applyLanguage: true);
        ApplyCurrentGlobalTheme();
        OnPropertyChanged(nameof(HasUnsavedChanges));
    }

    public void RestoreSavedSettingsPreview()
    {
        LocalizationManager.Instance.ApplyLanguageIndex(_lastSavedSettings.LanguageIndex);
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
            WindowColorIndex = WindowColors.SelectedIndex,
            WindowColors = [.. WindowColors.GetHexColors()]
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
            && left.WindowColorIndex == right.WindowColorIndex
            && left.WindowColors.SequenceEqual(right.WindowColors, StringComparer.OrdinalIgnoreCase);
    }

    private void LoadNetworkAdapters()
    {
        if (IsInDesignMode)
        {
            return;
        }

        _isLoadingNetworkAdapters = true;
        try
        {
            AvailableNetworkAdapters = _networkAdapterSession.AvailableAdapters;
            SelectedNetworkAdapter = _networkAdapterSession.SelectedAdapter;
        }
        finally
        {
            _isLoadingNetworkAdapters = false;
        }
    }

    partial void OnSelectedNetworkAdapterChanged(NetworkAdapterInfo? value)
    {
        if (_isLoadingNetworkAdapters || value is null)
        {
            return;
        }

        _networkAdapterSession.SelectAdapter(value);
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
}
