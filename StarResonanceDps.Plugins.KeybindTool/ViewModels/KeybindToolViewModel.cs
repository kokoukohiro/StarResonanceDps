using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Text.Json;
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
    private readonly PluginLocalizer _texts;
    private readonly BpsrKeybindSaveService _saveService;
    private readonly string _layoutFilePath;
    private readonly IReadOnlyList<string> _legacyLayoutFilePaths;
    private readonly Dictionary<string, ControllerActionRowViewModel> _controllerRowsById;
    private readonly Dictionary<string, KeyMouseActionRowViewModel> _keyMouseRowsById;
    private readonly Dictionary<string, KeyMouseInputOption> _customKeyMouseOptionsByStorageKey = new(StringComparer.Ordinal);
    private readonly IReadOnlyList<ControllerActionRowViewModel> _allControllerRows;
    private readonly IReadOnlyList<KeyMouseActionRowViewModel> _allKeyMouseRows;

    private KeybindSaveSession? _session;
    private ControllerLayoutProfile? _controllerProfileCache;
    private KeyMouseLayoutProfile? _keyMouseProfileCache;
    private DetectedSaveFile? _selectedDetectedSave;
    private string _selectedSaveFilePath = string.Empty;
    private string _statusText = string.Empty;
    private Func<PluginLocalizer, string>? _statusTextFormatter;
    private string _selectedControllerType = KeybindCatalog.DefaultControllerType;
    private HelperBindingOption? _selectedHelper1;
    private HelperBindingOption? _selectedHelper2;
    private PresetOption? _selectedPreset;
    private bool _isControllerMode = true;
    private bool _controllerQuickWheelIndependent;
    private bool _keyMouseQuickWheelIndependent;
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
        _texts = new PluginLocalizer(
            _context.Localization,
            typeof(KeybindToolViewModel).Assembly,
            "StarResonanceDps.Plugins.KeybindTool.Properties.Resources");
        _texts.CultureChanged += Texts_CultureChanged;
        _saveService = new BpsrKeybindSaveService(_texts);

        // The plugin owns its preset filename.
        // The host settings store fixes the actual location under runtime\\Plugins.
        _layoutFilePath = _context.Settings.GetFilePath(KeybindCatalog.ButtonLayoutFileName);
        _legacyLayoutFilePaths = new[]
        {
            // Previous storage location: the executable directory.
            Path.Combine(AppContext.BaseDirectory, KeybindCatalog.ButtonLayoutFileName),
            // Previous plugin-data location: Data\\PluginData\\{PluginId}.
            Path.Combine(
                AppContext.BaseDirectory,
                "Data",
                "PluginData",
                _context.PluginId,
                KeybindCatalog.ButtonLayoutFileName)
        };

        foreach (var controllerType in KeybindCatalog.ControllerTypes)
        {
            ControllerTypes.Add(controllerType);
        }

        ControllerMainActions = CreateControllerRows(KeybindModeGroup.Main);
        ControllerQuickWheelActions = CreateControllerRows(KeybindModeGroup.QuickWheel);
        ControllerPhotoActions = CreateControllerRows(KeybindModeGroup.Photo);
        ControllerFishingActions = CreateControllerRows(KeybindModeGroup.Fishing);

        KeyMouseMainActions = CreateKeyMouseRows(KeybindModeGroup.Main);
        KeyMouseQuickWheelActions = CreateKeyMouseRows(KeybindModeGroup.QuickWheel);
        KeyMousePhotoActions = CreateKeyMouseRows(KeybindModeGroup.Photo);
        KeyMouseFishingActions = CreateKeyMouseRows(KeybindModeGroup.Fishing);

        _allControllerRows = ControllerMainActions
            .Concat(ControllerQuickWheelActions)
            .Concat(ControllerPhotoActions)
            .Concat(ControllerFishingActions)
            .ToArray();

        _allKeyMouseRows = KeyMouseMainActions
            .Concat(KeyMouseQuickWheelActions)
            .Concat(KeyMousePhotoActions)
            .Concat(KeyMouseFishingActions)
            .ToArray();

        _controllerRowsById = _allControllerRows.ToDictionary(
            row => row.Definition.Id,
            StringComparer.Ordinal);
        _keyMouseRowsById = _allKeyMouseRows.ToDictionary(
            row => row.Definition.Id,
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
        OpenSelectedFileLocationCommand = new RelayCommand(OpenSelectedFileLocation, () => HasSelectedSaveFile);
        LoadLayoutCommand = new RelayCommand(LoadLayout);
        OpenLayoutFileLocationCommand = new RelayCommand(OpenLayoutFileLocation);
        ResetCommand = new RelayCommand(ResetValues, () => _session is not null);
        SaveCommand = new RelayCommand(SaveValues, () => CanSave);

        RunSynchronizing(() =>
        {
            RefreshControllerDependentChoices();
            RefreshKeyMouseChoices();
            RestoreIndependentSettingsFromLayoutFile();
            SynchronizeModeLinks();
        });

        SetStatus("Keybind.Status.NoFileSelected");
        RescanDetectedSaves();
    }

    public ObservableCollection<DetectedSaveFile> DetectedSaveFiles { get; } = new();

    public PluginLocalizer Texts => _texts;

    public ObservableCollection<string> ControllerTypes { get; } = new();

    public ObservableCollection<HelperBindingOption> HelperOptions { get; } = new();

    public ObservableCollection<PresetOption> PresetOptions { get; } = new();

    public ObservableCollection<ControllerActionRowViewModel> ControllerMainActions { get; }

    public ObservableCollection<ControllerActionRowViewModel> ControllerQuickWheelActions { get; }

    public ObservableCollection<ControllerActionRowViewModel> ControllerPhotoActions { get; }

    public ObservableCollection<ControllerActionRowViewModel> ControllerFishingActions { get; }

    public ObservableCollection<KeyMouseActionRowViewModel> KeyMouseMainActions { get; }

    public ObservableCollection<KeyMouseActionRowViewModel> KeyMouseQuickWheelActions { get; }

    public ObservableCollection<KeyMouseActionRowViewModel> KeyMousePhotoActions { get; }

    public ObservableCollection<KeyMouseActionRowViewModel> KeyMouseFishingActions { get; }

    public RelayCommand RescanCommand { get; }

    public RelayCommand SelectFileCommand { get; }

    public RelayCommand OpenSelectedFileLocationCommand { get; }

    public RelayCommand LoadLayoutCommand { get; }

    public RelayCommand OpenLayoutFileLocationCommand { get; }

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

            LoadSaveFile(
                value.FilePath,
                true,
                "Keybind.Status.LoadedSelected",
                value.DisplayName);
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

    public bool HasSelectedSaveFile => _session is not null;

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
            CacheCurrentActiveLayoutProfile();
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
            CacheCurrentActiveLayoutProfile();
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
            CacheCurrentActiveLayoutProfile();
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
                CacheCurrentActiveLayoutProfile();
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

    public bool ControllerQuickWheelIndependent
    {
        get => _controllerQuickWheelIndependent;
        set
        {
            if (SetProperty(ref _controllerQuickWheelIndependent, value) && !_isSynchronizing)
            {
                HandleControllerIndependentChanged(
                    value,
                    ControllerQuickWheelActions,
                    KeybindCatalog.ControllerQuickWheelLinks);
            }
        }
    }

    public bool KeyMouseQuickWheelIndependent
    {
        get => _keyMouseQuickWheelIndependent;
        set
        {
            if (SetProperty(ref _keyMouseQuickWheelIndependent, value) && !_isSynchronizing)
            {
                HandleKeyMouseIndependentChanged(
                    value,
                    KeyMouseQuickWheelActions,
                    KeybindCatalog.KeyMouseQuickWheelLinks);
            }
        }
    }

    public bool ControllerPhotoModeIndependent
    {
        get => _controllerPhotoModeIndependent;
        set
        {
            if (SetProperty(ref _controllerPhotoModeIndependent, value) && !_isSynchronizing)
            {
                HandleControllerIndependentChanged(
                    value,
                    ControllerPhotoActions,
                    KeybindCatalog.ControllerPhotoModeLinks);
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
                HandleKeyMouseIndependentChanged(
                    value,
                    KeyMousePhotoActions,
                    KeybindCatalog.KeyMousePhotoModeLinks);
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
                HandleKeyMouseIndependentChanged(
                    value,
                    KeyMouseFishingActions,
                    KeybindCatalog.KeyMouseFishingModeLinks);
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

    private void RestoreIndependentSettingsFromLayoutFile()
    {
        var layoutFilePath = GetExistingLayoutFilePath();
        if (layoutFilePath is null)
        {
            return;
        }

        try
        {
            var layout = ReadLayoutFile(layoutFilePath);

            if (layout.ControllerProfile is not null)
            {
                _controllerQuickWheelIndependent = layout.ControllerProfile.QuickWheelIndependent;
                _controllerPhotoModeIndependent = layout.ControllerProfile.PhotoModeIndependent;
                OnPropertyChanged(nameof(ControllerQuickWheelIndependent));
                OnPropertyChanged(nameof(ControllerPhotoModeIndependent));
            }

            if (layout.KeyMouseProfile is not null)
            {
                _keyMouseQuickWheelIndependent = layout.KeyMouseProfile.QuickWheelIndependent;
                _keyMousePhotoModeIndependent = layout.KeyMouseProfile.PhotoModeIndependent;
                _keyMouseFishingModeIndependent = layout.KeyMouseProfile.FishingModeIndependent;
                OnPropertyChanged(nameof(KeyMouseQuickWheelIndependent));
                OnPropertyChanged(nameof(KeyMousePhotoModeIndependent));
                OnPropertyChanged(nameof(KeyMouseFishingModeIndependent));
            }
        }
        catch (Exception)
        {
            // At startup, restore only the independent-mode flags as a convenience.
            // A malformed layout is reported explicitly through the normal load command.
        }
    }

    private ObservableCollection<ControllerActionRowViewModel> CreateControllerRows(KeybindModeGroup group)
    {
        var rows = new ObservableCollection<ControllerActionRowViewModel>(
            KeybindCatalog.GetControllerActions(group)
                .Select(definition => new ControllerActionRowViewModel(
                     definition,
                     Texts[KeybindCatalog.GetActionLocalizationKey(definition)])));

        if (rows.Count > 0)
        {
            rows[^1].IsLastInSection = true;
        }

        return rows;
    }

    private ObservableCollection<KeyMouseActionRowViewModel> CreateKeyMouseRows(KeybindModeGroup group)
    {
        var rows = new ObservableCollection<KeyMouseActionRowViewModel>(
            KeybindCatalog.GetKeyMouseActions(group)
                .Select(definition => new KeyMouseActionRowViewModel(
                    definition,
                    Texts[KeybindCatalog.GetActionLocalizationKey(definition)],
                    KeybindCatalog.UsesLControlPrefix(definition))));

        if (rows.Count > 0)
        {
            rows[^1].IsLastInSection = true;
        }

        return rows;
    }

    private void ActionRow_SelectionChanged(object? sender, EventArgs e)
    {
        if (_isSynchronizing)
        {
            return;
        }

        RunSynchronizing(SynchronizeModeLinks);
        CacheCurrentActiveLayoutProfile();
        UpdateSaveState();
    }

    private void SetInputMode(bool isControllerMode)
    {
        if (_isControllerMode == isControllerMode)
        {
            return;
        }

        CacheCurrentActiveLayoutProfile();

        RunSynchronizing(() =>
        {
            _isControllerMode = isControllerMode;
            OnPropertyChanged(nameof(IsControllerMode));
            OnPropertyChanged(nameof(IsKeyMouseMode));

            ApplyCachedLayoutForCurrentInput();
            SynchronizeModeLinks();
        });

        CacheCurrentActiveLayoutProfile();
        UpdateSaveState();
    }

    private void RefreshControllerDependentChoices()
    {
        var helper1Value = SelectedHelper1?.MainValue;
        var helper2Value = SelectedHelper2?.MainValue;
        var presetValue = SelectedPreset?.Value;

        ReplaceCollection(HelperOptions, KeybindCatalog.GetHelperOptions(SelectedControllerType, Texts));
        ReplaceCollection(PresetOptions, KeybindCatalog.GetPresetOptions(SelectedControllerType, Texts));

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
                .GetAllowedControllerOptions(row.Definition, SelectedControllerType, blockedValues, Texts)
                .ToList();

            if (selected is not null && options.All(option => option.Value != selected.Value))
            {
                var label = KeybindCatalog.GetControllerInputDisplayLabel(
                    SelectedControllerType,
                    selected.Value,
                    Texts);
                options.Add(KeybindCatalog.CreateControllerInputOption(
                    SelectedControllerType,
                    selected.Value,
                    label,
                    Texts));
            }

            row.ReplaceButtonOptions(options, selected?.Value);
        }
    }

    private void RefreshActionHelperChoices()
    {
        var options = new List<ActionHelperOption>
        {
            new(KeybindCatalog.ActionStateSingle, KeybindCatalog.HelperNoneLabel)
            {
                Visual = KeybindInputVisual.TextOnly(Texts["Keybind.Value.Unassigned"])
            }
        };

        if (SelectedHelper1 is not null)
        {
            options.Add(new ActionHelperOption(KeybindCatalog.ActionStateHelper1, SelectedHelper1.Label)
            {
                Visual = SelectedHelper1.Visual
            });
        }

        if (SelectedHelper2 is not null)
        {
            options.Add(new ActionHelperOption(KeybindCatalog.ActionStateHelper2, SelectedHelper2.Label)
            {
                Visual = SelectedHelper2.Visual
            });
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
            var options = KeybindCatalog.GetAllowedKeyMouseOptions(row.Definition, Texts).ToList();

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
        if (IsKeyMouseMode)
        {
            SynchronizeKeyMouseLinkGroup(
                KeyMouseQuickWheelActions,
                KeybindCatalog.KeyMouseQuickWheelLinks,
                KeyMouseQuickWheelIndependent);
            SynchronizeKeyMouseLinkGroup(
                KeyMousePhotoActions,
                KeybindCatalog.KeyMousePhotoModeLinks,
                KeyMousePhotoModeIndependent);
            SynchronizeKeyMouseLinkGroup(
                KeyMouseFishingActions,
                KeybindCatalog.KeyMouseFishingModeLinks,
                KeyMouseFishingModeIndependent);
            return;
        }

        SynchronizeControllerLinkGroup(
            ControllerQuickWheelActions,
            KeybindCatalog.ControllerQuickWheelLinks,
            ControllerQuickWheelIndependent);
        SynchronizeControllerLinkGroup(
            ControllerPhotoActions,
            KeybindCatalog.ControllerPhotoModeLinks,
            ControllerPhotoModeIndependent);
        SynchronizeControllerLinkGroup(
            ControllerFishingActions,
            KeybindCatalog.ControllerFishingModeLinks,
            independent: true);
    }

    private void SynchronizeControllerLinkGroup(
        IEnumerable<ControllerActionRowViewModel> modeRows,
        IReadOnlyDictionary<string, string> links,
        bool independent)
    {
        foreach (var row in modeRows)
        {
            if (links.TryGetValue(row.Definition.Id, out var sourceId)
                && _controllerRowsById.TryGetValue(sourceId, out var sourceRow))
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

    private void SynchronizeKeyMouseLinkGroup(
        IEnumerable<KeyMouseActionRowViewModel> modeRows,
        IReadOnlyDictionary<string, string> links,
        bool independent)
    {
        foreach (var row in modeRows)
        {
            if (links.TryGetValue(row.Definition.Id, out var sourceId)
                && _keyMouseRowsById.TryGetValue(sourceId, out var sourceRow))
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

    private void HandleControllerIndependentChanged(
        bool independent,
        IEnumerable<ControllerActionRowViewModel> modeRows,
        IReadOnlyDictionary<string, string> links)
    {
        RunSynchronizing(() =>
        {
            if (independent)
            {
                RestoreControllerLinkedActionsFromSession(modeRows, links);
            }

            SynchronizeControllerLinkGroup(modeRows, links, independent);
        });
        CacheCurrentActiveLayoutProfile();
        UpdateSaveState();
    }

    private void HandleKeyMouseIndependentChanged(
        bool independent,
        IEnumerable<KeyMouseActionRowViewModel> modeRows,
        IReadOnlyDictionary<string, string> links)
    {
        RunSynchronizing(() =>
        {
            if (independent)
            {
                RestoreKeyMouseLinkedActionsFromSession(modeRows, links);
            }

            SynchronizeKeyMouseLinkGroup(modeRows, links, independent);
        });
        CacheCurrentActiveLayoutProfile();
        UpdateSaveState();
    }

    private void RestoreControllerLinkedActionsFromSession(
        IEnumerable<ControllerActionRowViewModel> modeRows,
        IReadOnlyDictionary<string, string> links)
    {
        if (_session is null)
        {
            return;
        }

        foreach (var row in modeRows)
        {
            if (links.ContainsKey(row.Definition.Id))
            {
                LoadControllerActionFromSession(row);
            }
        }
    }

    private void RestoreKeyMouseLinkedActionsFromSession(
        IEnumerable<KeyMouseActionRowViewModel> modeRows,
        IReadOnlyDictionary<string, string> links)
    {
        if (_session is null)
        {
            return;
        }

        foreach (var row in modeRows)
        {
            if (links.ContainsKey(row.Definition.Id))
            {
                LoadKeyMouseActionFromSession(row);
            }
        }
    }

    private void RescanDetectedSaves()
    {
        var previousPath = _session?.FilePath ?? SelectedDetectedSave?.FilePath;
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
                SetStatus("Keybind.Status.NoDetectedSave");
            }

            UpdateSaveState();
            return;
        }

        LoadSaveFile(
            SelectedDetectedSave.FilePath,
            false,
            "Keybind.Status.DetectedSelected",
            DetectedSaveFiles.Count,
            SelectedDetectedSave.DisplayName);
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
                    if (!File.Exists(saveFilePath) || new FileInfo(saveFilePath).Length < 2048)
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
            _context.Logger.Warning($"Some folders could not be read while scanning settings files. {exception.Message}");
        }

        return result
            .OrderBy(item => item.DisplayName, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private void SelectSaveFile()
    {
        var dialog = new OpenFileDialog
        {
            Title = Texts["Keybind.Dialog.SelectSaveFile.Title"],
            Filter = Texts["Keybind.Dialog.SelectSaveFile.Filter"],
            InitialDirectory = ResolveDefaultOpenDirectory() ?? string.Empty,
            FileName = "localsave.bytes",
            CheckFileExists = true,
            Multiselect = false
        };

        if (dialog.ShowDialog() == true)
        {
            LoadSaveFile(
                dialog.FileName,
                true,
                "Keybind.Status.LoadCompleted");
        }
    }

    private void OpenSelectedFileLocation()
    {
        OpenFileLocation(
            _session?.FilePath,
            "Keybind.Message.SelectFileFirst");
    }

    private void OpenLayoutFileLocation()
    {
        OpenFileLocation(
            _layoutFilePath,
            "Keybind.Message.LayoutLocationUnavailable");
    }

    private void OpenFileLocation(string? filePath, string unavailableMessageResourceKey)
    {
        if (string.IsNullOrWhiteSpace(filePath))
        {
            ShowMessage(
                "Keybind.Message.Title.OpenLocationError",
                unavailableMessageResourceKey);
            return;
        }

        try
        {
            var fullPath = Path.GetFullPath(filePath);
            var directory = Path.GetDirectoryName(fullPath);
            if (string.IsNullOrWhiteSpace(directory) || !Directory.Exists(directory))
            {
                ShowMessage(
                    "Keybind.Message.Title.OpenLocationError",
                    "Keybind.Message.DirectoryNotFound",
                    directory ?? string.Empty);
                return;
            }

            var startInfo = File.Exists(fullPath)
                ? new ProcessStartInfo("explorer.exe", $"/select,\"{fullPath}\"")
                : new ProcessStartInfo(directory);
            startInfo.UseShellExecute = true;
            Process.Start(startInfo);
        }
        catch (Exception exception)
        {
            ShowMessage(
                "Keybind.Message.Title.OpenLocationError",
                "Keybind.Message.OpenDirectoryFailed",
                exception.Message);
        }
    }

    private void ShowMessage(
        string titleResourceKey,
        string messageResourceKey,
        params object?[] arguments)
    {
        _context.Messages.Show(
            Texts[titleResourceKey],
            Texts.Format(messageResourceKey, arguments));
    }

    private void LoadSaveFile(
        string filePath,
        bool showErrors,
        string statusResourceKey,
        params object?[] statusArguments)
    {
        try
        {
            var session = _saveService.Load(filePath);

            RunSynchronizing(() =>
            {
                _session = session;
                _controllerProfileCache = null;
                _keyMouseProfileCache = null;
                _customKeyMouseOptionsByStorageKey.Clear();
                SelectedSaveFilePath = session.FilePath;
                IsPresetSupported = session.IsPresetSupported;
                OnPropertyChanged(nameof(HasSelectedSaveFile));

                UpdateDetectedSelectionForPath(session.FilePath);
                LoadValuesFromSession();
                InitializeLayoutProfileCacheFromUi();
            });

            SetStatus(texts => session.IsPresetSupported
                ? texts.Format(statusResourceKey, statusArguments)
                : texts.Format(
                    "Keybind.Status.PresetUnsupported",
                    texts.Format(statusResourceKey, statusArguments)));
            UpdateSaveState();
        }
        catch (Exception exception)
        {
            _context.Logger.Error("Failed to load the keybind settings file.", exception);

            if (_session is null)
            {
                SelectedSaveFilePath = string.Empty;
                IsPresetSupported = false;
                OnPropertyChanged(nameof(HasSelectedSaveFile));
                SetStatus("Keybind.Status.LoadFailed");
            }

            UpdateSaveState();

            if (showErrors)
            {
                ShowMessage(
                    "Keybind.Message.Title.LoadError",
                    "Keybind.Message.LoadErrorBody");
            }
        }
    }

    private void LoadValuesFromSession()
    {
        if (_session is null)
        {
            return;
        }

        LoadControllerValuesFromSession();
        LoadKeyMouseValuesFromSession();
        SynchronizeModeLinks();
    }

    private void LoadControllerValuesFromSession()
    {
        if (_session is null)
        {
            return;
        }

        RefreshControllerDependentChoices();

        if (_session.IsPresetSupported && _session.PresetOffset is not null)
        {
            var presetValue = _session.Data[_session.PresetOffset.Value];
            var preset = PresetOptions.FirstOrDefault(option => option.Value == presetValue);
            if (preset is null)
            {
                preset = new PresetOption(presetValue, Texts.Format("Keybind.Value.UnknownPreset", presetValue));
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
            LoadControllerActionFromSession(row);
        }
    }

    private void LoadControllerActionFromSession(ControllerActionRowViewModel row)
    {
        if (_session is null)
        {
            return;
        }

        var offset = _saveService.GetControllerOffsets(_session, row.Definition).First();
        var value = _saveService.ReadUInt32(_session, offset);
        var option = row.ButtonOptions.FirstOrDefault(candidate => candidate.Value == value);
        if (option is null)
        {
            var label = KeybindCatalog.GetControllerInputDisplayLabel(
                SelectedControllerType,
                value,
                Texts);

            option = KeybindCatalog.CreateControllerInputOption(
                SelectedControllerType,
                value,
                label,
                Texts);

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

    private void LoadKeyMouseValuesFromSession()
    {
        if (_session is null)
        {
            return;
        }

        RefreshKeyMouseChoices();
        foreach (var row in _allKeyMouseRows)
        {
            LoadKeyMouseActionFromSession(row);
        }
    }

    private void LoadKeyMouseActionFromSession(KeyMouseActionRowViewModel row)
    {
        if (_session is null)
        {
            return;
        }

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
                Texts.Format("Keybind.Value.UnknownKeyMouseInput", inputType, value));
            RegisterCustomKeyMouseOption(option);
            row.EnsureKeyOption(option);
        }

        row.SelectedKey = option;
    }

    private void ResetValues()
    {
        if (_session is null)
        {
            return;
        }

        RunSynchronizing(() =>
        {
            _customKeyMouseOptionsByStorageKey.Clear();
            LoadValuesFromSession();
            InitializeLayoutProfileCacheFromUi();
        });
        SetStatus("Keybind.Status.Reset");
        UpdateSaveState();
    }

    private void LoadLayout()
    {
        var layoutFilePath = GetExistingLayoutFilePath();
        if (layoutFilePath is null)
        {
            ShowMessage(
                "Keybind.Message.Title.LayoutLoadError",
                "Keybind.Message.LayoutFileMissing",
                _layoutFilePath);
            return;
        }

        try
        {
            var layout = ReadLayoutFile(layoutFilePath);

            RunSynchronizing(() =>
            {
                CacheLoadedLayoutProfiles(
                    layout.ControllerProfile ?? new ControllerLayoutProfile(),
                    layout.KeyMouseProfile ?? new KeyMouseLayoutProfile());
                ApplyCachedLayoutForCurrentInput();
                SynchronizeModeLinks();
            });
            CacheCurrentActiveLayoutProfile();

            if (!PathsEqual(layoutFilePath, _layoutFilePath))
            {
                SaveLayoutFile(
                    NormalizeControllerLayoutProfile(_controllerProfileCache),
                    NormalizeKeyMouseLayoutProfile(_keyMouseProfileCache));
            }

            SetStatus("Keybind.Status.LayoutLoaded", Path.GetFileName(_layoutFilePath));
            UpdateSaveState();

            ShowMessage(
                "Keybind.Message.Title.LayoutLoaded",
                "Keybind.Message.LayoutLoadedBody");
        }
        catch (Exception exception)
        {
            _context.Logger.Error("Failed to load the key settings preset.", exception);
            SetStatus("Keybind.Status.LayoutLoadFailed");
            UpdateSaveState();

            ShowMessage(
                "Keybind.Message.Title.LayoutLoadError",
                "Keybind.Message.LayoutLoadFailedBody",
                exception.Message);
        }
    }

    private string? GetExistingLayoutFilePath()
    {
        if (File.Exists(_layoutFilePath))
        {
            return _layoutFilePath;
        }

        return _legacyLayoutFilePaths.FirstOrDefault(File.Exists);
    }

    private KeybindLayoutConfig ReadLayoutFile(string filePath)
    {
        if (PathsEqual(filePath, _layoutFilePath))
        {
            return _context.Settings.Load<KeybindLayoutConfig>(KeybindCatalog.ButtonLayoutFileName);
        }

        var json = File.ReadAllText(filePath);
        return JsonSerializer.Deserialize<KeybindLayoutConfig>(json, JsonOptions)
            ?? throw new InvalidDataException(Texts["Keybind.Error.InvalidLayoutFile"]);
    }


    private void RemoveLegacyLayoutFiles()
    {
        foreach (var legacyPath in _legacyLayoutFilePaths)
        {
            try
            {
                if (File.Exists(legacyPath))
                {
                    File.Delete(legacyPath);
                }
            }
            catch (Exception exception)
            {
                _context.Logger.Warning($"Failed to remove a previous key settings preset. {exception.Message}");
            }
        }
    }

    private void InitializeLayoutProfileCacheFromUi()
    {
        _controllerProfileCache = NormalizeControllerLayoutProfile(CollectControllerLayoutProfile());
        _keyMouseProfileCache = NormalizeKeyMouseLayoutProfile(CollectKeyMouseLayoutProfile());
    }

    private void CacheCurrentActiveLayoutProfile()
    {
        if (_isSynchronizing)
        {
            return;
        }

        if (IsKeyMouseMode)
        {
            _keyMouseProfileCache = NormalizeKeyMouseLayoutProfile(CollectKeyMouseLayoutProfile());
        }
        else
        {
            _controllerProfileCache = NormalizeControllerLayoutProfile(CollectControllerLayoutProfile());
        }
    }

    private void CacheLoadedLayoutProfiles(
        ControllerLayoutProfile controllerProfile,
        KeyMouseLayoutProfile keyMouseProfile)
    {
        _controllerProfileCache = MergeControllerLayoutProfile(_controllerProfileCache, controllerProfile);
        _keyMouseProfileCache = MergeKeyMouseLayoutProfile(_keyMouseProfileCache, keyMouseProfile);
    }

    private void ApplyCachedLayoutForCurrentInput()
    {
        if (IsKeyMouseMode)
        {
            if (_keyMouseProfileCache is not null)
            {
                ApplyKeyMouseLayoutProfile(CloneKeyMouseLayoutProfile(_keyMouseProfileCache));
            }

            return;
        }

        if (_controllerProfileCache is not null)
        {
            ApplyControllerLayoutProfile(CloneControllerLayoutProfile(_controllerProfileCache));
        }
    }

    private ControllerLayoutProfile CollectControllerLayoutProfile()
    {
        return new ControllerLayoutProfile
        {
            ControllerType = SelectedControllerType,
            QuickWheelIndependent = ControllerQuickWheelIndependent,
            PhotoModeIndependent = ControllerPhotoModeIndependent,
            Keybind = new ControllerKeybindProfile
            {
                Helper1 = SelectedHelper1 is null
                    ? null
                    : KeybindCatalog.GetHelperBindingStorageKey(SelectedHelper1.MainValue),
                Helper2 = SelectedHelper2 is null
                    ? null
                    : KeybindCatalog.GetHelperBindingStorageKey(SelectedHelper2.MainValue),
                Preset = SelectedPreset is null
                    ? null
                    : KeybindCatalog.GetPresetStorageKey(SelectedPreset.Value)
            },
            Actions = _allControllerRows.ToDictionary(
                row => row.Definition.Id,
                row => new ControllerActionLayout
                {
                    Helper = row.SelectedHelper is null
                        ? null
                        : KeybindCatalog.GetActionHelperStorageKey(row.SelectedHelper.StateValue),
                    Button = row.SelectedButton is null
                        ? null
                        : KeybindCatalog.GetControllerInputStorageKey(row.SelectedButton.Value)
                },
                StringComparer.Ordinal)
        };
    }

    private KeyMouseLayoutProfile CollectKeyMouseLayoutProfile()
    {
        return new KeyMouseLayoutProfile
        {
            QuickWheelIndependent = KeyMouseQuickWheelIndependent,
            PhotoModeIndependent = KeyMousePhotoModeIndependent,
            FishingModeIndependent = KeyMouseFishingModeIndependent,
            Actions = _allKeyMouseRows.ToDictionary(
                row => row.Definition.Id,
                row => new KeyMouseActionLayout
                {
                    Key = row.SelectedKey is null
                        ? null
                        : KeybindCatalog.GetKeyMouseInputStorageKey(
                            row.SelectedKey.InputType,
                            row.SelectedKey.Value)
                },
                StringComparer.Ordinal)
        };
    }

    private ControllerLayoutProfile NormalizeControllerLayoutProfile(ControllerLayoutProfile? source)
    {
        var normalized = CloneControllerLayoutProfile(source);
        if (!ControllerTypes.Contains(normalized.ControllerType, StringComparer.OrdinalIgnoreCase))
        {
            normalized.ControllerType = KeybindCatalog.DefaultControllerType;
        }

        NormalizeControllerLinkGroup(
            normalized.Actions,
            KeybindCatalog.ControllerQuickWheelActions,
            KeybindCatalog.ControllerQuickWheelLinks,
            normalized.QuickWheelIndependent);
        NormalizeControllerLinkGroup(
            normalized.Actions,
            KeybindCatalog.ControllerPhotoActions,
            KeybindCatalog.ControllerPhotoModeLinks,
            normalized.PhotoModeIndependent);

        return normalized;
    }

    private KeyMouseLayoutProfile NormalizeKeyMouseLayoutProfile(KeyMouseLayoutProfile? source)
    {
        var normalized = CloneKeyMouseLayoutProfile(source);
        NormalizeKeyMouseLinkGroup(
            normalized.Actions,
            KeybindCatalog.KeyMouseQuickWheelActions,
            KeybindCatalog.KeyMouseQuickWheelLinks,
            normalized.QuickWheelIndependent);
        NormalizeKeyMouseLinkGroup(
            normalized.Actions,
            KeybindCatalog.KeyMousePhotoActions,
            KeybindCatalog.KeyMousePhotoModeLinks,
            normalized.PhotoModeIndependent);
        NormalizeKeyMouseLinkGroup(
            normalized.Actions,
            KeybindCatalog.KeyMouseFishingActions,
            KeybindCatalog.KeyMouseFishingModeLinks,
            normalized.FishingModeIndependent);

        return normalized;
    }

    private static void NormalizeControllerLinkGroup(
        IDictionary<string, ControllerActionLayout> actions,
        IEnumerable<ControllerActionDefinition> modeActions,
        IReadOnlyDictionary<string, string> links,
        bool independent)
    {
        if (independent)
        {
            return;
        }

        foreach (var action in modeActions)
        {
            if (!links.TryGetValue(action.Id, out var sourceId)
                || !actions.TryGetValue(sourceId, out var source))
            {
                continue;
            }

            if (!actions.TryGetValue(action.Id, out var target))
            {
                target = new ControllerActionLayout();
                actions[action.Id] = target;
            }

            if (source.Button is not null)
            {
                target.Button = source.Button;
            }

            if (action.UsesHelper && source.Helper is not null)
            {
                target.Helper = source.Helper;
            }
        }
    }

    private static void NormalizeKeyMouseLinkGroup(
        IDictionary<string, KeyMouseActionLayout> actions,
        IEnumerable<KeyMouseActionDefinition> modeActions,
        IReadOnlyDictionary<string, string> links,
        bool independent)
    {
        if (independent)
        {
            return;
        }

        foreach (var action in modeActions)
        {
            if (!links.TryGetValue(action.Id, out var sourceId)
                || !actions.TryGetValue(sourceId, out var source))
            {
                continue;
            }

            if (!actions.TryGetValue(action.Id, out var target))
            {
                target = new KeyMouseActionLayout();
                actions[action.Id] = target;
            }

            if (source.Key is not null)
            {
                target.Key = source.Key;
            }
        }
    }

    private ControllerLayoutProfile MergeControllerLayoutProfile(
        ControllerLayoutProfile? baseline,
        ControllerLayoutProfile overlay)
    {
        var merged = CloneControllerLayoutProfile(baseline);
        if (!string.IsNullOrWhiteSpace(overlay.ControllerType))
        {
            merged.ControllerType = overlay.ControllerType;
        }

        merged.QuickWheelIndependent = overlay.QuickWheelIndependent;
        merged.PhotoModeIndependent = overlay.PhotoModeIndependent;

        if (overlay.Keybind is not null)
        {
            if (KeybindCatalog.TryGetHelperBindingMainValue(overlay.Keybind.Helper1, out _))
            {
                merged.Keybind.Helper1 = overlay.Keybind.Helper1;
            }

            if (KeybindCatalog.TryGetHelperBindingMainValue(overlay.Keybind.Helper2, out _))
            {
                merged.Keybind.Helper2 = overlay.Keybind.Helper2;
            }

            if (KeybindCatalog.TryGetPresetValue(overlay.Keybind.Preset, out _))
            {
                merged.Keybind.Preset = overlay.Keybind.Preset;
            }
        }

        foreach (var definition in KeybindCatalog.ControllerActions)
        {
            if (!TryGetProfileActionEntry(overlay.Actions, definition, out ControllerActionLayout? source)
                || source is null)
            {
                continue;
            }

            if (!merged.Actions.TryGetValue(definition.Id, out var target))
            {
                target = new ControllerActionLayout();
                merged.Actions[definition.Id] = target;
            }

            if (KeybindCatalog.TryGetControllerInputValue(source.Button, out _))
            {
                target.Button = source.Button;
            }

            if (KeybindCatalog.TryGetActionHelperState(source.Helper, out _))
            {
                target.Helper = source.Helper;
            }
        }

        return NormalizeControllerLayoutProfile(merged);
    }

    private KeyMouseLayoutProfile MergeKeyMouseLayoutProfile(
        KeyMouseLayoutProfile? baseline,
        KeyMouseLayoutProfile overlay)
    {
        var merged = CloneKeyMouseLayoutProfile(baseline);
        merged.QuickWheelIndependent = overlay.QuickWheelIndependent;
        merged.PhotoModeIndependent = overlay.PhotoModeIndependent;
        merged.FishingModeIndependent = overlay.FishingModeIndependent;

        foreach (var definition in KeybindCatalog.KeyMouseActions)
        {
            if (!TryGetProfileActionEntry(overlay.Actions, definition, out KeyMouseActionLayout? source)
                || source?.Key is null)
            {
                continue;
            }

            if (!merged.Actions.TryGetValue(definition.Id, out var target))
            {
                target = new KeyMouseActionLayout();
                merged.Actions[definition.Id] = target;
            }

            if (KeybindCatalog.TryGetKeyMouseInputValue(source.Key, out _, out _))
            {
                target.Key = source.Key;
            }
        }

        return NormalizeKeyMouseLayoutProfile(merged);
    }

    private void ApplyControllerLayoutProfile(ControllerLayoutProfile profile)
    {
        var normalized = NormalizeControllerLayoutProfile(profile);
        var controllerType = ControllerTypes.Contains(normalized.ControllerType, StringComparer.OrdinalIgnoreCase)
            ? normalized.ControllerType!
            : KeybindCatalog.DefaultControllerType;

        _controllerQuickWheelIndependent = normalized.QuickWheelIndependent;
        OnPropertyChanged(nameof(ControllerQuickWheelIndependent));
        _controllerPhotoModeIndependent = normalized.PhotoModeIndependent;
        OnPropertyChanged(nameof(ControllerPhotoModeIndependent));

        _selectedControllerType = controllerType;
        OnPropertyChanged(nameof(SelectedControllerType));
        RefreshControllerDependentChoices();

        _selectedHelper1 = KeybindCatalog.TryGetHelperBindingMainValue(
                normalized.Keybind.Helper1,
                out var helper1MainValue)
            ? FindByMainValue(HelperOptions, helper1MainValue)
            : null;
        OnPropertyChanged(nameof(SelectedHelper1));

        _selectedHelper2 = KeybindCatalog.TryGetHelperBindingMainValue(
                normalized.Keybind.Helper2,
                out var helper2MainValue)
            ? FindByMainValue(HelperOptions, helper2MainValue)
            : null;
        OnPropertyChanged(nameof(SelectedHelper2));

        RefreshControllerActionChoices();
        RefreshActionHelperChoices();

        _selectedPreset = KeybindCatalog.TryGetPresetValue(
                normalized.Keybind.Preset,
                out var presetValue)
            ? FindByValue(PresetOptions, presetValue)
            : PresetOptions.FirstOrDefault();
        OnPropertyChanged(nameof(SelectedPreset));

        foreach (var row in _allControllerRows)
        {
            if (!TryGetProfileActionEntry(normalized.Actions, row.Definition, out ControllerActionLayout? saved)
                || saved is null)
            {
                continue;
            }

            if (KeybindCatalog.TryGetActionHelperState(saved.Helper, out var helperState))
            {
                row.SelectHelperState(helperState);
            }

            if (KeybindCatalog.TryGetControllerInputValue(saved.Button, out var buttonValue))
            {
                var button = row.ButtonOptions.FirstOrDefault(option => option.Value == buttonValue);
                if (button is null)
                {
                    var label = KeybindCatalog.GetControllerInputDisplayLabel(
                        SelectedControllerType,
                        buttonValue,
                        Texts);
                    button = KeybindCatalog.CreateControllerInputOption(
                        SelectedControllerType,
                        buttonValue,
                        label,
                        Texts);
                    row.EnsureButtonOption(button);
                }

                row.SelectedButton = button;
            }
        }
    }

    private void ApplyKeyMouseLayoutProfile(KeyMouseLayoutProfile profile)
    {
        var normalized = NormalizeKeyMouseLayoutProfile(profile);

        _keyMouseQuickWheelIndependent = normalized.QuickWheelIndependent;
        OnPropertyChanged(nameof(KeyMouseQuickWheelIndependent));
        _keyMousePhotoModeIndependent = normalized.PhotoModeIndependent;
        OnPropertyChanged(nameof(KeyMousePhotoModeIndependent));
        _keyMouseFishingModeIndependent = normalized.FishingModeIndependent;
        OnPropertyChanged(nameof(KeyMouseFishingModeIndependent));

        foreach (var row in _allKeyMouseRows)
        {
            if (!TryGetProfileActionEntry(normalized.Actions, row.Definition, out KeyMouseActionLayout? saved)
                || string.IsNullOrEmpty(saved?.Key))
            {
                continue;
            }

            if (!KeybindCatalog.TryGetKeyMouseInputValue(
                    saved.Key,
                    out var inputType,
                    out var inputValue))
            {
                continue;
            }

            var key = row.KeyOptions.FirstOrDefault(option =>
                    option.InputType == inputType && option.Value == inputValue)
                ?? ResolveCustomKeyMouseOption(saved.Key);
            if (key is null)
            {
                key = KeybindCatalog.CreateKeyMouseInputOption(
                    inputType,
                    inputValue,
                    Texts.Format("Keybind.Value.UnknownKeyMouseInput", inputType, inputValue));
                RegisterCustomKeyMouseOption(key);
            }

            row.EnsureKeyOption(key);
            row.SelectedKey = key;
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
            CacheCurrentActiveLayoutProfile();
            var controllerProfile = NormalizeControllerLayoutProfile(_controllerProfileCache);
            var keyMouseProfile = NormalizeKeyMouseLayoutProfile(_keyMouseProfileCache);
            var data = _session.Data.ToArray();

            SaveControllerProfile(data, controllerProfile);
            SaveKeyMouseProfile(data, keyMouseProfile);

            _saveService.Save(_session.FilePath, data);
            _session.ReplaceData(data);
            _controllerProfileCache = CloneControllerLayoutProfile(controllerProfile);
            _keyMouseProfileCache = CloneKeyMouseLayoutProfile(keyMouseProfile);
            SaveLayoutFile(controllerProfile, keyMouseProfile);

            SetStatus("Keybind.Status.Saved");
            UpdateSaveState();

            ShowMessage(
                "Keybind.Message.Title.SaveCompleted",
                "Keybind.Message.SaveCompletedBody",
                _layoutFilePath);
        }
        catch (Exception exception)
        {
            _context.Logger.Error("Failed to save keybind settings.", exception);
            SetStatus("Keybind.Status.SaveFailed");
            UpdateSaveState();

            ShowMessage(
                "Keybind.Message.Title.SaveError",
                "Keybind.Message.SaveErrorBody");
        }
    }

    private void SaveControllerProfile(byte[] data, ControllerLayoutProfile profile)
    {
        if (_session is null)
        {
            throw new InvalidOperationException(Texts["Keybind.Error.SaveSessionNotLoaded"]);
        }

        var controllerType = profile.ControllerType;
        if (!ControllerTypes.Contains(controllerType, StringComparer.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(Texts["Keybind.Error.InvalidControllerType"]);
        }

        if (!KeybindCatalog.TryGetHelperBindingMainValue(profile.Keybind.Helper1, out var helper1Value))
        {
            throw new InvalidOperationException(Texts["Keybind.Error.InvalidHelper1"]);
        }

        if (!KeybindCatalog.TryGetHelperBindingMainValue(profile.Keybind.Helper2, out var helper2Value))
        {
            throw new InvalidOperationException(Texts["Keybind.Error.InvalidHelper2"]);
        }

        uint presetValue = 0x01u;
        if (_session.IsPresetSupported
            && !KeybindCatalog.TryGetPresetValue(profile.Keybind.Preset, out presetValue))
        {
            throw new InvalidOperationException(Texts["Keybind.Error.InvalidPreset"]);
        }

        if (_session.IsPresetSupported && _session.PresetOffset is not null)
        {
            data[_session.PresetOffset.Value] = checked((byte)presetValue);
        }

        _saveService.WriteUInt32(data, _session.Helper1Offset, helper1Value);
        _saveService.WriteUInt32(data, _session.Helper2Offset, helper2Value);

        foreach (var definition in KeybindCatalog.ControllerActions)
        {
            if (!profile.Actions.TryGetValue(definition.Id, out var saved)
                || !KeybindCatalog.TryGetControllerInputValue(saved.Button, out var value))
            {
                throw new InvalidOperationException(Texts.Format(
                    "Keybind.Error.InvalidActionValue",
                    Texts[KeybindCatalog.GetActionLocalizationKey(definition)]));
            }

            uint state;
            if (definition.UsesHelper)
            {
                if (!KeybindCatalog.TryGetActionHelperState(saved.Helper, out state))
                {
                    throw new InvalidOperationException(Texts.Format(
                        "Keybind.Error.InvalidActionHelper",
                        Texts[KeybindCatalog.GetActionLocalizationKey(definition)]));
                }
            }
            else
            {
                state = KeybindCatalog.ActionStateSingle;
            }

            foreach (var offset in _saveService.GetWritableControllerOffsets(_session, definition))
            {
                _saveService.WriteUInt32(data, offset, value);
                _saveService.WriteUInt32(data, offset + sizeof(uint), state);
            }
        }
    }

    private void SaveKeyMouseProfile(byte[] data, KeyMouseLayoutProfile profile)
    {
        if (_session is null)
        {
            throw new InvalidOperationException(Texts["Keybind.Error.SaveSessionNotLoaded"]);
        }

        foreach (var definition in KeybindCatalog.KeyMouseActions)
        {
            if (!profile.Actions.TryGetValue(definition.Id, out var saved)
                || string.IsNullOrEmpty(saved.Key)
                || !TryResolveKeyMouseOption(saved.Key, out var option))
            {
                throw new InvalidOperationException(Texts.Format(
                    "Keybind.Error.InvalidActionKey",
                    Texts[KeybindCatalog.GetActionLocalizationKey(definition)]));
            }

            foreach (var offset in _saveService.GetWritableKeyMouseOffsets(_session, definition))
            {
                _saveService.WriteUInt32(data, offset - sizeof(uint), option.InputType);
                _saveService.WriteUInt32(data, offset, option.Value);
            }
        }
    }

    private void SaveLayoutFile(
        ControllerLayoutProfile controllerProfile,
        KeyMouseLayoutProfile keyMouseProfile)
    {
        var layout = new KeybindLayoutConfig
        {
            Version = 7,
            InputDevice = IsKeyMouseMode
                ? KeybindCatalog.InputDeviceKeyMouse
                : KeybindCatalog.InputDeviceController,
            ControllerProfile = CloneControllerLayoutProfile(controllerProfile),
            KeyMouseProfile = CloneKeyMouseLayoutProfile(keyMouseProfile)
        };

        _context.Settings.Save(KeybindCatalog.ButtonLayoutFileName, layout);
        RemoveLegacyLayoutFiles();
    }

    private bool HasBlankRequiredFields()
    {
        if (_session is null)
        {
            return true;
        }

        CacheCurrentActiveLayoutProfile();
        return !IsControllerProfileComplete(_controllerProfileCache)
            || !IsKeyMouseProfileComplete(_keyMouseProfileCache);
    }

    private bool IsControllerProfileComplete(ControllerLayoutProfile? profile)
    {
        if (profile is null
            || string.IsNullOrWhiteSpace(profile.Keybind.Helper1)
            || string.IsNullOrWhiteSpace(profile.Keybind.Helper2)
            || string.IsNullOrWhiteSpace(profile.Keybind.Preset))
        {
            return false;
        }

        foreach (var definition in KeybindCatalog.ControllerActions)
        {
            if (!profile.Actions.TryGetValue(definition.Id, out var saved)
                || string.IsNullOrWhiteSpace(saved.Button)
                || (definition.UsesHelper && string.IsNullOrWhiteSpace(saved.Helper)))
            {
                return false;
            }
        }

        return true;
    }

    private static bool IsKeyMouseProfileComplete(KeyMouseLayoutProfile? profile)
    {
        if (profile is null)
        {
            return false;
        }

        return KeybindCatalog.KeyMouseActions.All(definition =>
            profile.Actions.TryGetValue(definition.Id, out var saved)
            && !string.IsNullOrWhiteSpace(saved.Key));
    }

    private void UpdateDetectedSelectionForPath(string filePath)
    {
        var matching = DetectedSaveFiles.FirstOrDefault(item => PathsEqual(item.FilePath, filePath));

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
        CanSave = !HasBlankRequiredFields();

        SaveCommand.RaiseCanExecuteChanged();
        ResetCommand.RaiseCanExecuteChanged();
        OpenSelectedFileLocationCommand.RaiseCanExecuteChanged();
    }

    private void RegisterCustomKeyMouseOption(KeyMouseInputOption option)
    {
        if (!KeybindCatalog.KeyMouseInputOptions.Any(known =>
                known.InputType == option.InputType && known.Value == option.Value))
        {
            _customKeyMouseOptionsByStorageKey[
                KeybindCatalog.GetKeyMouseInputStorageKey(option.InputType, option.Value)] = option;
        }
    }

    private KeyMouseInputOption? ResolveCustomKeyMouseOption(string storageKey)
    {
        return _customKeyMouseOptionsByStorageKey.TryGetValue(storageKey, out var option)
            ? option
            : null;
    }

    private bool TryResolveKeyMouseOption(string storageKey, out KeyMouseInputOption option)
    {
        if (!KeybindCatalog.TryGetKeyMouseInputValue(storageKey, out var inputType, out var value))
        {
            option = null!;
            return false;
        }

        var known = KeybindCatalog.CreateKnownKeyMouseInputOption(
            inputType,
            value,
            Texts);
        if (known is not null)
        {
            option = known;
            return true;
        }

        var custom = ResolveCustomKeyMouseOption(storageKey);
        if (custom is not null)
        {
            option = custom;
            return true;
        }

        option = null!;
        return false;
    }

    private static bool TryGetProfileActionEntry<TActionLayout>(
        IReadOnlyDictionary<string, TActionLayout>? actions,
        IKeybindActionDefinition definition,
        out TActionLayout? layout)
        where TActionLayout : class
    {
        layout = null;
        return actions is not null
            && actions.TryGetValue(definition.Id, out layout)
            && layout is not null;
    }

    private static ControllerLayoutProfile CloneControllerLayoutProfile(ControllerLayoutProfile? source)
    {
        source ??= new ControllerLayoutProfile();
        return new ControllerLayoutProfile
        {
            ControllerType = source.ControllerType,
            QuickWheelIndependent = source.QuickWheelIndependent,
            PhotoModeIndependent = source.PhotoModeIndependent,
            Keybind = new ControllerKeybindProfile
            {
                Helper1 = source.Keybind?.Helper1,
                Helper2 = source.Keybind?.Helper2,
                Preset = source.Keybind?.Preset
            },
            Actions = source.Actions?.ToDictionary(
                pair => pair.Key,
                pair => new ControllerActionLayout
                {
                    Helper = pair.Value?.Helper,
                    Button = pair.Value?.Button
                },
                StringComparer.Ordinal)
                ?? new Dictionary<string, ControllerActionLayout>(StringComparer.Ordinal)
        };
    }

    private static KeyMouseLayoutProfile CloneKeyMouseLayoutProfile(KeyMouseLayoutProfile? source)
    {
        source ??= new KeyMouseLayoutProfile();
        return new KeyMouseLayoutProfile
        {
            QuickWheelIndependent = source.QuickWheelIndependent,
            PhotoModeIndependent = source.PhotoModeIndependent,
            FishingModeIndependent = source.FishingModeIndependent,
            Actions = source.Actions?.ToDictionary(
                pair => pair.Key,
                pair => new KeyMouseActionLayout
                {
                    Key = pair.Value?.Key
                },
                StringComparer.Ordinal)
                ?? new Dictionary<string, KeyMouseActionLayout>(StringComparer.Ordinal)
        };
    }

    private void Texts_CultureChanged(object? sender, EventArgs e)
    {
        RunSynchronizing(() =>
        {
            RefreshActionDisplayNames();
            RefreshControllerDependentChoices();
            RefreshKeyMouseChoices();
            SynchronizeModeLinks();
        });

        RefreshStatusText();
    }

    private void RefreshActionDisplayNames()
    {
        foreach (var row in _allControllerRows)
        {
            row.RefreshDisplayName(Texts[KeybindCatalog.GetActionLocalizationKey(row.Definition)]);
        }

        foreach (var row in _allKeyMouseRows)
        {
            row.RefreshDisplayName(Texts[KeybindCatalog.GetActionLocalizationKey(row.Definition)]);
        }
    }

    private void SetStatus(string resourceKey, params object?[] arguments)
    {
        SetStatus(texts => texts.Format(resourceKey, arguments));
    }

    private void SetStatus(Func<PluginLocalizer, string> formatter)
    {
        ArgumentNullException.ThrowIfNull(formatter);

        _statusTextFormatter = formatter;
        StatusText = formatter(Texts);
    }

    private void RefreshStatusText()
    {
        if (_statusTextFormatter is not null)
        {
            StatusText = _statusTextFormatter(Texts);
            return;
        }

        SetStatus("Keybind.Status.NoFileSelected");
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
