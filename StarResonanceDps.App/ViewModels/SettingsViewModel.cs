using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Windows;
using System.Windows.Input;
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

    // --- ホットキー ---

    private readonly GlobalHotkeyService _hotkeyService = GlobalHotkeyService.Instance;

    /// <summary>キーを受け付けている行。受付中はホットキーを全部外している(<see cref="GlobalHotkeyService.Suspend"/>)。</summary>
    private HotkeyItemViewModel? _capturingHotkeyItem;

    // --- 通知 ---

    [ObservableProperty]
    private int _notificationMethodIndex = AppConfigDefaults.NotificationMethodNoneIndex;

    /// <summary>通知音量。スライダーの値なので double で持ち、保存のときに整数へ丸める。</summary>
    [ObservableProperty]
    private double _notificationVolume = AppConfigDefaults.NotificationVolumeDefault;

    [ObservableProperty]
    private int _speechVoiceIndex = AppConfigDefaults.SpeechVoiceWindowsIndex;

    // 選んである VOICEVOX の話者(保存する値)。選択肢を作り直すときに選択リストが null を書き戻しても、ここは変えない。
    private string _voicevoxSpeakerUuid = string.Empty;
    private string _voicevoxSpeakerName = string.Empty;
    private int? _voicevoxStyleId;
    private string _voicevoxStyleName = string.Empty;

    private readonly ObservableCollection<VoicevoxSpeakerOption> _voicevoxSpeakerOptions = [];

    private readonly ObservableCollection<VoicevoxStyleOption> _voicevoxStyleOptions = [];

    /// <summary>最後にエンジンから取れた話者の一覧。まだ問い合わせていなければ null(言語の切り替えでの作り直しに使う)。</summary>
    private IReadOnlyList<VoicevoxStyle>? _fetchedVoicevoxStyles;

    private bool _isRebuildingVoicevoxOptions;

    /// <summary>スタイルの行が要るか(<see cref="RebuildVoicevoxStyleOptions"/> が決める)。</summary>
    private bool _isVoicevoxStyleRowNeeded;

    [ObservableProperty]
    private VoicevoxSpeakerOption? _selectedVoicevoxSpeaker;

    [ObservableProperty]
    private VoicevoxStyleOption? _selectedVoicevoxStyle;

    /// <summary>
    /// 読み上げの声を確かめる時機(読み上げで声を選んだ・Windows の音声のまま言語を変えた)。画面が確かめてメッセージを出す。
    /// 読み込み(開いたとき・既定に戻す)では上げない。
    /// </summary>
    public event EventHandler? SpeechVoiceCheckRequested;

    public SettingsViewModel()
    {
        LocalizationManager.Instance.CultureChanged += LocalizationManager_CultureChanged;
        var hotkeyActions = System.Enum.GetValues<HotkeyAction>();
        HotkeyItems = new ReadOnlyObservableCollection<HotkeyItemViewModel>(new ObservableCollection<HotkeyItemViewModel>(
            hotkeyActions.Select((action, index) => new HotkeyItemViewModel(action, isLast: index == hotkeyActions.Length - 1))));
        RetentionPolicyOptions = new ReadOnlyObservableCollection<RetentionPolicyOption>(_retentionPolicyOptions);
        RebuildRetentionPolicyOptions();
        VoicevoxSpeakerOptions = new ReadOnlyObservableCollection<VoicevoxSpeakerOption>(_voicevoxSpeakerOptions);
        VoicevoxStyleOptions = new ReadOnlyObservableCollection<VoicevoxStyleOption>(_voicevoxStyleOptions);
        GameCapturePreferences = new ReadOnlyObservableCollection<GameCapturePreferenceOption>(
            new ObservableCollection<GameCapturePreferenceOption>(CreateGameCapturePreferences()));
        WindowColors = new ColorPaletteViewModel(AppConfigDefaults.CreateDefaultWindowColors(), AppConfigDefaults.MaxPaletteColorCount);
        WindowColors.PaletteChanged += WindowColors_PaletteChanged;

        var settings = _configManager.GetSettingsSnapshot();
        _lastSavedSettings = settings.Clone();
        LoadFromSettings(settings, applyLanguage: false, applyPreview: false);
        LoadCaptureSettings();
        _configManager.WidgetWindowTopmostModeSaved += ConfigManager_WidgetWindowTopmostModeSaved;
    }

    public ColorPaletteViewModel WindowColors { get; }

    public ReadOnlyObservableCollection<GameCapturePreferenceOption> GameCapturePreferences { get; }

    public bool IsCustomGameCapturePreference => SelectedGameCapturePreference?.Preference == EGameCapturePreference.Custom;

    public bool HasUnsavedChanges => !SettingsEquals(CreateSettings(), _lastSavedSettings) || !CaptureSettingsEqualsSaved();

    /// <summary>ホットキーの行。並びは <see cref="HotkeyAction"/> の並び。</summary>
    public ReadOnlyObservableCollection<HotkeyItemViewModel> HotkeyItems { get; }

    public bool IsCapturingHotkey => _capturingHotkeyItem is not null;

    /// <summary>通知方式が Windows の通知か。通知方式の行に応答不可の注意書きを出す。</summary>
    public bool IsWindowsNotificationSelected => NotificationMethodIndex == AppConfigDefaults.NotificationMethodWindowsIndex;

    /// <summary>通知方式が読み上げか。読み上げ方式の行を出す。</summary>
    public bool IsSpeechSelected => NotificationMethodIndex == AppConfigDefaults.NotificationMethodSpeechIndex;

    /// <summary>読み上げを VOICEVOX でするか。VOICEVOX の行(話者・スタイル)を出す。</summary>
    public bool IsVoicevoxSelected => IsSpeechSelected && SpeechVoiceIndex == AppConfigDefaults.SpeechVoiceVoicevoxIndex;

    public ReadOnlyObservableCollection<VoicevoxSpeakerOption> VoicevoxSpeakerOptions { get; }

    /// <summary>選んでいる話者のスタイル。</summary>
    public ReadOnlyObservableCollection<VoicevoxStyleOption> VoicevoxStyleOptions { get; }

    /// <summary>
    /// スタイルの行を出すか。エンジンの一覧で、選んでいる話者のスタイルが2つ以上あるときだけ出す。
    /// 一覧がまだ取れていないときはスタイルの数が分からないので、保存してあるスタイルを出す。
    /// </summary>
    public bool ShowsVoicevoxStyle => IsVoicevoxSelected && _isVoicevoxStyleRowNeeded;

    /// <summary>VOICEVOX の話者を選んであるか。選んでいなければ利用規約のリンクを出さない。</summary>
    public bool HasVoicevoxSpeaker => _voicevoxSpeakerUuid.Length > 0 && _voicevoxStyleId is not null;

    public string VoicevoxSpeakerUuid => _voicevoxSpeakerUuid;

    public string VoicevoxSpeakerName => _voicevoxSpeakerName;

    /// <summary>選んである話者の利用規約のリンクの文字。選んでいなければ空。</summary>
    public string VoicevoxPolicyLinkText => HasVoicevoxSpeaker
        ? LocalizationManager.Instance.Format("Voicevox_Policy_Title", _voicevoxSpeakerName)
        : string.Empty;

    /// <summary>
    /// エンジンから話者の一覧を取り直して選択肢を作り直す。失敗したら選択肢は今のままにし、結果の種類を返す
    /// (メッセージを出すかは画面が決める。出すのは読み上げで VOICEVOX を選んだときだけ)。
    /// </summary>
    public async Task<VoicevoxResultKind> RefreshVoicevoxSpeakersAsync()
    {
        var result = await VoicevoxClient.GetStylesAsync();
        if (result.Kind == VoicevoxResultKind.Success)
        {
            _fetchedVoicevoxStyles = result.Value;
            RebuildVoicevoxOptions();
        }

        return result.Kind;
    }

    public void Dispose()
    {
        // 受付中に閉じたら取り消して、外していたホットキーを戻す(戻せなかったものはサービスがログに書く)。
        EndHotkeyCapture();
        _configManager.WidgetWindowTopmostModeSaved -= ConfigManager_WidgetWindowTopmostModeSaved;
        _configManager.ClearSettingsPreview();
        _hotkeyService.PreviewGameProcessNames(null);

        // プレビューで Windows の通知を出していると、アプリの名前の登録が書かれている。
        // 保存してある通知方法が Windows の通知でなければ消す(取り消し・値を戻して閉じた場合も)。
        if (_lastSavedSettings.NotificationMethodIndex != AppConfigDefaults.NotificationMethodWindowsIndex)
        {
            NotificationService.RemoveWindowsNotificationRegistration();
        }
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

    /// <summary>保存してホットキーを登録し直す。登録できなかったホットキーを返す(画面がメッセージで知らせる)。</summary>
    public IReadOnlyList<HotkeyRegistrationFailure> SaveSettings()
    {
        var settings = CreateSettings();
        _configManager.SaveSettings(settings);
        _lastSavedSettings = settings.Clone();

        // Windows の通知のためのアプリの名前の登録は、Windows の通知を使わなくなったら消す(使うときに通知の処理が書く)。
        if (settings.NotificationMethodIndex != AppConfigDefaults.NotificationMethodWindowsIndex)
        {
            NotificationService.RemoveWindowsNotificationRegistration();
        }

        SaveCaptureSettings();
        ApplyCurrentGlobalTheme();
        OnPropertyChanged(nameof(HasUnsavedChanges));
        return _hotkeyService.Apply(settings.Hotkeys);
    }

    /// <summary>
    /// その行でキーの受付を始める。ほかの行が受付中ならそちらは取り消す。
    /// 受付の間はホットキーを全部外す(外さないと押したキーがホットキーとして動いて欄に届かない)。
    /// </summary>
    public void BeginHotkeyCapture(HotkeyItemViewModel item)
    {
        if (ReferenceEquals(_capturingHotkeyItem, item))
        {
            return;
        }

        if (_capturingHotkeyItem is not null)
        {
            _capturingHotkeyItem.IsCapturing = false;
        }
        else
        {
            _hotkeyService.Suspend();
        }

        _capturingHotkeyItem = item;
        item.IsCapturing = true;
        OnPropertyChanged(nameof(IsCapturingHotkey));
    }

    /// <summary>
    /// 受付中の行に、押されたキーを割り当てる。
    ///
    /// <para>
    /// その場で試しに登録し、できなければ割り当てずに元のキーのまま受付を終える。
    /// できたら、同じキーを持つほかの行は空欄にする。受付を終えると、画面で編集中の割り当てで登録し直す
    /// (プレビュー。保存せずに閉じたら <see cref="RestoreSavedSettingsPreview"/> が保存してある割り当てへ戻す)。
    /// 返すのは登録できなかったもの(試しの登録と、登録し直したときの両方)。
    /// </para>
    /// </summary>
    public IReadOnlyList<HotkeyRegistrationFailure> CompleteHotkeyCapture(Key key, ModifierKeys modifiers)
    {
        if (_capturingHotkeyItem is not { } item)
        {
            return [];
        }

        var failures = new List<HotkeyRegistrationFailure>();
        var binding = HotkeyBindingConfig.Create(key, modifiers);

        if (!binding.SameAs(item.Binding))
        {
            if (_hotkeyService.TestRegistration(binding) is { } error)
            {
                failures.Add(new HotkeyRegistrationFailure(item.Action, binding, error));
            }
            else
            {
                foreach (var other in HotkeyItems)
                {
                    if (!ReferenceEquals(other, item) && other.Binding.SameAs(binding))
                    {
                        other.Binding = new HotkeyBindingConfig();
                    }
                }

                item.Binding = binding;

                // 受付中(一時停止中)なので控えるだけ。下の受付の終了で登録される。
                _hotkeyService.Apply(CreateHotkeySettings());
            }
        }

        failures.AddRange(EndHotkeyCapture());
        OnPropertyChanged(nameof(HasUnsavedChanges));
        return failures;
    }

    /// <summary>受付を取り消す(欄の外をクリックした)。受付の前の割り当てで登録し直し、登録できなかったものを返す。</summary>
    public IReadOnlyList<HotkeyRegistrationFailure> CancelHotkeyCapture()
    {
        return EndHotkeyCapture();
    }

    private IReadOnlyList<HotkeyRegistrationFailure> EndHotkeyCapture()
    {
        if (_capturingHotkeyItem is null)
        {
            return [];
        }

        _capturingHotkeyItem.IsCapturing = false;
        _capturingHotkeyItem = null;
        OnPropertyChanged(nameof(IsCapturingHotkey));
        return _hotkeyService.Resume();
    }

    /// <summary>既定に戻してプレビューする。ホットキーも既定のキーで登録し直し、登録できなかったものを返す。</summary>
    public IReadOnlyList<HotkeyRegistrationFailure> ResetToDefaults()
    {
        LoadFromSettings(AppConfigDefaults.CreateSettings(), applyLanguage: true, applyPreview: true);
        LoadCaptureSettingsFromValues(string.Empty, EGameCapturePreference.Auto, string.Empty);
        ApplyCaptureSettingsPreview();
        ApplyCurrentGlobalTheme();
        OnPropertyChanged(nameof(HasUnsavedChanges));
        return _hotkeyService.Apply(CreateHotkeySettings());
    }

    /// <summary>保存せずに閉じる。プレビューを保存してある値へ戻し、ホットキーも登録し直して、登録できなかったものを返す。</summary>
    public IReadOnlyList<HotkeyRegistrationFailure> RestoreSavedSettingsPreview()
    {
        LocalizationManager.Instance.ApplyLanguageIndex(_lastSavedSettings.LanguageIndex);
        _configManager.ClearSettingsPreview();
        ThemeManager.Instance.ApplyGlobalTheme(_lastSavedSettings);
        LoadCaptureSettingsFromValues(
            _lastSavedNetworkAdapterDeviceName,
            _lastSavedGameCapturePreference,
            _lastSavedGameCaptureCustomExeName);
        ApplyCaptureSettingsPreview();
        return _hotkeyService.Apply(_lastSavedSettings.Hotkeys);
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
            Hotkeys = CreateHotkeySettings(),
            NotificationMethodIndex = NotificationMethodIndex,
            NotificationVolume = (int)Math.Round(NotificationVolume, MidpointRounding.AwayFromZero),
            SpeechVoiceIndex = SpeechVoiceIndex,
            VoicevoxSpeakerUuid = _voicevoxSpeakerUuid,
            VoicevoxSpeakerName = _voicevoxSpeakerName,
            VoicevoxStyleId = _voicevoxStyleId,
            VoicevoxStyleName = _voicevoxStyleName,

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

    private HotkeySettingsConfig CreateHotkeySettings()
    {
        var hotkeys = new HotkeySettingsConfig();
        foreach (var item in HotkeyItems)
        {
            hotkeys.Set(item.Action, item.Binding.Clone());
        }

        return hotkeys;
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

            foreach (var item in HotkeyItems)
            {
                item.Binding = settings.Hotkeys.Get(item.Action).Clone();
            }

            NotificationMethodIndex = settings.NotificationMethodIndex;
            NotificationVolume = settings.NotificationVolume;
            SpeechVoiceIndex = settings.SpeechVoiceIndex;
            _voicevoxSpeakerUuid = settings.VoicevoxSpeakerUuid;
            _voicevoxSpeakerName = settings.VoicevoxSpeakerName;
            _voicevoxStyleId = settings.VoicevoxStyleId;
            _voicevoxStyleName = settings.VoicevoxStyleName;
            RebuildVoicevoxOptions();
            OnVoicevoxSpeakerChanged();
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

        var gameCapturePreference = SelectedGameCapturePreference?.Preference ?? EGameCapturePreference.Auto;
        var gameCaptureCustomExeName = NormalizeCustomExeName(GameCaptureCustomExeName);
        _networkAdapterSession.PreviewCaptureSettings(
            NormalizeAdapterDeviceName(SelectedNetworkAdapter?.DeviceName),
            gameCapturePreference,
            gameCaptureCustomExeName);

        // ホットキーの「ゲームが前面か」の判定も、プレビューのゲームにそろえる(閉じるときに Dispose で戻す)。
        _hotkeyService.PreviewGameProcessNames(
            Utils.GameCapturePreferenceToExeNames(gameCapturePreference, gameCaptureCustomExeName));
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
        RebuildVoicevoxOptions();
        OnPropertyChanged(nameof(VoicevoxPolicyLinkText));

        foreach (var item in HotkeyItems)
        {
            item.RefreshText();
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
            && left.WindowColors.SequenceEqual(right.WindowColors, StringComparer.OrdinalIgnoreCase)
            && left.Hotkeys.HasSameBindings(right.Hotkeys)
            && left.NotificationMethodIndex == right.NotificationMethodIndex
            && left.NotificationVolume == right.NotificationVolume
            && left.SpeechVoiceIndex == right.SpeechVoiceIndex
            && string.Equals(left.VoicevoxSpeakerUuid, right.VoicevoxSpeakerUuid, StringComparison.Ordinal)
            && string.Equals(left.VoicevoxSpeakerName, right.VoicevoxSpeakerName, StringComparison.Ordinal)
            && left.VoicevoxStyleId == right.VoicevoxStyleId
            && string.Equals(left.VoicevoxStyleName, right.VoicevoxStyleName, StringComparison.Ordinal);
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

            // Windows の音声は表示言語の声で読むので、言語を変えたら声を確かめ直す。
            if (IsSpeechSelected && SpeechVoiceIndex == AppConfigDefaults.SpeechVoiceWindowsIndex)
            {
                SpeechVoiceCheckRequested?.Invoke(this, EventArgs.Empty);
            }
        }

        OnPropertyChanged(nameof(HasUnsavedChanges));
    }

    // 通知の値も保存前プレビューにする(通知を出す処理はプレビューを含む値を読む)。
    partial void OnNotificationMethodIndexChanged(int value)
    {
        OnSpeechSelectionChanged();
    }

    partial void OnSpeechVoiceIndexChanged(int value)
    {
        OnSpeechSelectionChanged();
    }

    partial void OnNotificationVolumeChanged(double value)
    {
        if (!_isLoadingSettings)
        {
            ApplySettingsPreview();
        }

        OnPropertyChanged(nameof(HasUnsavedChanges));
    }

    /// <summary>通知方式か読み上げ方式が変わった。読み上げで声を使うことになったら、声を確かめる。</summary>
    private void OnSpeechSelectionChanged()
    {
        OnPropertyChanged(nameof(IsWindowsNotificationSelected));
        OnPropertyChanged(nameof(IsSpeechSelected));
        OnPropertyChanged(nameof(IsVoicevoxSelected));
        OnPropertyChanged(nameof(ShowsVoicevoxStyle));
        OnPropertyChanged(nameof(HasUnsavedChanges));

        if (_isLoadingSettings)
        {
            return;
        }

        ApplySettingsPreview();

        if (IsSpeechSelected)
        {
            SpeechVoiceCheckRequested?.Invoke(this, EventArgs.Empty);
        }
    }

    partial void OnSelectedVoicevoxSpeakerChanged(VoicevoxSpeakerOption? value)
    {
        // 作り直しの途中と、選択リストが null を書き戻したときは、保存する値を変えない。
        if (_isRebuildingVoicevoxOptions || _isLoadingSettings || value is null || value.IsMissing)
        {
            return;
        }

        // 同じ話者が選び直されただけなら、選んでいるスタイルを保つ。
        if (string.Equals(value.SpeakerUuid, _voicevoxSpeakerUuid, StringComparison.Ordinal))
        {
            return;
        }

        // 話者を変えたら、その話者の一覧の最初のスタイルにする(VOICEVOX の既定のスタイルと同じ決め方)。
        // 保存してある話者のほかの選択肢は、エンジンの一覧から作ったものだけ。
        var defaultStyle = _fetchedVoicevoxStyles!.First(
            style => string.Equals(style.SpeakerUuid, value.SpeakerUuid, StringComparison.Ordinal));
        _voicevoxSpeakerUuid = value.SpeakerUuid;
        _voicevoxSpeakerName = value.SpeakerName;
        _voicevoxStyleId = defaultStyle.StyleId;
        _voicevoxStyleName = defaultStyle.StyleName;
        RebuildVoicevoxStyleOptions();
        OnVoicevoxSpeakerChanged();
        OnPropertyChanged(nameof(HasUnsavedChanges));
        ApplySettingsPreview();
    }

    partial void OnSelectedVoicevoxStyleChanged(VoicevoxStyleOption? value)
    {
        if (_isRebuildingVoicevoxOptions || _isLoadingSettings || value is null || value.IsMissing)
        {
            return;
        }

        _voicevoxStyleId = value.StyleId;
        _voicevoxStyleName = value.StyleName;
        OnPropertyChanged(nameof(HasUnsavedChanges));
        ApplySettingsPreview();
    }

    private void OnVoicevoxSpeakerChanged()
    {
        OnPropertyChanged(nameof(HasVoicevoxSpeaker));
        OnPropertyChanged(nameof(VoicevoxPolicyLinkText));
    }

    /// <summary>
    /// 話者とスタイルの選択肢を作り直す。選んである話者は押し直す。
    /// まだ問い合わせていないときは、選んである話者だけを選択肢にする。問い合わせた一覧に無ければ「見つかりません」付きで残す。
    /// </summary>
    private void RebuildVoicevoxOptions()
    {
        _isRebuildingVoicevoxOptions = true;
        try
        {
            _voicevoxSpeakerOptions.Clear();
            VoicevoxSpeakerOption? selected = null;
            var addedSpeakers = new HashSet<string>(StringComparer.Ordinal);

            foreach (var style in _fetchedVoicevoxStyles ?? [])
            {
                if (!addedSpeakers.Add(style.SpeakerUuid))
                {
                    continue;
                }

                var option = CreateVoicevoxSpeakerOption(style.SpeakerUuid, style.SpeakerName, isMissing: false);
                _voicevoxSpeakerOptions.Add(option);
                if (HasVoicevoxSpeaker && string.Equals(option.SpeakerUuid, _voicevoxSpeakerUuid, StringComparison.Ordinal))
                {
                    selected = option;
                }
            }

            if (selected is null && HasVoicevoxSpeaker)
            {
                selected = CreateVoicevoxSpeakerOption(
                    _voicevoxSpeakerUuid,
                    _voicevoxSpeakerName,
                    isMissing: _fetchedVoicevoxStyles is not null);
                _voicevoxSpeakerOptions.Insert(0, selected);
            }

            SelectedVoicevoxSpeaker = selected;
        }
        finally
        {
            _isRebuildingVoicevoxOptions = false;
        }

        OnPropertyChanged(nameof(SelectedVoicevoxSpeaker));
        RebuildVoicevoxStyleOptions();
    }

    /// <summary>
    /// 選んでいる話者のスタイルの選択肢を作り直し、スタイルの行を出すかを決める。選んであるスタイルは押し直す。
    /// 話者が一覧にあってスタイルだけ無ければ「見つかりません」付きで残す。話者ごと無いときは話者の行が知らせるので、スタイルの行は出さない。
    /// </summary>
    private void RebuildVoicevoxStyleOptions()
    {
        _isRebuildingVoicevoxOptions = true;
        try
        {
            _voicevoxStyleOptions.Clear();
            VoicevoxStyleOption? selected = null;
            var speakerFound = false;

            if (HasVoicevoxSpeaker)
            {
                foreach (var style in _fetchedVoicevoxStyles ?? [])
                {
                    if (!string.Equals(style.SpeakerUuid, _voicevoxSpeakerUuid, StringComparison.Ordinal))
                    {
                        continue;
                    }

                    speakerFound = true;
                    var option = CreateVoicevoxStyleOption(style.StyleId, style.StyleName, isMissing: false);
                    _voicevoxStyleOptions.Add(option);
                    if (option.StyleId == _voicevoxStyleId)
                    {
                        selected = option;
                    }
                }

                if (selected is null)
                {
                    selected = CreateVoicevoxStyleOption(_voicevoxStyleId!.Value, _voicevoxStyleName, isMissing: speakerFound);
                    _voicevoxStyleOptions.Insert(0, selected);
                }
            }

            _isVoicevoxStyleRowNeeded = HasVoicevoxSpeaker
                && (_fetchedVoicevoxStyles is null || (speakerFound && _voicevoxStyleOptions.Count > 1));
            SelectedVoicevoxStyle = selected;
        }
        finally
        {
            _isRebuildingVoicevoxOptions = false;
        }

        OnPropertyChanged(nameof(SelectedVoicevoxStyle));
        OnPropertyChanged(nameof(ShowsVoicevoxStyle));
    }

    private static VoicevoxSpeakerOption CreateVoicevoxSpeakerOption(string speakerUuid, string speakerName, bool isMissing)
    {
        var displayName = isMissing
            ? LocalizationManager.Instance.Format("Settings_Notification_VoicevoxSpeakerMissing", speakerName)
            : speakerName;
        return new VoicevoxSpeakerOption(speakerUuid, speakerName, isMissing, displayName);
    }

    private static VoicevoxStyleOption CreateVoicevoxStyleOption(int styleId, string styleName, bool isMissing)
    {
        var displayName = isMissing
            ? LocalizationManager.Instance.Format("Settings_Notification_VoicevoxSpeakerMissing", styleName)
            : styleName;
        return new VoicevoxStyleOption(styleId, styleName, isMissing, displayName);
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

    /// <summary>
    /// ホットキーが表示設定「ウィジェットウィンドウ」を切り替えて保存した。表示と保存済みの控えの両方を保存した値に合わせる
    /// (未保存の扱いにしない)。プレビューは保存の側で合わせてあるので出し直さない。
    /// </summary>
    private void ConfigManager_WidgetWindowTopmostModeSaved(object? sender, EventArgs e)
    {
        var savedIndex = _configManager.AppConfig.Settings.WidgetWindowTopmostModeIndex;
        _lastSavedSettings.WidgetWindowTopmostModeIndex = savedIndex;

        _isLoadingSettings = true;
        try
        {
            WidgetWindowTopmostModeIndex = savedIndex;
        }
        finally
        {
            _isLoadingSettings = false;
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
