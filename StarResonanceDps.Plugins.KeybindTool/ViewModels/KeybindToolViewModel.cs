using System.Collections.ObjectModel;
using System.IO;
using System.Text.Json;
using System.Windows;
using Microsoft.Win32;
using StarResonanceDps.PluginSdk;
using StarResonanceDps.Plugins.KeybindTool.Models;
using StarResonanceDps.Plugins.KeybindTool.Services;

namespace StarResonanceDps.Plugins.KeybindTool.ViewModels;

internal sealed class KeybindToolViewModel : ObservableObject
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true
    };

    private readonly IPluginContext _context;
    private readonly BpsrKeybindSaveService _saveService = new();
    private readonly string _layoutFilePath;
    private readonly Dictionary<string, ControllerActionRowViewModel> _controllerRowsByName;
    private readonly Dictionary<string, KeyMouseActionRowViewModel> _keyMouseRowsByName;
    private readonly IReadOnlyList<ControllerActionRowViewModel> _allControllerRows;
    private readonly IReadOnlyList<KeyMouseActionRowViewModel> _allKeyMouseRows;

    private KeybindSaveSession? _session;
    private DetectedSaveFile? _selectedDetectedSave;
    private string _selectedSaveFilePath = string.Empty;
    private string _statusText = "ファイル未選択";
    private string _selectedControllerType = KeybindCatalog.DefaultControllerType;
    private HelperBindingOption? _selectedHelper1;
    private HelperBindingOption? _selectedHelper2;
    private PresetOption? _selectedPreset;
    private bool _isControllerMode = true;
    private bool _controllerPhotoModeIndependent;
    private bool _keyMousePhotoModeIndependent;
    private bool _keyMouseFishingModeIndependent;
    private bool _isPresetSupported;
    private bool _isSynchronizing;
    private bool _isUpdatingDetectedSelection;
    private bool _canSave;

    public KeybindToolViewModel(IPluginContext context)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
        _layoutFilePath = Path.Combine(_context.PluginDataDirectory, KeybindCatalog.ButtonLayoutFileName);

        foreach (var controllerType in KeybindCatalog.ControllerTypes)
        {
            ControllerTypes.Add(controllerType);
        }

        ControllerMainActions = CreateControllerRows(KeybindModeGroup.Main);
        ControllerPhotoActions = CreateControllerRows(KeybindModeGroup.Photo);
        ControllerFishingActions = CreateControllerRows(KeybindModeGroup.Fishing);
        KeyMouseMainActions = CreateKeyMouseRows(KeybindModeGroup.Main);
        KeyMousePhotoActions = CreateKeyMouseRows(KeybindModeGroup.Photo);
        KeyMouseFishingActions = CreateKeyMouseRows(KeybindModeGroup.Fishing);

        _allControllerRows = ControllerMainActions
            .Concat(ControllerPhotoActions)
            .Concat(ControllerFishingActions)
            .ToArray();

        _allKeyMouseRows = KeyMouseMainActions
            .Concat(KeyMousePhotoActions)
            .Concat(KeyMouseFishingActions)
            .ToArray();

        _controllerRowsByName = _allControllerRows.ToDictionary(
            row => row.Definition.Name,
            StringComparer.Ordinal);

        _keyMouseRowsByName = _allKeyMouseRows.ToDictionary(
            row => row.Definition.Name,
            StringComparer.Ordinal);

        foreach (var row in _allControllerRows)
        {
            row.SelectionChanged += ActionRow_SelectionChanged;
        }

        foreach (var row in _allKeyMouseRows)
        {
            row.SelectionChanged += ActionRow_SelectionChanged;
        }

        RescanCommand = new RelayCommand(RescanDetectedSaves);
        SelectFileCommand = new RelayCommand(SelectSaveFile);
        LoadLayoutCommand = new RelayCommand(LoadLayout);
        ResetCommand = new RelayCommand(ResetValues, () => _session is not null);
        SaveCommand = new RelayCommand(SaveValues, () => CanSave);

        RunSynchronizing(() =>
        {
            RefreshControllerDependentChoices();
            RefreshKeyMouseChoices();
            SynchronizeModeLinks();
        });

        RescanDetectedSaves();
    }

    public ObservableCollection<DetectedSaveFile> DetectedSaveFiles { get; } = new();

    public ObservableCollection<string> ControllerTypes { get; } = new();

    public ObservableCollection<HelperBindingOption> HelperOptions { get; } = new();

    public ObservableCollection<PresetOption> PresetOptions { get; } = new();

    public ObservableCollection<ControllerActionRowViewModel> ControllerMainActions { get; }

    public ObservableCollection<ControllerActionRowViewModel> ControllerPhotoActions { get; }

    public ObservableCollection<ControllerActionRowViewModel> ControllerFishingActions { get; }

    public ObservableCollection<KeyMouseActionRowViewModel> KeyMouseMainActions { get; }

    public ObservableCollection<KeyMouseActionRowViewModel> KeyMousePhotoActions { get; }

    public ObservableCollection<KeyMouseActionRowViewModel> KeyMouseFishingActions { get; }

    public RelayCommand RescanCommand { get; }

    public RelayCommand SelectFileCommand { get; }

    public RelayCommand LoadLayoutCommand { get; }

    public RelayCommand ResetCommand { get; }

    public RelayCommand SaveCommand { get; }

    public DetectedSaveFile? SelectedDetectedSave
    {
        get => _selectedDetectedSave;
        set
        {
            if (!SetProperty(ref _selectedDetectedSave, value)
                || _isUpdatingDetectedSelection
                || value is null)
            {
                return;
            }

            LoadSaveFile(value.FilePath, showErrors: true, statusMessage: $"{value.DisplayName}を読み込み完了");
        }
    }

    public string SelectedSaveFilePath
    {
        get => _selectedSaveFilePath;
        private set => SetProperty(ref _selectedSaveFilePath, value);
    }

    public string LayoutFilePath => _layoutFilePath;

    public string StatusText
    {
        get => _statusText;
        private set => SetProperty(ref _statusText, value);
    }

    public string SelectedControllerType
    {
        get => _selectedControllerType;
        set
        {
            if (!ControllerTypes.Contains(value, StringComparer.OrdinalIgnoreCase))
            {
                value = KeybindCatalog.DefaultControllerType;
            }

            if (!SetProperty(ref _selectedControllerType, value) || _isSynchronizing)
            {
                return;
            }

            RunSynchronizing(() =>
            {
                RefreshControllerDependentChoices();
                SynchronizeModeLinks();
            });
            UpdateSaveState();
        }
    }

    public HelperBindingOption? SelectedHelper1
    {
        get => _selectedHelper1;
        set
        {
            if (!SetProperty(ref _selectedHelper1, value) || _isSynchronizing)
            {
                return;
            }

            RunSynchronizing(() =>
            {
                EnsureDistinctHelpers(changedHelper: 1);
                ClearControllerActionConflicts(value?.MainValue);
                RefreshControllerActionChoices();
                RefreshActionHelperChoices();
                SynchronizeModeLinks();
            });
            UpdateSaveState();
        }
    }

    public HelperBindingOption? SelectedHelper2
    {
        get => _selectedHelper2;
        set
        {
            if (!SetProperty(ref _selectedHelper2, value) || _isSynchronizing)
            {
                return;
            }

            RunSynchronizing(() =>
            {
                EnsureDistinctHelpers(changedHelper: 2);
                ClearControllerActionConflicts(value?.MainValue);
                RefreshControllerActionChoices();
                RefreshActionHelperChoices();
                SynchronizeModeLinks();
            });
            UpdateSaveState();
        }
    }

    public PresetOption? SelectedPreset
    {
        get => _selectedPreset;
        set
        {
            if (SetProperty(ref _selectedPreset, value) && !_isSynchronizing)
            {
                UpdateSaveState();
            }
        }
    }

    public bool IsControllerMode
    {
        get => _isControllerMode;
        set
        {
            if (!value || _isControllerMode)
            {
                return;
            }

            SetInputMode(isControllerMode: true);
        }
    }

    public bool IsKeyMouseMode
    {
        get => !_isControllerMode;
        set
        {
            if (!value || !_isControllerMode)
            {
                return;
            }

            SetInputMode(isControllerMode: false);
        }
    }

    public bool ControllerPhotoModeIndependent
    {
        get => _controllerPhotoModeIndependent;
        set
        {
            if (SetProperty(ref _controllerPhotoModeIndependent, value) && !_isSynchronizing)
            {
                RunSynchronizing(SynchronizeModeLinks);
                UpdateSaveState();
            }
        }
    }

    public bool KeyMousePhotoModeIndependent
    {
        get => _keyMousePhotoModeIndependent;
        set
        {
            if (SetProperty(ref _keyMousePhotoModeIndependent, value) && !_isSynchronizing)
            {
                RunSynchronizing(SynchronizeModeLinks);
                UpdateSaveState();
            }
        }
    }

    public bool KeyMouseFishingModeIndependent
    {
        get => _keyMouseFishingModeIndependent;
        set
        {
            if (SetProperty(ref _keyMouseFishingModeIndependent, value) && !_isSynchronizing)
            {
                RunSynchronizing(SynchronizeModeLinks);
                UpdateSaveState();
            }
        }
    }

    public bool IsPresetSupported
    {
        get => _isPresetSupported;
        private set => SetProperty(ref _isPresetSupported, value);
    }

    public bool CanSave
    {
        get => _canSave;
        private set => SetProperty(ref _canSave, value);
    }

    private ObservableCollection<ControllerActionRowViewModel> CreateControllerRows(KeybindModeGroup group)
    {
        return new ObservableCollection<ControllerActionRowViewModel>(
            KeybindCatalog.GetControllerActions(group)
                .Select(definition => new ControllerActionRowViewModel(
                    definition,
                    KeybindCatalog.GetDisplayActionName(definition.Name, group))));
    }

    private ObservableCollection<KeyMouseActionRowViewModel> CreateKeyMouseRows(KeybindModeGroup group)
    {
        return new ObservableCollection<KeyMouseActionRowViewModel>(
            KeybindCatalog.GetKeyMouseActions(group)
                .Select(definition => new KeyMouseActionRowViewModel(
                    definition,
                    KeybindCatalog.GetDisplayActionName(definition.Name, group),
                    KeybindCatalog.UsesLControlPrefix(definition.Name))));
    }

    private void ActionRow_SelectionChanged(object? sender, EventArgs e)
    {
        if (_isSynchronizing)
        {
            return;
        }

        RunSynchronizing(SynchronizeModeLinks);
        UpdateSaveState();
    }

    private void SetInputMode(bool isControllerMode)
    {
        if (_isControllerMode == isControllerMode)
        {
            return;
        }

        _isControllerMode = isControllerMode;
        OnPropertyChanged(nameof(IsControllerMode));
        OnPropertyChanged(nameof(IsKeyMouseMode));

        if (!_isSynchronizing)
        {
            UpdateSaveState();
        }
    }

    private void RefreshControllerDependentChoices()
    {
        var helper1Value = SelectedHelper1?.MainValue;
        var helper2Value = SelectedHelper2?.MainValue;
        var presetValue = SelectedPreset?.Value;

        ReplaceCollection(
            HelperOptions,
            KeybindCatalog.GetHelperOptions(SelectedControllerType));
        ReplaceCollection(
            PresetOptions,
            KeybindCatalog.GetPresetOptions(SelectedControllerType));

        _selectedHelper1 = FindByMainValue(HelperOptions, helper1Value);
        OnPropertyChanged(nameof(SelectedHelper1));

        _selectedHelper2 = FindByMainValue(HelperOptions, helper2Value);
        OnPropertyChanged(nameof(SelectedHelper2));

        _selectedPreset = FindByValue(PresetOptions, presetValue);
        OnPropertyChanged(nameof(SelectedPreset));

        RefreshControllerActionChoices();
        RefreshActionHelperChoices();
    }

    private void RefreshControllerActionChoices()
    {
        var blockedValues = GetBlockedControllerValues();

        foreach (var row in _allControllerRows)
        {
            var selected = row.SelectedButton;
            var options = KeybindCatalog
                .GetAllowedControllerOptions(row.Definition, SelectedControllerType, blockedValues)
                .ToList();

            if (selected is not null && options.All(option => option.Value != selected.Value))
            {
                var label = KeybindCatalog.GetControllerDisplayMap(SelectedControllerType)
                    .TryGetValue(selected.Value, out var currentLabel)
                    ? currentLabel
                    : selected.Label;
                options.Add(new ControllerInputOption(selected.Value, label));
            }

            row.ReplaceButtonOptions(options, selected?.Value);
        }
    }

    private void RefreshActionHelperChoices()
    {
        var options = new List<ActionHelperOption>
        {
            new(KeybindCatalog.ActionStateSingle, KeybindCatalog.HelperNoneLabel)
        };

        if (SelectedHelper1 is not null)
        {
            options.Add(new ActionHelperOption(
                KeybindCatalog.ActionStateHelper1,
                SelectedHelper1.Label));
        }

        if (SelectedHelper2 is not null)
        {
            options.Add(new ActionHelperOption(
                KeybindCatalog.ActionStateHelper2,
                SelectedHelper2.Label));
        }

        foreach (var row in _allControllerRows)
        {
            var selectedState = row.SelectedHelper?.StateValue ?? KeybindCatalog.ActionStateSingle;
            row.ReplaceHelperOptions(options, selectedState);

            if (!row.UsesHelper)
            {
                row.SelectHelperState(KeybindCatalog.ActionStateSingle);
            }
        }
    }

    private void RefreshKeyMouseChoices()
    {
        foreach (var row in _allKeyMouseRows)
        {
            var selected = row.SelectedKey;
            var options = KeybindCatalog
                .GetAllowedKeyMouseOptions(row.Definition)
                .ToList();

            if (selected is not null
                && options.All(option => option.InputType != selected.InputType || option.Value != selected.Value))
            {
                options.Add(selected);
            }

            row.ReplaceKeyOptions(
                options,
                selected is null ? null : (selected.InputType, selected.Value));
        }
    }

    private void EnsureDistinctHelpers(int changedHelper)
    {
        if (SelectedHelper1 is null
            || SelectedHelper2 is null
            || SelectedHelper1.MainValue != SelectedHelper2.MainValue)
        {
            return;
        }

        if (changedHelper == 1)
        {
            _selectedHelper2 = null;
            OnPropertyChanged(nameof(SelectedHelper2));
        }
        else
        {
            _selectedHelper1 = null;
            OnPropertyChanged(nameof(SelectedHelper1));
        }
    }

    private void ClearControllerActionConflicts(uint? helperMainValue)
    {
        if (helperMainValue is null
            || !KeybindCatalog.HelperMainToActionValue.TryGetValue(helperMainValue.Value, out var actionValue))
        {
            return;
        }

        foreach (var row in _allControllerRows)
        {
            if (row.SelectedButton?.Value == actionValue)
            {
                row.SelectButtonValue(null);
            }
        }
    }

    private HashSet<uint> GetBlockedControllerValues()
    {
        var blocked = new HashSet<uint>();

        if (SelectedHelper1 is not null
            && KeybindCatalog.HelperMainToActionValue.TryGetValue(SelectedHelper1.MainValue, out var helper1ActionValue))
        {
            blocked.Add(helper1ActionValue);
        }

        if (SelectedHelper2 is not null
            && KeybindCatalog.HelperMainToActionValue.TryGetValue(SelectedHelper2.MainValue, out var helper2ActionValue))
        {
            blocked.Add(helper2ActionValue);
        }

        return blocked;
    }

    private void SynchronizeModeLinks()
    {
        SynchronizeControllerModeLinks(
            ControllerPhotoActions,
            KeybindCatalog.ControllerPhotoModeLinks,
            ControllerPhotoModeIndependent);

        SynchronizeControllerModeLinks(
            ControllerFishingActions,
            KeybindCatalog.ControllerFishingModeLinks,
            independent: true);

        SynchronizeKeyMouseModeLinks(
            KeyMousePhotoActions,
            KeybindCatalog.KeyMousePhotoModeLinks,
            KeyMousePhotoModeIndependent);

        SynchronizeKeyMouseModeLinks(
            KeyMouseFishingActions,
            KeybindCatalog.KeyMouseFishingModeLinks,
            KeyMouseFishingModeIndependent);
    }

    private void SynchronizeControllerModeLinks(
        IEnumerable<ControllerActionRowViewModel> modeRows,
        IReadOnlyDictionary<string, string> links,
        bool independent)
    {
        foreach (var row in modeRows)
        {
            if (links.TryGetValue(row.Definition.Name, out var sourceName)
                && _controllerRowsByName.TryGetValue(sourceName, out var sourceRow))
            {
                if (!independent)
                {
                    if (sourceRow.SelectedButton is not null)
                    {
                        row.EnsureButtonOption(sourceRow.SelectedButton);
                    }

                    row.SelectButtonValue(sourceRow.SelectedButton?.Value);
                    row.SelectHelperState(sourceRow.SelectedHelper?.StateValue);
                }

                row.IsEditable = independent;
            }
            else
            {
                row.IsEditable = true;
            }
        }
    }

    private void SynchronizeKeyMouseModeLinks(
        IEnumerable<KeyMouseActionRowViewModel> modeRows,
        IReadOnlyDictionary<string, string> links,
        bool independent)
    {
        foreach (var row in modeRows)
        {
            if (links.TryGetValue(row.Definition.Name, out var sourceName)
                && _keyMouseRowsByName.TryGetValue(sourceName, out var sourceRow))
            {
                if (!independent)
                {
                    if (sourceRow.SelectedKey is not null)
                    {
                        row.EnsureKeyOption(sourceRow.SelectedKey);
                    }

                    row.SelectKeyRecord(
                        sourceRow.SelectedKey?.InputType,
                        sourceRow.SelectedKey?.Value);
                }

                row.IsEditable = independent;
            }
            else
            {
                row.IsEditable = true;
            }
        }
    }

    private void RescanDetectedSaves()
    {
        var previousPath = _session?.FilePath;
        var detected = ScanDetectedSaveFiles();

        RunSynchronizing(() =>
        {
            DetectedSaveFiles.Clear();
            foreach (var item in detected)
            {
                DetectedSaveFiles.Add(item);
            }

            _isUpdatingDetectedSelection = true;
            try
            {
                SelectedDetectedSave = previousPath is null
                    ? DetectedSaveFiles.FirstOrDefault()
                    : DetectedSaveFiles.FirstOrDefault(item => PathsEqual(item.FilePath, previousPath))
                        ?? DetectedSaveFiles.FirstOrDefault();
            }
            finally
            {
                _isUpdatingDetectedSelection = false;
            }
        });

        if (SelectedDetectedSave is null)
        {
            if (_session is null)
            {
                StatusText = "設定ファイルが見つかりません、手動選択してください";
            }

            UpdateSaveState();
            return;
        }

        LoadSaveFile(
            SelectedDetectedSave.FilePath,
            showErrors: false,
            statusMessage: $"{DetectedSaveFiles.Count}件の設定ファイルを検出、{SelectedDetectedSave.DisplayName}を選択中");
    }

    private IReadOnlyList<DetectedSaveFile> ScanDetectedSaveFiles()
    {
        var result = new List<DetectedSaveFile>();
        var root = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            "AppData",
            "LocalLow",
            "bokura");

        if (!Directory.Exists(root))
        {
            return result;
        }

        try
        {
            foreach (var versionDirectory in Directory.EnumerateDirectories(root))
            {
                var envDirectory = Path.Combine(versionDirectory, "localsave", "Env1");
                if (!Directory.Exists(envDirectory))
                {
                    continue;
                }

                foreach (var levelDirectory in Directory.EnumerateDirectories(envDirectory))
                {
                    var uidDirectories = Directory.EnumerateDirectories(levelDirectory).Take(2).ToArray();
                    if (uidDirectories.Length != 1)
                    {
                        continue;
                    }

                    var saveFilePath = Path.Combine(uidDirectories[0], "localsave.bytes");
                    if (!File.Exists(saveFilePath))
                    {
                        continue;
                    }

                    if (new FileInfo(saveFilePath).Length < 2048)
                    {
                        continue;
                    }

                    var versionName = Path.GetFileName(versionDirectory);
                    var uidName = Path.GetFileName(uidDirectories[0]);
                    result.Add(new DetectedSaveFile(
                        $"{versionName}-UID:{uidName}",
                        saveFilePath));
                }
            }
        }
        catch (Exception exception)
        {
            _context.Logger.Warning($"設定ファイルの検索中に一部のフォルダを読み込めませんでした。{exception.Message}");
        }

        return result
            .OrderBy(item => item.DisplayName, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private void SelectSaveFile()
    {
        var dialog = new OpenFileDialog
        {
            Title = "設定ファイルを選択",
            Filter = "Bytes files (*.bytes)|*.bytes",
            InitialDirectory = ResolveDefaultOpenDirectory() ?? string.Empty,
            FileName = "localsave.bytes",
            CheckFileExists = true,
            Multiselect = false
        };

        if (dialog.ShowDialog() == true)
        {
            LoadSaveFile(dialog.FileName, showErrors: true, statusMessage: "読み込み完了");
        }
    }

    private void LoadSaveFile(string filePath, bool showErrors, string statusMessage)
    {
        try
        {
            var session = _saveService.Load(filePath);

            RunSynchronizing(() =>
            {
                _session = session;
                SelectedSaveFilePath = session.FilePath;
                IsPresetSupported = session.IsPresetSupported;

                UpdateDetectedSelectionForPath(session.FilePath);
                LoadValuesFromSession();
            });

            StatusText = session.IsPresetSupported
                ? statusMessage
                : $"{statusMessage}（確認/キャンセル設定はこのファイルでは編集できません）";

            UpdateSaveState();
        }
        catch (Exception exception)
        {
            _context.Logger.Error("キーバインド設定ファイルの読み込みに失敗しました。", exception);

            if (_session is null)
            {
                SelectedSaveFilePath = string.Empty;
                IsPresetSupported = false;
                StatusText = "読み込み失敗";
            }

            UpdateSaveState();

            if (showErrors)
            {
                MessageBox.Show(
                    "ファイルの読み込みに失敗しました。\n対応していないファイルか、データが破損している可能性があります。",
                    "読み込みエラー",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }
    }

    private void LoadValuesFromSession()
    {
        if (_session is null)
        {
            return;
        }

        RefreshControllerDependentChoices();
        RefreshKeyMouseChoices();

        if (_session.IsPresetSupported && _session.PresetOffset is not null)
        {
            var presetValue = _session.Data[_session.PresetOffset.Value];
            var preset = PresetOptions.FirstOrDefault(option => option.Value == presetValue);
            if (preset is null)
            {
                preset = new PresetOption(presetValue, $"不明 (0x{presetValue:X2})");
                PresetOptions.Add(preset);
            }

            _selectedPreset = preset;
            OnPropertyChanged(nameof(SelectedPreset));
        }
        else
        {
            _selectedPreset = PresetOptions.FirstOrDefault();
            OnPropertyChanged(nameof(SelectedPreset));
        }

        var helper1Value = _saveService.ReadUInt32(_session, _session.Helper1Offset);
        var helper2Value = _saveService.ReadUInt32(_session, _session.Helper2Offset);

        _selectedHelper1 = FindByMainValue(HelperOptions, helper1Value);
        OnPropertyChanged(nameof(SelectedHelper1));

        _selectedHelper2 = FindByMainValue(HelperOptions, helper2Value);
        OnPropertyChanged(nameof(SelectedHelper2));

        RefreshControllerActionChoices();
        RefreshActionHelperChoices();

        foreach (var row in _allControllerRows)
        {
            var offset = _saveService.GetControllerOffsets(_session, row.Definition).First();
            var value = _saveService.ReadUInt32(_session, offset);
            var option = row.ButtonOptions.FirstOrDefault(candidate => candidate.Value == value);
            if (option is null)
            {
                var label = KeybindCatalog.GetControllerDisplayMap(SelectedControllerType)
                    .TryGetValue(value, out var knownLabel)
                    ? knownLabel
                    : $"不明 (0x{value:X8})";
                option = new ControllerInputOption(value, label);
                row.EnsureButtonOption(option);
            }

            row.SelectedButton = option;

            var state = _saveService.ReadUInt32(_session, offset + sizeof(uint));
            var helperState = state is KeybindCatalog.ActionStateSingle
                or KeybindCatalog.ActionStateHelper1
                or KeybindCatalog.ActionStateHelper2
                ? state
                : KeybindCatalog.ActionStateSingle;
            row.SelectHelperState(row.UsesHelper ? helperState : KeybindCatalog.ActionStateSingle);
        }

        foreach (var row in _allKeyMouseRows)
        {
            var offset = _saveService.GetKeyMouseOffsets(_session, row.Definition).First();
            var inputType = _saveService.ReadInputType(_session, offset);
            var value = _saveService.ReadUInt32(_session, offset);

            var option = row.KeyOptions.FirstOrDefault(candidate => candidate.InputType == inputType
                && candidate.Value == value);
            if (option is null)
            {
                option = new KeyMouseInputOption(
                    inputType,
                    value,
                    $"不明 (type=0x{inputType:X8}, value=0x{value:X8})");
                row.EnsureKeyOption(option);
            }

            row.SelectedKey = option;
        }

        SynchronizeModeLinks();
    }

    private void ResetValues()
    {
        if (_session is null)
        {
            return;
        }

        RunSynchronizing(LoadValuesFromSession);
        StatusText = "読み込み時の状態に戻しました";
        UpdateSaveState();
    }

    private void LoadLayout()
    {
        if (!File.Exists(_layoutFilePath))
        {
            MessageBox.Show(
                $"配置ファイルが見つかりません。\n{_layoutFilePath}",
                "配置読み込みエラー",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
            return;
        }

        try
        {
            var json = File.ReadAllText(_layoutFilePath);
            var layout = JsonSerializer.Deserialize<KeybindLayoutConfig>(json, JsonOptions)
                ?? throw new InvalidDataException("配置ファイルの形式が不正です。");

            RunSynchronizing(() =>
            {
                ApplyLayout(layout);
                SynchronizeModeLinks();
            });

            StatusText = $"キー設定を読み込みました: {Path.GetFileName(_layoutFilePath)}";
            UpdateSaveState();

            MessageBox.Show(
                "キー設定を読み込みました。\nゲーム設定へ反映するには、通常の保存ボタンを押してください。",
                "配置読み込み",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
        }
        catch (Exception exception)
        {
            _context.Logger.Error("キー設定プリセットの読み込みに失敗しました。", exception);
            StatusText = "キー設定の読み込みに失敗しました";
            UpdateSaveState();

            MessageBox.Show(
                $"キー設定の読み込みに失敗しました。\n{exception.Message}",
                "配置読み込みエラー",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
        }
    }

    private void ApplyLayout(KeybindLayoutConfig layout)
    {
        var controllerProfile = layout.ControllerProfile ?? new ControllerLayoutProfile();
        var keyMouseProfile = layout.KeyMouseProfile ?? new KeyMouseLayoutProfile();

        var controllerType = ControllerTypes.Contains(controllerProfile.ControllerType, StringComparer.OrdinalIgnoreCase)
            ? controllerProfile.ControllerType
            : KeybindCatalog.DefaultControllerType;

        _selectedControllerType = controllerType;
        OnPropertyChanged(nameof(SelectedControllerType));

        RefreshControllerDependentChoices();

        _selectedHelper1 = HelperOptions.FirstOrDefault(option =>
            string.Equals(option.Label, controllerProfile.Keybind?.Helper1, StringComparison.Ordinal));
        OnPropertyChanged(nameof(SelectedHelper1));

        _selectedHelper2 = HelperOptions.FirstOrDefault(option =>
            string.Equals(option.Label, controllerProfile.Keybind?.Helper2, StringComparison.Ordinal));
        OnPropertyChanged(nameof(SelectedHelper2));

        RefreshControllerActionChoices();
        RefreshActionHelperChoices();

        _selectedPreset = PresetOptions.FirstOrDefault(option =>
            string.Equals(option.Label, controllerProfile.Keybind?.Preset, StringComparison.Ordinal))
            ?? PresetOptions.FirstOrDefault();
        OnPropertyChanged(nameof(SelectedPreset));

        ApplyControllerActionLayout(controllerProfile.Actions);
        ApplyKeyMouseActionLayout(keyMouseProfile.Actions);

        _controllerPhotoModeIndependent = controllerProfile.PhotoModeIndependent;
        OnPropertyChanged(nameof(ControllerPhotoModeIndependent));

        _keyMousePhotoModeIndependent = keyMouseProfile.PhotoModeIndependent;
        OnPropertyChanged(nameof(KeyMousePhotoModeIndependent));

        _keyMouseFishingModeIndependent = keyMouseProfile.FishingModeIndependent;
        OnPropertyChanged(nameof(KeyMouseFishingModeIndependent));

        SetInputMode(!string.Equals(
            layout.InputDevice,
            KeybindCatalog.KeyMouseInputDeviceName,
            StringComparison.Ordinal));
    }

    private void ApplyControllerActionLayout(IReadOnlyDictionary<string, ControllerActionLayout>? actions)
    {
        if (actions is null)
        {
            return;
        }

        foreach (var pair in actions)
        {
            if (!_controllerRowsByName.TryGetValue(pair.Key, out var row) || pair.Value is null)
            {
                continue;
            }

            var button = row.ButtonOptions.FirstOrDefault(option =>
                string.Equals(option.Label, pair.Value.Button, StringComparison.Ordinal));
            if (button is not null)
            {
                row.SelectedButton = button;
            }

            var helper = row.HelperOptions.FirstOrDefault(option =>
                string.Equals(option.Label, pair.Value.Helper, StringComparison.Ordinal));
            if (helper is not null)
            {
                row.SelectedHelper = helper;
            }
        }
    }

    private void ApplyKeyMouseActionLayout(IReadOnlyDictionary<string, KeyMouseActionLayout>? actions)
    {
        if (actions is null)
        {
            return;
        }

        foreach (var pair in actions)
        {
            if (!_keyMouseRowsByName.TryGetValue(pair.Key, out var row) || pair.Value is null)
            {
                continue;
            }

            var key = row.KeyOptions.FirstOrDefault(option =>
                string.Equals(option.Label, pair.Value.Key, StringComparison.Ordinal));
            if (key is not null)
            {
                row.SelectedKey = key;
            }
        }
    }

    private void SaveValues()
    {
        if (_session is null || !CanSave)
        {
            return;
        }

        try
        {
            var data = _session.Data.ToArray();

            if (IsControllerMode)
            {
                SaveControllerValues(data);
            }
            else
            {
                SaveKeyMouseValues(data);
            }

            _saveService.Save(_session.FilePath, data);
            _session.ReplaceData(data);
            SaveLayoutFile();

            RunSynchronizing(LoadValuesFromSession);

            StatusText = "保存しました";
            UpdateSaveState();

            MessageBox.Show(
                $"保存しました。\nキー設定プリセットを作成しました。\n{_layoutFilePath}",
                "保存完了",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
        }
        catch (Exception exception)
        {
            _context.Logger.Error("キーバインド設定の保存に失敗しました。", exception);
            StatusText = "保存失敗";
            UpdateSaveState();

            MessageBox.Show(
                "保存に失敗しました。入力内容を確認してください。",
                "保存エラー",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
        }
    }

    private void SaveControllerValues(byte[] data)
    {
        if (_session is null
            || SelectedHelper1 is null
            || SelectedHelper2 is null
            || (_session.IsPresetSupported && SelectedPreset is null))
        {
            throw new InvalidOperationException("ゲームパッド設定に未入力の項目があります。");
        }

        if (_session.IsPresetSupported && _session.PresetOffset is not null)
        {
            data[_session.PresetOffset.Value] = checked((byte)SelectedPreset!.Value);
        }

        _saveService.WriteUInt32(data, _session.Helper1Offset, SelectedHelper1.MainValue);
        _saveService.WriteUInt32(data, _session.Helper2Offset, SelectedHelper2.MainValue);

        foreach (var row in _allControllerRows)
        {
            if (row.SelectedButton is null || row.SelectedHelper is null)
            {
                throw new InvalidOperationException($"{row.DisplayName} に未設定の項目があります。");
            }

            var state = row.UsesHelper
                ? row.SelectedHelper.StateValue
                : KeybindCatalog.ActionStateSingle;

            foreach (var offset in _saveService.GetWritableControllerOffsets(_session, row.Definition))
            {
                _saveService.WriteUInt32(data, offset, row.SelectedButton.Value);
                _saveService.WriteUInt32(data, offset + sizeof(uint), state);
            }
        }
    }

    private void SaveKeyMouseValues(byte[] data)
    {
        if (_session is null)
        {
            throw new InvalidOperationException("設定ファイルが読み込まれていません。");
        }

        foreach (var row in _allKeyMouseRows)
        {
            if (row.SelectedKey is null)
            {
                throw new InvalidOperationException($"{row.DisplayName} に未設定の項目があります。");
            }

            foreach (var offset in _saveService.GetWritableKeyMouseOffsets(_session, row.Definition))
            {
                _saveService.WriteUInt32(data, offset - sizeof(uint), row.SelectedKey.InputType);
                _saveService.WriteUInt32(data, offset, row.SelectedKey.Value);
            }
        }
    }

    private void SaveLayoutFile()
    {
        var directory = Path.GetDirectoryName(_layoutFilePath);
        if (string.IsNullOrWhiteSpace(directory))
        {
            throw new InvalidOperationException("キー設定プリセットの保存先を特定できません。");
        }

        Directory.CreateDirectory(directory);

        var layout = new KeybindLayoutConfig
        {
            Version = 4,
            InputDevice = IsKeyMouseMode
                ? KeybindCatalog.KeyMouseInputDeviceName
                : SelectedControllerType,
            ControllerProfile = new ControllerLayoutProfile
            {
                ControllerType = SelectedControllerType,
                PhotoModeIndependent = ControllerPhotoModeIndependent,
                Keybind = new ControllerKeybindProfile
                {
                    Helper1 = SelectedHelper1?.Label ?? string.Empty,
                    Helper2 = SelectedHelper2?.Label ?? string.Empty,
                    Preset = SelectedPreset?.Label ?? string.Empty
                },
                Actions = _allControllerRows.ToDictionary(
                    row => row.Definition.Name,
                    row => new ControllerActionLayout
                    {
                        Helper = row.SelectedHelper?.Label ?? string.Empty,
                        Button = row.SelectedButton?.Label ?? string.Empty
                    },
                    StringComparer.Ordinal)
            },
            KeyMouseProfile = new KeyMouseLayoutProfile
            {
                PhotoModeIndependent = KeyMousePhotoModeIndependent,
                FishingModeIndependent = KeyMouseFishingModeIndependent,
                Actions = _allKeyMouseRows.ToDictionary(
                    row => row.Definition.Name,
                    row => new KeyMouseActionLayout
                    {
                        Key = row.SelectedKey?.Label ?? string.Empty
                    },
                    StringComparer.Ordinal)
            }
        };

        var temporaryPath = $"{_layoutFilePath}.tmp";
        try
        {
            var json = JsonSerializer.Serialize(layout, JsonOptions);
            File.WriteAllText(temporaryPath, json);
            File.Move(temporaryPath, _layoutFilePath, overwrite: true);
        }
        finally
        {
            if (File.Exists(temporaryPath))
            {
                File.Delete(temporaryPath);
            }
        }
    }

    private void UpdateDetectedSelectionForPath(string filePath)
    {
        var matching = DetectedSaveFiles.FirstOrDefault(item => PathsEqual(item.FilePath, filePath));
        if (matching is null)
        {
            return;
        }

        _isUpdatingDetectedSelection = true;
        try
        {
            SelectedDetectedSave = matching;
        }
        finally
        {
            _isUpdatingDetectedSelection = false;
        }
    }

    private static bool PathsEqual(string left, string right)
    {
        try
        {
            return string.Equals(
                Path.GetFullPath(left),
                Path.GetFullPath(right),
                StringComparison.OrdinalIgnoreCase);
        }
        catch
        {
            return string.Equals(left, right, StringComparison.OrdinalIgnoreCase);
        }
    }

    private string? ResolveDefaultOpenDirectory()
    {
        var root = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            "AppData",
            "LocalLow",
            "bokura");

        var first = PickOnlySubdirectory(root);
        if (first is not null)
        {
            var second = PickOnlySubdirectory(Path.Combine(first, "localsave", "Env1"));
            if (second is not null)
            {
                var third = PickOnlySubdirectory(second);
                if (third is not null)
                {
                    return third;
                }
            }
        }

        return Directory.Exists(root) ? root : null;
    }

    private static string? PickOnlySubdirectory(string parentPath)
    {
        if (!Directory.Exists(parentPath))
        {
            return null;
        }

        try
        {
            var directories = Directory.EnumerateDirectories(parentPath).Take(2).ToArray();
            return directories.Length == 1 ? directories[0] : null;
        }
        catch
        {
            return null;
        }
    }

    private void UpdateSaveState()
    {
        CanSave = _session is not null
            && (IsControllerMode
                ? IsControllerSaveComplete()
                : _allKeyMouseRows.All(row => row.SelectedKey is not null));

        SaveCommand.RaiseCanExecuteChanged();
        ResetCommand.RaiseCanExecuteChanged();
    }

    private bool IsControllerSaveComplete()
    {
        if (SelectedHelper1 is null || SelectedHelper2 is null)
        {
            return false;
        }

        if (_session?.IsPresetSupported == true && SelectedPreset is null)
        {
            return false;
        }

        return _allControllerRows.All(row => row.SelectedButton is not null
            && (!row.UsesHelper || row.SelectedHelper is not null));
    }

    private void RunSynchronizing(Action action)
    {
        var previous = _isSynchronizing;
        _isSynchronizing = true;
        try
        {
            action();
        }
        finally
        {
            _isSynchronizing = previous;
        }
    }

    private static void ReplaceCollection<T>(
        ObservableCollection<T> target,
        IEnumerable<T> values)
    {
        target.Clear();
        foreach (var value in values)
        {
            target.Add(value);
        }
    }

    private static HelperBindingOption? FindByMainValue(
        IEnumerable<HelperBindingOption> options,
        uint? mainValue)
    {
        return mainValue is null
            ? null
            : options.FirstOrDefault(option => option.MainValue == mainValue.Value);
    }

    private static PresetOption? FindByValue(
        IEnumerable<PresetOption> options,
        uint? value)
    {
        return value is null
            ? null
            : options.FirstOrDefault(option => option.Value == value.Value);
    }
}
