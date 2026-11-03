using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Globalization;
using System.Numerics;
using System.Windows.Threading;
using StarResonanceDps.PluginSdk;
using StarResonanceDps.Plugins.ModuleTool.Models;
using StarResonanceDps.Plugins.ModuleTool.Services;

namespace StarResonanceDps.Plugins.ModuleTool.ViewModels;

internal sealed class ModuleToolViewModel : ObservableObject, IDisposable
{
    private const string ConfigFileName = "module_config.json";
    private const string ResourceBaseName = "StarResonanceDps.Plugins.ModuleTool.Properties.Resources";
    private const int ResultCount = 10;
    private const int NoSpecId = 0;
    private static readonly TimeSpan ElapsedRefreshInterval = TimeSpan.FromMilliseconds(100);

    private readonly IPluginContext _context;
    private readonly ModuleCatalog _catalog;
    private readonly PluginLocalizer _texts;
    private readonly ModuleToolConfig _config;
    private readonly Dictionary<int, int> _effectIndexById;
    private readonly DispatcherTimer _elapsedTimer;
    private readonly Stopwatch _stopwatch = new();
    private readonly HashSet<long> _reportedUnknownModules = [];

    private PluginModuleInventory? _inventory;
    private IReadOnlyList<ModuleCandidate> _usableModules = [];
    private int _missingLinkModuleCount;
    private int _unknownModuleCount;
    private DateTime? _receivedAt;
    private ModuleCombination? _equipped;
    private IReadOnlyList<ModuleCombination>? _results;
    private double _resultElapsedSeconds;
    private CancellationTokenSource? _calculation;
    private double _progress;
    private bool _isStale;
    private bool _hasReportedSaveFailure;

    private IReadOnlyList<ModuleOption<int>> _specOptions = [];
    private ModuleOption<int>? _selectedSpec;
    private IReadOnlyList<ModuleEffectOption> _addEffectOptions = [];

    public ModuleToolViewModel(IPluginContext context, ModuleCatalog catalog)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
        _catalog = catalog ?? throw new ArgumentNullException(nameof(catalog));
        _texts = new PluginLocalizer(_context.Localization, typeof(ModuleToolViewModel).Assembly, ResourceBaseName);
        _texts.CultureChanged += Texts_CultureChanged;
        _config = _context.Settings.Load<ModuleToolConfig>(ConfigFileName);
        _effectIndexById = _catalog.Effects
            .Select((effect, index) => (effect.EffectId, index))
            .ToDictionary(pair => pair.EffectId, pair => pair.index);

        _elapsedTimer = new DispatcherTimer { Interval = ElapsedRefreshInterval };
        _elapsedTimer.Tick += (_, _) => OnPropertyChanged(nameof(CalculationNoteText));

        CalculateCommand = new RelayCommand(StartOrCancelCalculation, CanStartOrCancelCalculation);

        foreach (var priority in _config.PriorityEffects)
        {
            if (_effectIndexById.ContainsKey(priority.EffectId) && PriorityEffects.All(row => row.EffectId != priority.EffectId))
            {
                PriorityEffects.Add(CreatePriorityRow(priority.EffectId, priority.MinLevel));
            }
            else
            {
                _context.Logger.Warning($"Ignored a saved priority effect {priority.EffectId} that is unknown or duplicated.");
            }
        }

        foreach (var quality in _catalog.Qualities)
        {
            QualityToggles.Add(new ModuleQualityToggleViewModel(
                quality,
                GetQualityLabel(quality),
                !_config.ExcludedQualities.Contains(quality),
                OnQualitiesChanged));
        }

        RebuildOptions();
        _context.GameData.SelfModulesChanged += GameData_SelfModulesChanged;
        ApplyInventory(_context.GameData.SelfModules, isUpdate: false);
    }

    public PluginLocalizer Texts => _texts;

    public RelayCommand CalculateCommand { get; }

    public ObservableCollection<ModulePriorityEffectRowViewModel> PriorityEffects { get; } = [];

    public ObservableCollection<ModuleQualityToggleViewModel> QualityToggles { get; } = [];

    public ObservableCollection<ModuleResultCardViewModel> Cards { get; } = [];

    public bool IsInventoryLoaded => _inventory is not null;

    public string InventorySummaryText => _inventory is null
        ? string.Empty
        : _texts.Format("Module.Inventory.Summary", _inventory.Modules.Count, _inventory.EquippedUuidBySlot.Count, _catalog.SlotCount);

    public string EquippedFightText => _equipped is not null
        ? _texts.Format("Module.Inventory.EquippedFight", FormatNumber(_equipped.FightValue))
        : _texts["Module.Inventory.EquippedFightUnavailable"];

    public bool HasEquippedFight => _inventory is not null && _inventory.EquippedUuidBySlot.Count > 0;

    public string ReceivedAtText => _receivedAt is { } receivedAt
        ? _texts.Format("Module.Inventory.ReceivedAt", receivedAt.ToString("HH:mm", _texts.CurrentCulture))
        : string.Empty;

    public bool HasReceivedAt => _receivedAt is not null && _inventory is not null;

    public string MissingLinksText => _texts.Format("Module.Inventory.MissingLinks", _missingLinkModuleCount);

    public bool HasMissingLinks => _inventory is not null && _missingLinkModuleCount > 0;

    public string UnknownModulesText => _texts.Format("Module.Inventory.UnknownModules", _unknownModuleCount);

    public bool HasUnknownModules => _inventory is not null && _unknownModuleCount > 0;

    public IReadOnlyList<ModuleOption<int>> SpecOptions
    {
        get => _specOptions;
        private set => SetProperty(ref _specOptions, value);
    }

    /// <summary>おすすめ。特化を選ぶと、優先する効果をその特化のおすすめの効果に置き換える。</summary>
    public ModuleOption<int>? SelectedSpec
    {
        get => _selectedSpec;
        set
        {
            if (value is null || !SetProperty(ref _selectedSpec, value))
            {
                return;
            }

            _config.RecommendSpecId = value.Value;
            if (value.Value != NoSpecId)
            {
                PriorityEffects.Clear();
                foreach (var effectId in _catalog.GetRecommendedEffects(value.Value))
                {
                    PriorityEffects.Add(CreatePriorityRow(effectId, 0));
                }

                RebuildAddEffectOptions();
            }

            OnConditionsChanged();
        }
    }

    public IReadOnlyList<ModuleEffectOption> AddEffectOptions
    {
        get => _addEffectOptions;
        private set => SetProperty(ref _addEffectOptions, value);
    }

    public bool CanAddEffect => _addEffectOptions.Count > 0;

    public bool IsCalculating => _calculation is not null;

    public string CalculateButtonText => IsCalculating ? _texts["Module.Button.Cancel"] : _texts["Module.Button.Calculate"];

    public string CalculationNoteText
    {
        get
        {
            if (IsCalculating)
            {
                return _texts.Format(
                    "Module.Calculate.Progress",
                    Math.Floor(_progress * 100).ToString("0", _texts.CurrentCulture),
                    _stopwatch.Elapsed.TotalSeconds.ToString("0.0", _texts.CurrentCulture));
            }

            if (_inventory is null)
            {
                return string.Empty;
            }

            var candidates = GetCandidates();
            var size = Math.Min(_catalog.SlotCount, candidates.Count);
            return _texts.Format("Module.Calculate.Count", size, FormatNumber(CountCombinations(candidates.Count, size)));
        }
    }

    public bool HasResults => _results is not null;

    public string ResultsSummaryText => _results switch
    {
        null => string.Empty,
        { Count: 0 } => _texts["Module.Results.Empty"],
        _ => _texts.Format(
            "Module.Results.Summary",
            _results.Count,
            _resultElapsedSeconds.ToString("0.0", _texts.CurrentCulture)),
    };

    public bool IsStale
    {
        get => _isStale;
        private set => SetProperty(ref _isStale, value);
    }

    /// <summary>効果を追加する選択リストで選ばれた効果を、優先する効果の最後に足す。</summary>
    public void AddPriorityEffect(int effectId)
    {
        if (PriorityEffects.Any(row => row.EffectId == effectId))
        {
            return;
        }

        PriorityEffects.Add(CreatePriorityRow(effectId, 0));
        RebuildAddEffectOptions();
        OnConditionsChanged();
    }

    /// <summary>窓を閉じたとき。本体の知らせの購読を外し、計算を止める。</summary>
    public void Dispose()
    {
        _context.GameData.SelfModulesChanged -= GameData_SelfModulesChanged;
        _texts.CultureChanged -= Texts_CultureChanged;
        _texts.Dispose();
        _elapsedTimer.Stop();
        _calculation?.Cancel();
    }

    private void GameData_SelfModulesChanged(object? sender, EventArgs e)
    {
        ApplyInventory(_context.GameData.SelfModules, isUpdate: true);
    }

    private void Texts_CultureChanged(object? sender, EventArgs e)
    {
        foreach (var toggle in QualityToggles)
        {
            toggle.Label = GetQualityLabel(toggle.Quality);
        }

        var levelOptions = CreateLevelOptions();
        foreach (var row in PriorityEffects)
        {
            row.Relabel(_catalog.GetEffectName(row.EffectId, _texts.CurrentCulture), levelOptions);
        }

        RebuildOptions();
        RebuildCards();
        RaiseInventoryChanged();
        RaiseCalculationChanged();
        OnPropertyChanged(nameof(ResultsSummaryText));
    }

    private void ApplyInventory(PluginModuleInventory? inventory, bool isUpdate)
    {
        _inventory = inventory;
        _receivedAt = isUpdate && inventory is not null ? DateTime.Now : null;
        _missingLinkModuleCount = 0;
        _unknownModuleCount = 0;

        var usable = new List<ModuleCandidate>();
        foreach (var module in inventory?.Modules ?? [])
        {
            if (module.Effects.Any(effect => effect.LinkCount is null))
            {
                _missingLinkModuleCount++;
                continue;
            }

            if (!_catalog.TryGetModule(module.ConfigId, out _)
                || module.Effects.Any(effect => !_effectIndexById.ContainsKey(effect.EffectId)))
            {
                _unknownModuleCount++;
                if (_reportedUnknownModules.Add(module.Uuid))
                {
                    _context.Logger.Warning(
                        $"Module {module.Uuid} (config {module.ConfigId}, effects {string.Join(",", module.Effects.Select(effect => effect.EffectId))}) is not in the module tables; it is excluded.");
                }

                continue;
            }

            usable.Add(new ModuleCandidate(
                module.Uuid,
                module.ConfigId,
                module.Effects.Select(effect => (_effectIndexById[effect.EffectId], effect.LinkCount!.Value)).ToArray()));
        }

        _usableModules = usable;
        _equipped = CreateEquippedCombination();
        if (isUpdate && _results is not null)
        {
            IsStale = true;
        }

        RebuildCards();
        RaiseInventoryChanged();
        RaiseCalculationChanged();
    }

    private ModuleCombination? CreateEquippedCombination()
    {
        if (_inventory is null || _inventory.EquippedUuidBySlot.Count == 0)
        {
            return null;
        }

        var byUuid = _usableModules.ToDictionary(module => module.Uuid);
        var modules = new List<ModuleCandidate>();
        foreach (var (_, uuid) in _inventory.EquippedUuidBySlot.OrderBy(pair => pair.Key))
        {
            if (!byUuid.TryGetValue(uuid, out var module))
            {
                return null;
            }

            modules.Add(module);
        }

        var scorer = CreateScorer();
        var links = SumLinks(modules);
        var totalLinks = modules.Sum(module => module.TotalLinks);
        return new ModuleCombination(scorer.GetFightValue(links, totalLinks), totalLinks, modules);
    }

    private IReadOnlyList<ModuleCandidate> GetCandidates()
    {
        var excluded = QualityToggles.Where(toggle => !toggle.IsSelected).Select(toggle => toggle.Quality).ToHashSet();
        return _usableModules
            .Where(module => _catalog.TryGetModule(module.ConfigId, out var definition) && !excluded.Contains(definition.Quality))
            .ToArray();
    }

    private ModuleScorer CreateScorer()
    {
        return new ModuleScorer(
            _catalog.Effects,
            _catalog.GetLinkFightValues(_context.GameData.CurrentServer),
            PriorityEffects.Select(row => new ModulePriorityEffect(row.EffectId, row.MinLevel)).ToArray());
    }

    private bool CanStartOrCancelCalculation()
    {
        return IsCalculating || (_inventory is not null && GetCandidates().Count > 0);
    }

    private async void StartOrCancelCalculation()
    {
        if (_calculation is not null)
        {
            _calculation.Cancel();
            return;
        }

        var candidates = GetCandidates();
        var equippedUuids = _inventory?.EquippedUuidBySlot.Values.ToHashSet() ?? [];
        using var calculation = new CancellationTokenSource();
        _calculation = calculation;
        _progress = 0;
        _stopwatch.Restart();
        _elapsedTimer.Start();
        RaiseCalculationChanged();

        var progress = new Progress<double>(value =>
        {
            _progress = value;
            OnPropertyChanged(nameof(CalculationNoteText));
        });

        try
        {
            var scorer = CreateScorer();
            var results = await Task.Run(
                () => new ModuleSolver(scorer, candidates, equippedUuids, _catalog.SlotCount, ResultCount)
                    .Solve(progress, calculation.Token),
                calculation.Token);
            _stopwatch.Stop();
            _results = results;
            _resultElapsedSeconds = _stopwatch.Elapsed.TotalSeconds;
            IsStale = false;
            RebuildCards();
            OnPropertyChanged(nameof(HasResults));
            OnPropertyChanged(nameof(ResultsSummaryText));
        }
        catch (OperationCanceledException) when (calculation.IsCancellationRequested)
        {
            // 中断。前の結果はそのまま残す。
        }
        catch (Exception exception)
        {
            _context.Logger.Error("Module combination calculation failed.", exception);
            _context.Messages.Show(
                _texts["Module.Message.CalculationError.Title"],
                _texts["Module.Message.CalculationError.Message"],
                _texts["Module.Message.ErrorDetail"]);
        }
        finally
        {
            _stopwatch.Stop();
            _elapsedTimer.Stop();
            _calculation = null;
            RaiseCalculationChanged();
        }
    }

    private void OnConditionsChanged()
    {
        if (_results is not null)
        {
            IsStale = true;
        }

        SaveConfig();
        _equipped = CreateEquippedCombination();
        RebuildCards();
        RaiseCalculationChanged();
    }

    private void OnQualitiesChanged()
    {
        OnConditionsChanged();
    }

    private void RemovePriorityEffect(ModulePriorityEffectRowViewModel row)
    {
        if (PriorityEffects.Remove(row))
        {
            RebuildAddEffectOptions();
            OnConditionsChanged();
        }
    }

    private ModulePriorityEffectRowViewModel CreatePriorityRow(int effectId, int minLevel)
    {
        return new ModulePriorityEffectRowViewModel(
            effectId,
            _catalog.GetEffectName(effectId, _texts.CurrentCulture),
            _catalog.GetImage(_catalog.Effects[_effectIndexById[effectId]].Icon),
            CreateLevelOptions(),
            minLevel,
            OnConditionsChanged,
            RemovePriorityEffect);
    }

    // 「指定なし」は入っていること(Lv1以上)が条件なので、Lv1以上は並べない。
    private IReadOnlyList<ModuleOption<int>> CreateLevelOptions()
    {
        var maxLevel = _catalog.Effects.Max(effect => effect.MaxLevel);
        return Enumerable.Range(0, maxLevel + 1)
            .Where(level => level != 1)
            .Select(level => new ModuleOption<int>(
                level,
                level == 0 ? _texts["Module.Level.Any"] : _texts.Format("Module.Level.AtLeast", level)))
            .ToArray();
    }

    private void RebuildOptions()
    {
        var specOptions = new List<ModuleOption<int>> { new(NoSpecId, _texts["Module.Recommend.None"]) };
        specOptions.AddRange(_catalog.RecommendedSpecIds.Select(specId =>
            new ModuleOption<int>(specId, _catalog.GetSpecName(specId, _texts.CurrentCulture))));
        SpecOptions = specOptions;
        _selectedSpec = specOptions.FirstOrDefault(option => option.Value == _config.RecommendSpecId) ?? specOptions[0];
        OnPropertyChanged(nameof(SelectedSpec));

        RebuildAddEffectOptions();
    }

    private void RebuildAddEffectOptions()
    {
        AddEffectOptions = _catalog.Effects
            .Where(effect => PriorityEffects.All(row => row.EffectId != effect.EffectId))
            .Select(effect => new ModuleEffectOption(
                effect.EffectId,
                _catalog.GetEffectName(effect.EffectId, _texts.CurrentCulture),
                _catalog.GetImage(effect.Icon)))
            .ToArray();
        OnPropertyChanged(nameof(CanAddEffect));
    }

    private void RebuildCards()
    {
        Cards.Clear();
        if (_inventory is null)
        {
            return;
        }

        var scorer = CreateScorer();
        var priorityOrder = PriorityEffects
            .Select((row, index) => (row.EffectId, index))
            .ToDictionary(pair => _effectIndexById[pair.EffectId], pair => pair.index);
        var equippedUuids = _inventory.EquippedUuidBySlot.Values.ToHashSet();

        if (_equipped is not null)
        {
            Cards.Add(CreateCard(_texts["Module.Card.Equipped"], true, _equipped, scorer, priorityOrder, equippedUuids));
        }

        var rank = 1;
        foreach (var result in _results ?? [])
        {
            Cards.Add(CreateCard(_texts.Format("Module.Card.Rank", rank++), false, result, scorer, priorityOrder, equippedUuids));
        }
    }

    private ModuleResultCardViewModel CreateCard(
        string title,
        bool isEquippedCard,
        ModuleCombination combination,
        ModuleScorer scorer,
        IReadOnlyDictionary<int, int> priorityOrder,
        IReadOnlySet<long> equippedUuids)
    {
        var culture = _texts.CurrentCulture;
        var links = SumLinks(combination.Modules);
        var chips = Enumerable.Range(0, links.Length)
            .Where(effectIndex => links[effectIndex] > 0)
            .Select(effectIndex => (EffectIndex: effectIndex, Links: links[effectIndex], Level: scorer.GetLevel(effectIndex, links[effectIndex])))
            .OrderBy(effect => priorityOrder.TryGetValue(effect.EffectIndex, out var order) ? order : int.MaxValue)
            .ThenByDescending(effect => effect.Level)
            .ThenByDescending(effect => effect.Links)
            .ThenBy(effect => _catalog.Effects[effect.EffectIndex].EffectId)
            .Select(effect =>
            {
                var definition = _catalog.Effects[effect.EffectIndex];
                return new ModuleEffectChipViewModel(
                    _catalog.GetImage(definition.Icon),
                    _catalog.GetEffectName(definition.EffectId, culture),
                    effect.Links,
                    effect.Level,
                    priorityOrder.ContainsKey(effect.EffectIndex));
            })
            .ToArray();

        var tiles = combination.Modules
            .Select(module =>
            {
                _catalog.TryGetModule(module.ConfigId, out var definition);
                return new ModuleTileViewModel(
                    _catalog.GetQualityImage(definition.Quality),
                    _catalog.GetImage(definition.Icon),
                    _catalog.GetModuleName(module.ConfigId, culture),
                    equippedUuids.Contains(module.Uuid),
                    module.Effects
                        .Select(effect =>
                        {
                            var effectDefinition = _catalog.Effects[effect.EffectIndex];
                            return new ModulePartViewModel(
                                _catalog.GetImage(effectDefinition.Icon),
                                _catalog.GetEffectName(effectDefinition.EffectId, culture),
                                effect.Links);
                        })
                        .ToArray());
            })
            .ToArray();

        var delta = !isEquippedCard && _equipped is not null ? combination.FightValue - _equipped.FightValue : (int?)null;
        return new ModuleResultCardViewModel(
            isEquippedCard,
            title,
            _texts["Module.Card.FightValue"],
            FormatNumber(combination.FightValue),
            delta is { } value ? FormatDelta(value) : null,
            delta ?? 0,
            _texts.Format("Module.Card.LinkTotal", combination.TotalLinks),
            chips,
            tiles);
    }

    private int[] SumLinks(IEnumerable<ModuleCandidate> modules)
    {
        var links = new int[_catalog.Effects.Count];
        foreach (var module in modules)
        {
            foreach (var (effectIndex, effectLinks) in module.Effects)
            {
                links[effectIndex] += effectLinks;
            }
        }

        return links;
    }

    private void SaveConfig()
    {
        _config.PriorityEffects = PriorityEffects
            .Select(row => new ModulePriorityEffectConfig { EffectId = row.EffectId, MinLevel = row.MinLevel })
            .ToList();
        _config.ExcludedQualities = QualityToggles.Where(toggle => !toggle.IsSelected).Select(toggle => toggle.Quality).ToList();

        try
        {
            _context.Settings.Save(ConfigFileName, _config);
        }
        catch (Exception exception)
        {
            _context.Logger.Error("Failed to save the module tool settings.", exception);
            if (!_hasReportedSaveFailure)
            {
                _hasReportedSaveFailure = true;
                _context.Messages.Show(
                    _texts["Module.Message.SaveError.Title"],
                    _texts["Module.Message.SaveError.Message"],
                    _texts["Module.Message.ErrorDetail"]);
            }
        }
    }

    private string GetQualityLabel(int quality)
    {
        return _texts[string.Create(CultureInfo.InvariantCulture, $"Module.Quality.{quality}")];
    }

    private string FormatNumber(long value)
    {
        return value.ToString("N0", _texts.CurrentCulture);
    }

    private string FormatNumber(BigInteger value)
    {
        return value.ToString("N0", _texts.CurrentCulture);
    }

    private string FormatDelta(int delta)
    {
        return delta switch
        {
            > 0 => "+" + FormatNumber(delta),
            < 0 => "-" + FormatNumber(-delta),
            _ => "±0",
        };
    }

    private static BigInteger CountCombinations(int count, int size)
    {
        BigInteger result = BigInteger.One;
        for (var index = 0; index < size; index++)
        {
            result = result * (count - index) / (index + 1);
        }

        return result;
    }

    private void RaiseInventoryChanged()
    {
        OnPropertyChanged(nameof(IsInventoryLoaded));
        OnPropertyChanged(nameof(InventorySummaryText));
        OnPropertyChanged(nameof(EquippedFightText));
        OnPropertyChanged(nameof(HasEquippedFight));
        OnPropertyChanged(nameof(ReceivedAtText));
        OnPropertyChanged(nameof(HasReceivedAt));
        OnPropertyChanged(nameof(MissingLinksText));
        OnPropertyChanged(nameof(HasMissingLinks));
        OnPropertyChanged(nameof(UnknownModulesText));
        OnPropertyChanged(nameof(HasUnknownModules));
    }

    private void RaiseCalculationChanged()
    {
        OnPropertyChanged(nameof(IsCalculating));
        OnPropertyChanged(nameof(CalculateButtonText));
        OnPropertyChanged(nameof(CalculationNoteText));
        CalculateCommand.RaiseCanExecuteChanged();
    }
}
