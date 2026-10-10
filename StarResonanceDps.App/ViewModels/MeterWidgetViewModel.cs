using System.Collections.ObjectModel;
using System.Globalization;
using System.Windows.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using StarResonanceDps.App.Config;
using StarResonanceDps.App.Diagnostics;
using StarResonanceDps.App.Localization;
using StarResonanceDps.App.Models.Widgets;
using StarResonanceDps.Core.CombatRuntime;
using StarResonanceDps.Core.Models;

namespace StarResonanceDps.App.ViewModels;

public sealed partial class MeterWidgetViewModel : ViewModelBase, IDisposable
{
    private readonly WidgetListItemViewModel _widget;
    private readonly MeterSnapshotKind _kind;
    private readonly ObservableCollection<MeterPlayerEntry> _entries = [];
    private readonly Dictionary<long, MeterPlayerEntry> _entriesByCharacterId = [];
    private readonly DispatcherTimer _refreshTimer;
    private readonly ConfigManager _configManager = ConfigManager.Instance;
    private readonly Action<WidgetKind, long> _requestPlayerWindow;

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
    private string _benchmarkActionText = string.Empty;

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
        HistorySwitchProbe.Register(this, _widget.Kind.ToString());
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
        HistorySwitchProbe.Unregister(this);
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
        var probe = HistorySwitchProbe.BeginRefresh(this);

        // 計測の完了後も描き直してよい。時計が窓の終わりで止まり、窓の後は記録されない(Core)。
        var benchmarkState = MeterSnapshotProvider.GetBenchmarkState();
        var settings = _widget.GetMeterSettingsSnapshot();
        var globalSettings = _configManager.GetSettingsSnapshot();
        var numberDisplayFormatIndex = globalSettings.NumberDisplayFormatIndex;
        var playerNameDisplayMode = (PlayerNameDisplayMode)globalSettings.PlayerNameDisplayModeIndex;
        var snapshot = MeterSnapshotProvider.GetSnapshot(
            _kind,
            (PartyDisplayMode)settings.PartyDisplayModeIndex);
        probe?.DataDone();
        var rowsBefore = _entries.Count;

        ElapsedText = FormatDuration(snapshot.Duration);
        IsBenchmarkActive = benchmarkState.IsActive;
        BenchmarkStatusText = LocalizationManager.Instance.GetString(
            benchmarkState.IsCompleted
                ? "Meter_BenchmarkCompleted"
                : "Meter_BenchmarkInProgress");
        BenchmarkActionText = LocalizationManager.Instance.GetString(
            benchmarkState.IsActive
                ? "Meter_StopBenchmark"
                : "Meter_Benchmark");
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

        probe?.End($"players={snapshot.Players.Count} rows={rowsBefore}->{_entries.Count}");
    }

    [RelayCommand]
    private void ToggleBenchmark()
    {
        MeterSnapshotProvider.ToggleBenchmark();
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

    /// <summary>
    /// メーターのタイマーの書式。被ダメログの時刻も同じ書式で出す。
    /// 秒は床関数で丸め、負は符号を付けて出す(起点より前の被ダメログの行。-0.4 秒は -00:00:01)。
    /// </summary>
    internal static string FormatDuration(TimeSpan duration)
    {
        var totalSeconds = (long)Math.Floor(duration.TotalSeconds);
        var sign = totalSeconds < 0 ? "-" : string.Empty;
        var magnitude = Math.Abs(totalSeconds);
        var hours = magnitude / 3600;
        var minutes = magnitude / 60 % 60;
        var seconds = magnitude % 60;

        return hours >= 100
            ? string.Create(CultureInfo.InvariantCulture, $"{sign}{hours:000}:{minutes:00}:{seconds:00}")
            : string.Create(CultureInfo.InvariantCulture, $"{sign}{hours:00}:{minutes:00}:{seconds:00}");
    }
}
