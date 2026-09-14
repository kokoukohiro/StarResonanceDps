using System.Collections.ObjectModel;
using System.Windows.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using StarResonanceDps.App.Config;
using StarResonanceDps.App.Localization;
using StarResonanceDps.App.Models.Widgets;
using StarResonanceDps.Core.CombatRuntime;
using StarResonanceDps.Core.Models;

namespace StarResonanceDps.App.ViewModels;

public sealed partial class MeterWidgetViewModel : ViewModelBase, IDisposable
{
    private const int ThreeMinuteBenchmarkDurationSeconds = 180;

    private readonly WidgetListItemViewModel _widget;
    private readonly MeterSnapshotKind _kind;
    private readonly ObservableCollection<MeterPlayerEntry> _entries = [];
    private readonly Dictionary<long, MeterPlayerEntry> _entriesByCharacterId = [];
    private readonly DispatcherTimer _refreshTimer;
    private readonly ConfigManager _configManager = ConfigManager.Instance;
    private readonly Action<WidgetKind, long> _requestPlayerWindow;
    private bool _isBenchmarkUiFrozen;

    [ObservableProperty]
    private string _elapsedText = "00:00:00";

    [ObservableProperty]
    private string _partyMetricLabel = string.Empty;

    [ObservableProperty]
    private string _partyMetricValueText = string.Empty;

    [ObservableProperty]
    private string _totalLabel = string.Empty;

    [ObservableProperty]
    private string _totalValueText = string.Empty;

    [ObservableProperty]
    private bool _isBenchmarkActive;

    [ObservableProperty]
    private string _benchmarkStatusText = string.Empty;

    /// <summary>ヘッダーのボタンに出す文言。計測中は「計測停止」に変わる。</summary>
    [ObservableProperty]
    private string _threeMinuteBenchmarkActionText = string.Empty;

    public MeterWidgetViewModel(
        WidgetListItemViewModel widget,
        MeterSnapshotKind kind,
        Action<WidgetKind, long> requestPlayerWindow)
    {
        _widget = widget;
        _kind = kind;
        _requestPlayerWindow = requestPlayerWindow;
        Entries = new ReadOnlyObservableCollection<MeterPlayerEntry>(_entries);
        _refreshTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(250)
        };
        _refreshTimer.Tick += RefreshTimer_Tick;
        _widget.MeterSettingsChanged += Widget_MeterSettingsChanged;
        _configManager.SettingsPreviewChanged += ConfigManager_SettingsPreviewChanged;
        LocalizationManager.Instance.CultureChanged += LocalizationManager_CultureChanged;
        Refresh();
        _refreshTimer.Start();
    }

    public WidgetListItemViewModel Widget => _widget;

    public ReadOnlyObservableCollection<MeterPlayerEntry> Entries { get; }

    public void Dispose()
    {
        _refreshTimer.Stop();
        _refreshTimer.Tick -= RefreshTimer_Tick;
        _widget.MeterSettingsChanged -= Widget_MeterSettingsChanged;
        _configManager.SettingsPreviewChanged -= ConfigManager_SettingsPreviewChanged;
        LocalizationManager.Instance.CultureChanged -= LocalizationManager_CultureChanged;
    }

    public string ContributionMenuText => LocalizationManager.Instance.GetString(
        _kind == MeterSnapshotKind.Damage
            ? "Widget_DamageSkillDetails"
            : "Widget_HealingSkillDetails");

    public string SummaryMenuText => LocalizationManager.Instance.GetString(
        _kind == MeterSnapshotKind.Damage
            ? "Widget_DamageContribution"
            : "Widget_HealingContribution");

    public string TimelineMenuText => LocalizationManager.Instance.GetString(
        _kind == MeterSnapshotKind.Damage
            ? "Widget_DpsGraph"
            : "Widget_HpsGraph");

    private void RefreshTimer_Tick(object? sender, EventArgs e)
    {
        Refresh();
    }

    private void Widget_MeterSettingsChanged(object? sender, EventArgs e)
    {
        Refresh();
    }

    private void ConfigManager_SettingsPreviewChanged(object? sender, EventArgs e)
    {
        Refresh();
    }

    private void LocalizationManager_CultureChanged(object? sender, EventArgs e)
    {
        OnPropertyChanged(nameof(ContributionMenuText));
        OnPropertyChanged(nameof(SummaryMenuText));
        OnPropertyChanged(nameof(TimelineMenuText));
        Refresh();
    }

    private void Refresh()
    {
        var benchmarkState = MeterSnapshotProvider.GetBenchmarkState();
        if (benchmarkState.IsCompleted && _isBenchmarkUiFrozen)
        {
            return;
        }

        _isBenchmarkUiFrozen = benchmarkState.IsCompleted;

        var settings = _widget.GetMeterSettingsSnapshot();
        var globalSettings = _configManager.GetSettingsSnapshot();
        var numberDisplayFormatIndex = globalSettings.NumberDisplayFormatIndex;
        var playerNameDisplayMode = (PlayerNameDisplayMode)globalSettings.PlayerNameDisplayModeIndex;
        var snapshot = MeterSnapshotProvider.GetSnapshot(
            _kind,
            (PartyDisplayMode)settings.PartyDisplayModeIndex);

        ElapsedText = benchmarkState.IsActive && !benchmarkState.HasBegun
            ? "00:00:00"
            : FormatDuration(snapshot.Duration);
        IsBenchmarkActive = benchmarkState.IsActive;
        BenchmarkStatusText = LocalizationManager.Instance.GetString(
            benchmarkState.IsCompleted
                ? "Meter_BenchmarkCompleted"
                : "Meter_BenchmarkInProgress");
        ThreeMinuteBenchmarkActionText = LocalizationManager.Instance.GetString(
            benchmarkState.IsActive
                ? "Meter_StopBenchmark"
                : "Meter_ThreeMinuteBenchmark");
        PartyMetricLabel = _kind == MeterSnapshotKind.Damage ? "DPS:" : "HPS:";
        PartyMetricValueText = MeterNumberFormatter.Format(snapshot.ValuePerSecond, numberDisplayFormatIndex);
        TotalLabel = $"{LocalizationManager.Instance.GetString("Meter_Total")}:";
        TotalValueText = MeterNumberFormatter.Format(snapshot.TotalValue, numberDisplayFormatIndex);

        var activeCharacterIds = snapshot.Players
            .Select(player => player.CharacterId)
            .ToHashSet();

        for (var index = _entries.Count - 1; index >= 0; index--)
        {
            var entry = _entries[index];
            if (activeCharacterIds.Contains(entry.CharacterId))
            {
                continue;
            }

            entry.IsPlayerSelectionMenuOpen = false;
            _entriesByCharacterId.Remove(entry.CharacterId);
            _entries.RemoveAt(index);
        }

        for (var index = 0; index < snapshot.Players.Count; index++)
        {
            var player = snapshot.Players[index];
            if (!_entriesByCharacterId.TryGetValue(player.CharacterId, out var entry))
            {
                entry = MeterPlayerEntry.Create(player, index + 1, settings, _widget.Kind, numberDisplayFormatIndex, playerNameDisplayMode);
                _entriesByCharacterId.Add(player.CharacterId, entry);
                _entries.Insert(index, entry);
                continue;
            }

            entry.Update(player, index + 1, settings, _widget.Kind, numberDisplayFormatIndex, playerNameDisplayMode);
            if (_entries[index].CharacterId == player.CharacterId)
            {
                continue;
            }

            var currentIndex = FindEntryIndex(player.CharacterId, index + 1);
            if (currentIndex >= 0)
            {
                _entries.Move(currentIndex, index);
            }
        }
    }

    [RelayCommand]
    private void ToggleThreeMinuteBenchmark()
    {
        var benchmarkState = MeterSnapshotProvider.GetBenchmarkState();
        if (benchmarkState.IsActive)
        {
            MeterSnapshotProvider.TryStopBenchmark();
        }
        else
        {
            MeterSnapshotProvider.TryStartBenchmark(ThreeMinuteBenchmarkDurationSeconds);
        }

        Refresh();
    }

    [RelayCommand]
    private void ResetEncounter()
    {
        MeterSnapshotProvider.ResetCurrentEncounter();
    }

    [RelayCommand]
    private void RequestContribution(MeterPlayerEntry? player)
    {
        RequestPlayerWindow(
            _kind == MeterSnapshotKind.Damage
                ? WidgetKind.DamageContribution
                : WidgetKind.HealingContribution,
            player);
    }

    [RelayCommand]
    private void RequestSummary(MeterPlayerEntry? player)
    {
        RequestPlayerWindow(
            _kind == MeterSnapshotKind.Damage
                ? WidgetKind.DamageSummary
                : WidgetKind.HealingSummary,
            player);
    }

    [RelayCommand]
    private void RequestTimeline(MeterPlayerEntry? player)
    {
        RequestPlayerWindow(
            _kind == MeterSnapshotKind.Damage
                ? WidgetKind.DpsGraph
                : WidgetKind.HpsGraph,
            player);
    }

    private void RequestPlayerWindow(WidgetKind widgetKind, MeterPlayerEntry? player)
    {
        if (player is not null)
        {
            _requestPlayerWindow(widgetKind, player.PlayerId);
        }
    }

    private int FindEntryIndex(long characterId, int startIndex)
    {
        for (var index = startIndex; index < _entries.Count; index++)
        {
            if (_entries[index].CharacterId == characterId)
            {
                return index;
            }
        }

        return -1;
    }

    /// <summary>メーターのタイマーの書式。被ダメログの時刻も同じ書式で出す。</summary>
    internal static string FormatDuration(TimeSpan duration)
    {
        if (duration < TimeSpan.Zero)
        {
            duration = TimeSpan.Zero;
        }

        return duration.TotalHours >= 100d
            ? $"{(int)duration.TotalHours:000}:{duration.Minutes:00}:{duration.Seconds:00}"
            : duration.ToString(@"hh\:mm\:ss");
    }
}
