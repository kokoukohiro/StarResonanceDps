using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Windows;
using System.Windows.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using StarResonanceDps.App.Config;
using StarResonanceDps.App.Localization;
using StarResonanceDps.App.Services;
using StarResonanceDps.Core.CombatRuntime.DataTypes;
using StarResonanceDps.Core.Models;
using StarResonanceDps.Core.Services;

namespace StarResonanceDps.App.ViewModels;

public sealed partial class SettingsViewModel : ViewModelBase, IDisposable
{
    private static readonly bool IsInDesignMode =
        DesignerProperties.GetIsInDesignMode(new DependencyObject());

    private readonly ConfigManager _configManager = ConfigManager.Instance;
    private readonly NetworkAdapterSession _networkAdapterSession = NetworkAdapterSession.Instance;
    private SettingsConfig _lastSavedSettings;
    private string _lastSavedNetworkAdapterDeviceName = string.Empty;
    private EGameCapturePreference _lastSavedGameCapturePreference = EGameCapturePreference.Auto;
    private string _lastSavedGameCaptureCustomExeName = string.Empty;
    private bool _isLoadingSettings;
    private bool _isLoadingNetworkAdapters;
    private bool _isLoadingCaptureSettings;

    [ObservableProperty]
    private IReadOnlyList<NetworkAdapterOption> _availableNetworkAdapters = [];

    [ObservableProperty]
    private NetworkAdapterOption? _selectedNetworkAdapter;

    [ObservableProperty]
    private GameCapturePreferenceOption? _selectedGameCapturePreference;

    [ObservableProperty]
    private string _gameCaptureCustomExeName = string.Empty;

    [ObservableProperty]
    private int _languageIndex;

    [ObservableProperty]
    private int _numberDisplayFormatIndex;

    [ObservableProperty]
    private int _playerNameDisplayModeIndex;

    public SettingsViewModel()
    {
        LocalizationManager.Instance.CultureChanged += LocalizationManager_CultureChanged;
        GameCapturePreferences = new ReadOnlyObservableCollection<GameCapturePreferenceOption>(
            new ObservableCollection<GameCapturePreferenceOption>(CreateGameCapturePreferences()));
        WindowColors = new ColorPaletteViewModel(AppConfigDefaults.CreateDefaultWindowColors(), AppConfigDefaults.MaxPaletteColorCount);
        WindowColors.PaletteChanged += WindowColors_PaletteChanged;

        var settings = _configManager.GetSettingsSnapshot();
        _lastSavedSettings = settings.Clone();
        LoadFromSettings(settings, applyLanguage: false, applyPreview: false);
        LoadCaptureSettings();
    }

    public ColorPaletteViewModel WindowColors { get; }

    public ReadOnlyObservableCollection<GameCapturePreferenceOption> GameCapturePreferences { get; }

    public bool IsCustomGameCapturePreference => SelectedGameCapturePreference?.Preference == EGameCapturePreference.Custom;

    public bool HasUnsavedChanges => !SettingsEquals(CreateSettings(), _lastSavedSettings) || !CaptureSettingsEqualsSaved();

    public void Dispose()
    {
        _configManager.ClearSettingsPreview();
        WindowColors.PaletteChanged -= WindowColors_PaletteChanged;
        LocalizationManager.Instance.CultureChanged -= LocalizationManager_CultureChanged;
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
        SaveCaptureSettings();
        ApplyCurrentGlobalTheme();
        OnPropertyChanged(nameof(HasUnsavedChanges));
    }

    public void ResetToDefaults()
    {
        LoadFromSettings(AppConfigDefaults.CreateSettings(), applyLanguage: true, applyPreview: true);
        LoadCaptureSettingsFromValues(string.Empty, EGameCapturePreference.Auto, string.Empty);
        ApplyCaptureSettingsPreview();
        ApplyCurrentGlobalTheme();
        OnPropertyChanged(nameof(HasUnsavedChanges));
    }

    public void RestoreSavedSettingsPreview()
    {
        LocalizationManager.Instance.ApplyLanguageIndex(_lastSavedSettings.LanguageIndex);
        _configManager.ClearSettingsPreview();
        ThemeManager.Instance.ApplyGlobalTheme(_lastSavedSettings);
        LoadCaptureSettingsFromValues(
            _lastSavedNetworkAdapterDeviceName,
            _lastSavedGameCapturePreference,
            _lastSavedGameCaptureCustomExeName);
        ApplyCaptureSettingsPreview();
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

    private void LoadCaptureSettings()
    {
        if (IsInDesignMode)
        {
            return;
        }

        _isLoadingNetworkAdapters = true;
        _isLoadingCaptureSettings = true;
        try
        {
            _networkAdapterSession.Initialize();
            AvailableNetworkAdapters = CreateNetworkAdapterOptions(_networkAdapterSession.AvailableAdapters);
            _lastSavedNetworkAdapterDeviceName = NormalizeAdapterDeviceName(Settings.Instance.NetCaptureDeviceName);
            _lastSavedGameCapturePreference = Settings.Instance.GameCapturePreference;
            _lastSavedGameCaptureCustomExeName = NormalizeCustomExeName(Settings.Instance.GameCaptureCustomExeName);
            LoadCaptureSettingsFromValues(
                _lastSavedNetworkAdapterDeviceName,
                _lastSavedGameCapturePreference,
                _lastSavedGameCaptureCustomExeName);
        }
        finally
        {
            _isLoadingCaptureSettings = false;
            _isLoadingNetworkAdapters = false;
        }
    }

    private void LoadCaptureSettingsFromValues(
        string networkAdapterDeviceName,
        EGameCapturePreference gameCapturePreference,
        string gameCaptureCustomExeName)
    {
        var wasLoadingCaptureSettings = _isLoadingCaptureSettings;
        _isLoadingCaptureSettings = true;
        try
        {
            SelectedNetworkAdapter = FindNetworkAdapterOption(networkAdapterDeviceName) ?? FindAutomaticNetworkAdapterOption();
            SelectedGameCapturePreference = GameCapturePreferences.FirstOrDefault(option =>
                    option.Preference == gameCapturePreference)
                ?? GameCapturePreferences.FirstOrDefault(option => option.Preference == EGameCapturePreference.Auto)
                ?? GameCapturePreferences.FirstOrDefault();
            GameCaptureCustomExeName = NormalizeCustomExeName(gameCaptureCustomExeName);
        }
        finally
        {
            _isLoadingCaptureSettings = wasLoadingCaptureSettings;
        }

        OnPropertyChanged(nameof(IsCustomGameCapturePreference));
        OnPropertyChanged(nameof(HasUnsavedChanges));
    }

    private void SaveCaptureSettings()
    {
        if (IsInDesignMode)
        {
            return;
        }

        var networkAdapterDeviceName = NormalizeAdapterDeviceName(SelectedNetworkAdapter?.DeviceName);
        var gameCapturePreference = SelectedGameCapturePreference?.Preference ?? EGameCapturePreference.Auto;
        var gameCaptureCustomExeName = NormalizeCustomExeName(GameCaptureCustomExeName);

        _networkAdapterSession.ApplyCaptureSettings(
            networkAdapterDeviceName,
            gameCapturePreference,
            gameCaptureCustomExeName);

        networkAdapterDeviceName = NormalizeAdapterDeviceName(Settings.Instance.NetCaptureDeviceName);
        gameCapturePreference = Settings.Instance.GameCapturePreference;
        gameCaptureCustomExeName = NormalizeCustomExeName(Settings.Instance.GameCaptureCustomExeName);

        _lastSavedNetworkAdapterDeviceName = networkAdapterDeviceName;
        _lastSavedGameCapturePreference = gameCapturePreference;
        _lastSavedGameCaptureCustomExeName = gameCaptureCustomExeName;
        LoadCaptureSettingsFromValues(networkAdapterDeviceName, gameCapturePreference, gameCaptureCustomExeName);
    }

    private void ApplyCaptureSettingsPreview()
    {
        if (IsInDesignMode || _isLoadingNetworkAdapters || _isLoadingCaptureSettings)
        {
            return;
        }

        _networkAdapterSession.PreviewCaptureSettings(
            NormalizeAdapterDeviceName(SelectedNetworkAdapter?.DeviceName),
            SelectedGameCapturePreference?.Preference ?? EGameCapturePreference.Auto,
            NormalizeCustomExeName(GameCaptureCustomExeName));
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

    private void LocalizationManager_CultureChanged(object? sender, EventArgs e)
    {
        foreach (var option in AvailableNetworkAdapters)
        {
            option.RefreshDisplayName();
        }

        foreach (var option in GameCapturePreferences)
        {
            option.RefreshDisplayName();
        }
    }

    private void ApplySettingsPreview()
    {
        _configManager.SetSettingsPreview(CreateSettings());
    }

    private void ApplyCurrentGlobalTheme()
    {
        ThemeManager.Instance.ApplyGlobalTheme(CreateSettings());
    }

    private bool CaptureSettingsEqualsSaved()
    {
        if (IsInDesignMode)
        {
            return true;
        }

        return string.Equals(NormalizeAdapterDeviceName(SelectedNetworkAdapter?.DeviceName), _lastSavedNetworkAdapterDeviceName, StringComparison.Ordinal)
            && (SelectedGameCapturePreference?.Preference ?? EGameCapturePreference.Auto) == _lastSavedGameCapturePreference
            && string.Equals(NormalizeCustomExeName(GameCaptureCustomExeName), _lastSavedGameCaptureCustomExeName, StringComparison.Ordinal);
    }

    private NetworkAdapterOption? FindAutomaticNetworkAdapterOption()
    {
        return AvailableNetworkAdapters.FirstOrDefault(option => option.IsAutomatic);
    }

    private NetworkAdapterOption? FindNetworkAdapterOption(string? deviceName)
    {
        var normalizedDeviceName = NormalizeAdapterDeviceName(deviceName);
        if (Settings.IsAutomaticNetCaptureDeviceName(normalizedDeviceName))
        {
            return FindAutomaticNetworkAdapterOption();
        }

        return AvailableNetworkAdapters.FirstOrDefault(option =>
            string.Equals(option.DeviceName, normalizedDeviceName, StringComparison.Ordinal));
    }

    private static IReadOnlyList<NetworkAdapterOption> CreateNetworkAdapterOptions(IReadOnlyList<NetworkAdapterInfo> adapters)
    {
        var options = new List<NetworkAdapterOption>
        {
            NetworkAdapterOption.CreateAutomatic()
        };

        foreach (var adapter in adapters)
        {
            options.Add(NetworkAdapterOption.Create(adapter));
        }

        return options;
    }

    private static IReadOnlyList<GameCapturePreferenceOption> CreateGameCapturePreferences()
    {
        return
        [
            CreateGameCapturePreferenceOption(EGameCapturePreference.Auto),
            CreateGameCapturePreferenceOption(EGameCapturePreference.Standalone),
            CreateGameCapturePreferenceOption(EGameCapturePreference.Steam),
            CreateGameCapturePreferenceOption(EGameCapturePreference.Epic),
            CreateGameCapturePreferenceOption(EGameCapturePreference.HaoPlaySea),
            CreateGameCapturePreferenceOption(EGameCapturePreference.XDG),
            CreateGameCapturePreferenceOption(EGameCapturePreference.HaoPlaySeaSteam),
            CreateGameCapturePreferenceOption(EGameCapturePreference.XDGSteam),
            CreateGameCapturePreferenceOption(EGameCapturePreference.WeGame),
            CreateGameCapturePreferenceOption(EGameCapturePreference.Custom)
        ];
    }

    private static GameCapturePreferenceOption CreateGameCapturePreferenceOption(EGameCapturePreference preference)
    {
        return new GameCapturePreferenceOption(preference);
    }

    private static string NormalizeAdapterDeviceName(string? deviceName)
    {
        return Settings.IsAutomaticNetCaptureDeviceName(deviceName)
            ? Settings.AutomaticNetCaptureDeviceName
            : deviceName!.Trim();
    }

    private static string NormalizeCustomExeName(string? customExeName)
    {
        return Path.GetFileNameWithoutExtension(customExeName ?? string.Empty);
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

    partial void OnSelectedNetworkAdapterChanged(NetworkAdapterOption? value)
    {
        if (!_isLoadingNetworkAdapters && !_isLoadingCaptureSettings)
        {
            ApplyCaptureSettingsPreview();
        }

        OnPropertyChanged(nameof(HasUnsavedChanges));
    }

    partial void OnSelectedGameCapturePreferenceChanged(GameCapturePreferenceOption? value)
    {
        OnPropertyChanged(nameof(IsCustomGameCapturePreference));

        if (!_isLoadingCaptureSettings)
        {
            ApplyCaptureSettingsPreview();
        }

        OnPropertyChanged(nameof(HasUnsavedChanges));
    }

    partial void OnGameCaptureCustomExeNameChanged(string value)
    {
        if (!_isLoadingCaptureSettings)
        {
            ApplyCaptureSettingsPreview();
        }

        OnPropertyChanged(nameof(HasUnsavedChanges));
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
