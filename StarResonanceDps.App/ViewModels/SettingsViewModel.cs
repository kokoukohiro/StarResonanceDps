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
using StarResonanceDps.Core.CombatRuntime;
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

    [ObservableProperty]
    private int _internalIdDisplayModeIndex;

    [ObservableProperty]
    private int _widgetWindowTopmostModeIndex = AppConfigDefaults.AlwaysWidgetWindowTopmostModeIndex;

    // --- 集計設定 ---

    [ObservableProperty]
    private bool _splitEncountersOnNewPhases = true;

    [ObservableProperty]
    private bool _keepPastEncounterInMeterUntilNextDamage;

    [ObservableProperty]
    private bool _clearHistorySelectionOnNextEvent = true;

    /// <summary>戦闘履歴を残す最大の件数。<b>0 は無限。</b></summary>
    [ObservableProperty]
    private int _databaseMaxEncounterCount;

    /// <summary>
    /// 保持期間の選択肢。**値は日数そのもので、0 が無期限。**
    /// 選べる値をここだけで決めているので、増やすならこの配列に足す
    /// (<c>AppConfigDefaults.Normalize</c> の上限とずれないようにすること)。
    /// </summary>
    private static readonly int[] MaxEncounterCountChoices = [20, 50, 99, 0];

    private readonly ObservableCollection<RetentionPolicyOption> _retentionPolicyOptions = [];

    public SettingsViewModel()
    {
        LocalizationManager.Instance.CultureChanged += LocalizationManager_CultureChanged;
        RetentionPolicyOptions = new ReadOnlyObservableCollection<RetentionPolicyOption>(_retentionPolicyOptions);
        RebuildRetentionPolicyOptions();
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
            InternalIdDisplayModeIndex = InternalIdDisplayModeIndex,
            WidgetWindowTopmostModeIndex = WidgetWindowTopmostModeIndex,
            WindowColorIndex = WindowColors.SelectedIndex,
            WindowColors = [.. WindowColors.GetHexColors()],
            SplitEncountersOnNewPhases = SplitEncountersOnNewPhases,
            KeepPastEncounterInMeterUntilNextDamage = KeepPastEncounterInMeterUntilNextDamage,
            ClearHistorySelectionOnNextEvent = ClearHistorySelectionOnNextEvent,
            DatabaseMaxEncounterCount = DatabaseMaxEncounterCount,

            // キャプチャ3項目はこの画面では SettingsConfig 経由で編集しない
            // (NetworkAdapterSession が持ち、保存も別経路)。ただしここで落とすと
            // AppConfig.Settings 側が既定値に戻ってしまうので、現在値を持ち回す。
            NetCaptureDeviceName = CombatRuntimeSettings.NetCaptureDeviceName,
            GameCapturePreference = CombatRuntimeSettings.GameCapturePreference,
            GameCaptureCustomExeName = CombatRuntimeSettings.GameCaptureCustomExeName
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
            InternalIdDisplayModeIndex = settings.InternalIdDisplayModeIndex;
            WidgetWindowTopmostModeIndex = settings.WidgetWindowTopmostModeIndex;
            WindowColors.Load(settings.WindowColors, settings.WindowColorIndex);
            SplitEncountersOnNewPhases = settings.SplitEncountersOnNewPhases;
            KeepPastEncounterInMeterUntilNextDamage = settings.KeepPastEncounterInMeterUntilNextDamage;
            ClearHistorySelectionOnNextEvent = settings.ClearHistorySelectionOnNextEvent;
            DatabaseMaxEncounterCount = settings.DatabaseMaxEncounterCount;
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
            _lastSavedNetworkAdapterDeviceName = NormalizeAdapterDeviceName(CombatRuntimeSettings.NetCaptureDeviceName);
            _lastSavedGameCapturePreference = CombatRuntimeSettings.GameCapturePreference;
            _lastSavedGameCaptureCustomExeName = NormalizeCustomExeName(CombatRuntimeSettings.GameCaptureCustomExeName);
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

        networkAdapterDeviceName = NormalizeAdapterDeviceName(CombatRuntimeSettings.NetCaptureDeviceName);
        gameCapturePreference = CombatRuntimeSettings.GameCapturePreference;
        gameCaptureCustomExeName = NormalizeCustomExeName(CombatRuntimeSettings.GameCaptureCustomExeName);

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

        // ON / OFF は言語で変わるので、開いたまま切り替えられても追従させる
        // (言語の選択肢はこの画面の中にあるので、開いたままの切り替えが普通に起きる)。
        OnPropertyChanged(nameof(SplitEncountersOnNewPhasesStateText));
        OnPropertyChanged(nameof(KeepPastEncounterInMeterUntilNextDamageStateText));
        OnPropertyChanged(nameof(ClearHistorySelectionOnNextEventStateText));

        RebuildRetentionPolicyOptions();
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
        if (CombatRuntimeSettings.IsAutomaticNetCaptureDeviceName(normalizedDeviceName))
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
        return CombatRuntimeSettings.IsAutomaticNetCaptureDeviceName(deviceName)
            ? CombatRuntimeSettings.AutomaticNetCaptureDeviceName
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
            && left.InternalIdDisplayModeIndex == right.InternalIdDisplayModeIndex
            && left.WidgetWindowTopmostModeIndex == right.WidgetWindowTopmostModeIndex
            && left.WindowColorIndex == right.WindowColorIndex
            && left.SplitEncountersOnNewPhases == right.SplitEncountersOnNewPhases
            && left.KeepPastEncounterInMeterUntilNextDamage == right.KeepPastEncounterInMeterUntilNextDamage
            && left.ClearHistorySelectionOnNextEvent == right.ClearHistorySelectionOnNextEvent
            && left.DatabaseMaxEncounterCount == right.DatabaseMaxEncounterCount
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

    // 集計設定はプレビューを持たない。戦闘の区切り方やDBの掃除は「下見」できる類ではなく、
    // 保存したときにだけ効かせる。未保存の印だけ更新する。
    partial void OnSplitEncountersOnNewPhasesChanged(bool value)
    {
        OnPropertyChanged(nameof(SplitEncountersOnNewPhasesStateText));
        OnPropertyChanged(nameof(HasUnsavedChanges));
    }

    partial void OnKeepPastEncounterInMeterUntilNextDamageChanged(bool value)
    {
        OnPropertyChanged(nameof(KeepPastEncounterInMeterUntilNextDamageStateText));
        OnPropertyChanged(nameof(HasUnsavedChanges));
    }

    partial void OnClearHistorySelectionOnNextEventChanged(bool value)
    {
        OnPropertyChanged(nameof(ClearHistorySelectionOnNextEventStateText));
        OnPropertyChanged(nameof(HasUnsavedChanges));
    }

    /// <summary>スイッチの右に出す ON / OFF。ウィジェット設定と同じ形。</summary>
    public string SplitEncountersOnNewPhasesStateText => GetSwitchStateText(SplitEncountersOnNewPhases);

    public string KeepPastEncounterInMeterUntilNextDamageStateText =>
        GetSwitchStateText(KeepPastEncounterInMeterUntilNextDamage);

    public string ClearHistorySelectionOnNextEventStateText =>
        GetSwitchStateText(ClearHistorySelectionOnNextEvent);

    private static string GetSwitchStateText(bool isOn)
    {
        return LocalizationManager.Instance.GetString(isOn ? "Settings_Switch_On" : "Settings_Switch_Off");
    }

    partial void OnDatabaseMaxEncounterCountChanged(int value)
    {
        OnPropertyChanged(nameof(HasUnsavedChanges));
    }

    public ReadOnlyObservableCollection<RetentionPolicyOption> RetentionPolicyOptions { get; }

    /// <summary>
    /// 選択肢を作り直す。**言語切替のたびに呼ぶ** — 文言が言語で変わるので、
    /// 作りっぱなしだと開いたまま切り替えたときに古い言語のまま残る。
    /// </summary>
    private void RebuildRetentionPolicyOptions()
    {
        // 作り直すと SelectedValue の参照先が消えるので、選択を戻せるよう控えておく。
        // 通知を出さない ＝ 空欄のままになる。控えた値を押し直して選び直させる。
        var selected = DatabaseMaxEncounterCount;

        _retentionPolicyOptions.Clear();
        foreach (var count in MaxEncounterCountChoices)
        {
            _retentionPolicyOptions.Add(new RetentionPolicyOption(count, FormatRetentionPolicy(count)));
        }

        DatabaseMaxEncounterCount = selected;
        OnPropertyChanged(nameof(DatabaseMaxEncounterCount));
    }

    /// <summary><b>0 は件数ではなく「無限」</b>なので数字を出さない。</summary>
    private static string FormatRetentionPolicy(int count)
    {
        return count <= 0
            ? LocalizationManager.Instance.GetString("Settings_Aggregation_MaxEncounterCount_Unlimited")
            : string.Format(
                LocalizationManager.Instance.GetString("Settings_Aggregation_MaxEncounterCount_Value"),
                count);
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

    partial void OnInternalIdDisplayModeIndexChanged(int value)
    {
        if (!_isLoadingSettings)
        {
            ApplySettingsPreview();
        }

        OnPropertyChanged(nameof(HasUnsavedChanges));
    }

    partial void OnWidgetWindowTopmostModeIndexChanged(int value)
    {
        if (!_isLoadingSettings)
        {
            ApplySettingsPreview();
        }

        OnPropertyChanged(nameof(HasUnsavedChanges));
    }
}
