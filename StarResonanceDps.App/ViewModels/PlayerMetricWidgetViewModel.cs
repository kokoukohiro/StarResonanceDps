using System.Collections.ObjectModel;
using System.Globalization;
using System.Windows.Threading;
using StarResonanceDps.App.Config;
using StarResonanceDps.App.Localization;
using StarResonanceDps.App.Models.Widgets;
using StarResonanceDps.Core.CombatRuntime;
using StarResonanceDps.Core.Models;

namespace StarResonanceDps.App.ViewModels;

public enum PlayerMetricDisplayMode
{
    Contribution,
    Timeline
}

public sealed class PlayerMetricWidgetViewModel : PlayerWidgetWindowViewModel, IDisposable
{
    private readonly MeterSnapshotKind _kind;
    private readonly PlayerMetricDisplayMode _displayMode;
    private readonly ConfigManager _configManager = ConfigManager.Instance;
    private readonly DispatcherTimer _refreshTimer;
    private readonly ObservableCollection<MetricSkillTableEntry> _skillEntries = [];
    private IReadOnlyList<MetricTimelinePoint> _timelinePoints = Array.Empty<MetricTimelinePoint>();
    private string _metricLabel = string.Empty;
    private string _totalLabel = string.Empty;
    private string _totalValueText = string.Empty;
    private string _latestValueText = string.Empty;
    private string _noDataText = string.Empty;
    private bool _hasMetricData;
    private bool _isBenchmarkUiFrozen;
    private bool _isDisposed;

    public PlayerMetricWidgetViewModel(
        WidgetListItemViewModel playerWidget,
        long? requestedCharacterId,
        PlayerRosterEntry? initialPlayer,
        MeterSnapshotKind kind,
        PlayerMetricDisplayMode displayMode)
        : base(playerWidget, requestedCharacterId)
    {
        _kind = kind;
        _displayMode = displayMode;
        SkillEntries = new ReadOnlyObservableCollection<MetricSkillTableEntry>(_skillEntries);
        _refreshTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(250)
        };
        _refreshTimer.Tick += RefreshTimer_Tick;
        _configManager.SettingsPreviewChanged += ConfigManager_SettingsPreviewChanged;
        LocalizationManager.Instance.CultureChanged += LocalizationManager_CultureChanged;
        InitializePlayer(initialPlayer);
        Refresh();
        _refreshTimer.Start();
    }

    public bool IsContribution => _displayMode == PlayerMetricDisplayMode.Contribution;

    public bool IsTimeline => _displayMode == PlayerMetricDisplayMode.Timeline;

    public IReadOnlyList<MetricTimelinePoint> TimelinePoints
    {
        get => _timelinePoints;
        private set => SetProperty(ref _timelinePoints, value);
    }

    public ReadOnlyObservableCollection<MetricSkillTableEntry> SkillEntries { get; }

    public string MetricLabel
    {
        get => _metricLabel;
        private set => SetProperty(ref _metricLabel, value);
    }

    public string TotalLabel
    {
        get => _totalLabel;
        private set => SetProperty(ref _totalLabel, value);
    }

    public string TotalValueText
    {
        get => _totalValueText;
        private set => SetProperty(ref _totalValueText, value);
    }

    public string LatestValueText
    {
        get => _latestValueText;
        private set => SetProperty(ref _latestValueText, value);
    }

    public string NoDataText
    {
        get => _noDataText;
        private set => SetProperty(ref _noDataText, value);
    }

    public bool HasMetricData
    {
        get => _hasMetricData;
        private set => SetProperty(ref _hasMetricData, value);
    }

    public string TotalValueHeader => LocalizationManager.Instance.GetString(
        _kind == MeterSnapshotKind.Damage
            ? "Metric_Damage"
            : "Metric_Healing");

    public string ActivePerSecondHeader => LocalizationManager.Instance.GetString(
        _kind == MeterSnapshotKind.Damage
            ? "Metric_ActiveDps"
            : "Metric_ActiveHps");

    public string EncounterPerSecondHeader => LocalizationManager.Instance.GetString(
        _kind == MeterSnapshotKind.Damage
            ? "Metric_EncounterDps"
            : "Metric_EncounterHps");

    public string ShareHeader => LocalizationManager.Instance.GetString(
        _kind == MeterSnapshotKind.Damage
            ? "Metric_DamageShare"
            : "Metric_HealingShare");

    public void Dispose()
    {
        if (_isDisposed)
        {
            return;
        }

        _isDisposed = true;
        _refreshTimer.Stop();
        _refreshTimer.Tick -= RefreshTimer_Tick;
        _configManager.SettingsPreviewChanged -= ConfigManager_SettingsPreviewChanged;
        LocalizationManager.Instance.CultureChanged -= LocalizationManager_CultureChanged;
    }

    protected override void OnSelectedPlayerChanged(PlayerRosterEntry? player)
    {
        Refresh();
    }

    private void RefreshTimer_Tick(object? sender, EventArgs e)
    {
        Refresh();
    }

    private void ConfigManager_SettingsPreviewChanged(object? sender, EventArgs e)
    {
        Refresh();
    }

    private void LocalizationManager_CultureChanged(object? sender, EventArgs e)
    {
        OnPropertyChanged(nameof(TotalValueHeader));
        OnPropertyChanged(nameof(ActivePerSecondHeader));
        OnPropertyChanged(nameof(EncounterPerSecondHeader));
        OnPropertyChanged(nameof(ShareHeader));
        Refresh();
    }

    private void Refresh()
    {
        if (_isDisposed)
        {
            return;
        }

        var benchmarkState = MeterSnapshotProvider.GetBenchmarkState();
        if (benchmarkState.IsCompleted && _isBenchmarkUiFrozen)
        {
            return;
        }

        _isBenchmarkUiFrozen = benchmarkState.IsCompleted;

        var numberDisplayFormatIndex = _configManager.GetSettingsSnapshot().NumberDisplayFormatIndex;
        MetricLabel = _displayMode == PlayerMetricDisplayMode.Timeline
            ? LocalizationManager.Instance.GetString(_kind == MeterSnapshotKind.Damage ? "Metric_InstantDps" : "Metric_InstantHps")
            : (_kind == MeterSnapshotKind.Damage ? "DPS" : "HPS");
        TotalLabel = $"{LocalizationManager.Instance.GetString("Meter_Total")}:";
        NoDataText = LocalizationManager.Instance.GetString("Widget_NoMetricData");

        if (SelectedCharacterId is not { } characterId)
        {
            ApplyEmptyData(numberDisplayFormatIndex);
            return;
        }

        var playerIdentity = MeterSnapshotProvider.GetPlayerIdentity(characterId);
        if (playerIdentity is not null)
        {
            SetHeaderText(playerIdentity.Name, playerIdentity.UserId);
        }

        if (IsTimeline)
        {
            var aggregationIntervalSeconds = PlayerWidget
                .GetMetricTimelineSettingsSnapshot()
                .AggregationIntervalSeconds;
            var timeline = MeterSnapshotProvider.GetPlayerTimeline(
                _kind,
                characterId,
                aggregationIntervalSeconds);
            TimelinePoints = timeline.Points;
            ClearSkillEntries();
            TotalValueText = MeterNumberFormatter.Format(timeline.TotalValue, numberDisplayFormatIndex);
            LatestValueText = timeline.Points.Count == 0
                ? string.Empty
                : MeterNumberFormatter.Format(timeline.Points[^1].ValuePerSecond, numberDisplayFormatIndex);
            HasMetricData = timeline.Points.Count > 0;
            return;
        }

        var table = MeterSnapshotProvider.GetPlayerSkillTable(_kind, characterId);
        TimelinePoints = Array.Empty<MetricTimelinePoint>();
        TotalValueText = MeterNumberFormatter.Format(table.TotalValue, numberDisplayFormatIndex);
        LatestValueText = string.Empty;
        SynchronizeSkillEntries(table.Entries, numberDisplayFormatIndex);
        HasMetricData = table.Entries.Count > 0;
    }

    private void ApplyEmptyData(int numberDisplayFormatIndex)
    {
        TimelinePoints = Array.Empty<MetricTimelinePoint>();
        ClearSkillEntries();
        TotalValueText = MeterNumberFormatter.Format(0UL, numberDisplayFormatIndex);
        LatestValueText = string.Empty;
        HasMetricData = false;
    }

    private void SynchronizeSkillEntries(
        IReadOnlyList<MetricSkillTableRowSnapshot> entries,
        int numberDisplayFormatIndex)
    {
        _skillEntries.Clear();

        foreach (var entry in entries)
        {
            _skillEntries.Add(new MetricSkillTableEntry(
                entry.SkillId.ToString(CultureInfo.CurrentCulture),
                entry.Name,
                MeterNumberFormatter.Format(entry.TotalValue, numberDisplayFormatIndex),
                MeterNumberFormatter.Format(entry.ValuePerSecondActive, numberDisplayFormatIndex),
                MeterNumberFormatter.Format(entry.ValuePerSecond, numberDisplayFormatIndex),
                entry.HitCount.ToString(CultureInfo.CurrentCulture),
                entry.CritRate.ToString(CultureInfo.CurrentCulture) + "%",
                MeterNumberFormatter.Format(entry.AverageValue, numberDisplayFormatIndex),
                entry.Percentage.ToString(CultureInfo.CurrentCulture) + "%"));
        }
    }

    private void ClearSkillEntries()
    {
        if (_skillEntries.Count > 0)
        {
            _skillEntries.Clear();
        }
    }
}
