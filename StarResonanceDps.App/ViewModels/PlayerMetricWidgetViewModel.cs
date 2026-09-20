using System.Collections.ObjectModel;
using System.Globalization;
using System.Windows;
using System.Windows.Media;
using System.Windows.Threading;
using StarResonanceDps.App.Config;
using StarResonanceDps.App.Localization;
using StarResonanceDps.App.Models.Widgets;
using StarResonanceDps.App.Services;
using StarResonanceDps.Core.CombatRuntime;
using StarResonanceDps.Core.Models;

namespace StarResonanceDps.App.ViewModels;

public enum PlayerMetricDisplayMode
{
    Contribution,
    Timeline
}

/// <summary>
/// スキル詳細の属性列に出すアイコン。TIPS は属性名。
/// 被ダメログの属性アイコンと同じで、<b>影は付けない</b>。
/// </summary>
/// <param name="Widget">TIPS の色を取る窓のパレットの持ち主。</param>
public sealed record MetricElementIconSegment(ImageSource Icon, string Name, WidgetListItemViewModel Widget);

public sealed class PlayerMetricWidgetViewModel : PlayerWidgetWindowViewModel, IDisposable
{
    /// <summary>タイプ列の並び(ユーザー決定)。enum の値の順ではない。</summary>
    private static readonly Zproto.EDamageMode[] DamageModeDisplayOrder =
    [
        Zproto.EDamageMode.DamagePhysical,
        Zproto.EDamageMode.DamageMagical,
        Zproto.EDamageMode.DamageNormal
    ];

    /// <summary>物理でも魔法でもないときの表記。記号なので4言語とも同じで、リソースは持たない。</summary>
    private const string NoDamageModeText = "――";

    private readonly MeterSnapshotKind _kind;
    private readonly PlayerMetricDisplayMode _displayMode;
    private ElementColorWidgetSettingsConfig _elementColorSettings;
    private readonly ConfigManager _configManager = ConfigManager.Instance;
    private readonly DispatcherTimer _refreshTimer;
    private readonly ObservableCollection<MetricSkillTableEntry> _skillEntries = [];
    private readonly Dictionary<long, MetricSkillTableEntry> _skillEntriesBySkillId = [];
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
        _elementColorSettings = playerWidget.GetElementColorSettingsSnapshot();
        SkillEntries = new ReadOnlyObservableCollection<MetricSkillTableEntry>(_skillEntries);
        _refreshTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(250)
        };
        _refreshTimer.Tick += RefreshTimer_Tick;
        _configManager.SettingsPreviewChanged += ConfigManager_SettingsPreviewChanged;
        playerWidget.ElementColorSettingsChanged += Widget_ElementColorSettingsChanged;
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
        PlayerWidget.ElementColorSettingsChanged -= Widget_ElementColorSettingsChanged;
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

    /// <summary>属性カラーの設定(保存かプレビュー)が変わった。行のバーの色を作り直す。</summary>
    private void Widget_ElementColorSettingsChanged(object? sender, EventArgs e)
    {
        _elementColorSettings = PlayerWidget.GetElementColorSettingsSnapshot();
        Refresh();
    }

    private void LocalizationManager_CultureChanged(object? sender, EventArgs e)
    {
        OnPropertyChanged(nameof(TotalValueHeader));
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

    /// <summary>
    /// 行を作り直さず使い回す(メーターの行と同じ)。毎回作り直すと行の UI 要素ごと作り直され、
    /// バーのアニメーションが更新のたびに 0 から始まってしまう。
    /// </summary>
    private void SynchronizeSkillEntries(
        IReadOnlyList<MetricSkillTableRowSnapshot> entries,
        int numberDisplayFormatIndex)
    {
        var activeSkillIds = entries
            .Select(entry => entry.SkillId)
            .ToHashSet();

        for (var index = _skillEntries.Count - 1; index >= 0; index--)
        {
            var skillId = _skillEntries[index].SkillId;
            if (activeSkillIds.Contains(skillId))
            {
                continue;
            }

            _skillEntriesBySkillId.Remove(skillId);
            _skillEntries.RemoveAt(index);
        }

        for (var index = 0; index < entries.Count; index++)
        {
            var entry = entries[index];
            if (!_skillEntriesBySkillId.TryGetValue(entry.SkillId, out var item))
            {
                item = new MetricSkillTableEntry(entry.SkillId);
                ApplySkillEntry(item, entry, index, numberDisplayFormatIndex);
                _skillEntriesBySkillId.Add(entry.SkillId, item);
                _skillEntries.Insert(index, item);
                continue;
            }

            ApplySkillEntry(item, entry, index, numberDisplayFormatIndex);
            if (_skillEntries[index].SkillId == entry.SkillId)
            {
                continue;
            }

            var currentIndex = FindSkillEntryIndex(entry.SkillId);
            if (currentIndex >= 0)
            {
                _skillEntries.Move(currentIndex, index);
            }
        }
    }

    private void ApplySkillEntry(
        MetricSkillTableEntry item,
        MetricSkillTableRowSnapshot entry,
        int index,
        int numberDisplayFormatIndex)
    {
        item.Update(
            // 並びは総量の降順なので、そのまま振るとメーターの順位と同じ形になる。
            (index + 1).ToString(CultureInfo.CurrentCulture),
            CreateElementSegments(entry),
            CreateDamageModeText(entry),
            entry.Name,
            MeterNumberFormatter.Format(entry.TotalValue, numberDisplayFormatIndex),
            MeterNumberFormatter.Format(entry.ValuePerSecond, numberDisplayFormatIndex),
            entry.HitCount.ToString(CultureInfo.CurrentCulture),
            entry.CritRate.ToString("F2", CultureInfo.CurrentCulture) + "%",
            entry.Percentage.ToString("F2", CultureInfo.CurrentCulture) + "%",
            CreateBarBrush(entry),
            Math.Clamp(entry.Percentage / 100d, 0d, 1d));
    }

    private int FindSkillEntryIndex(long skillId)
    {
        for (var index = 0; index < _skillEntries.Count; index++)
        {
            if (_skillEntries[index].SkillId == skillId)
            {
                return index;
            }
        }

        return -1;
    }

    /// <summary>
    /// 行のバーの塗り。属性ごとの色を<b>その行の割合で混ぜ</b>、フィルターと不透明度を掛ける。
    ///
    /// <para>
    /// <b>内訳が1件も無い行は <c>null</c></b> を返す(バーを出さない)。
    /// 内訳を溜める前に保存した履歴がこれに当たる。色を決める材料が無いので、
    /// それらしい色で埋めると内訳が無いことが見えなくなる。
    /// </para>
    /// </summary>
    private Brush? CreateBarBrush(MetricSkillTableRowSnapshot entry)
    {
        if (entry.TotalValue == 0UL || entry.ValueByElement.Count == 0)
        {
            return null;
        }

        var parts = new List<(Color Color, double Weight)>();
        foreach (var pair in entry.ValueByElement)
        {
            if (pair.Value == 0UL)
            {
                continue;
            }

            parts.Add((ResolveElementColor(pair.Key), pair.Value));
        }

        if (!ColorUtilities.TryBlendWeighted(parts, out var blended))
        {
            return null;
        }

        var filtered = ClassColorFilter.Apply(blended, _elementColorSettings);
        var opacity = Math.Clamp(
            _elementColorSettings.ColorOpacity,
            WidgetConfigDefaults.MinClassColorOpacity,
            WidgetConfigDefaults.MaxClassColorOpacity);

        var brush = new SolidColorBrush(Color.FromArgb(
            (byte)Math.Round(opacity / 100d * byte.MaxValue, MidpointRounding.AwayFromZero),
            filtered.R,
            filtered.G,
            filtered.B));
        brush.Freeze();
        return brush;
    }

    private Color ResolveElementColor(Zproto.EDamageProperty element)
    {
        var key = element.ToString();
        var defaults = WidgetConfigDefaults.CreateDefaultElementColors(key);
        var palette = _elementColorSettings.ColorPalettes.TryGetValue(key, out var colors) && colors.Count > 0
            ? colors
            : defaults;
        var selectedIndex = _elementColorSettings.ColorIndexes.TryGetValue(key, out var index)
            ? index
            : WidgetConfigDefaults.MinClassColorIndex + 1;

        var hex = palette[Math.Clamp(selectedIndex, 0, palette.Count - 1)];
        return ColorUtilities.TryParseHex(hex, out var color)
            ? color
            : Colors.Gray;
    }

    /// <summary>
    /// 属性の内訳。アイコンと割合を属性ID順に並べる。
    /// <b>割合が 0 の属性は出さない</b>ので、混ざっていない行はアイコン1つと 100.00% になる。
    /// </summary>
    private IReadOnlyList<object> CreateElementSegments(MetricSkillTableRowSnapshot entry)
    {
        if (entry.TotalValue == 0UL || entry.ValueByElement.Count == 0)
        {
            return [];
        }

        var localization = LocalizationManager.Instance;
        var segments = new List<object>();
        foreach (var pair in entry.ValueByElement)
        {
            if (pair.Value == 0UL)
            {
                continue;
            }

            if (segments.Count > 0)
            {
                segments.Add(" ");
            }

            segments.Add(new MetricElementIconSegment(
                (ImageSource)Application.Current.FindResource($"Icon.DamageProperty.{pair.Key}"),
                localization.GetString($"DamageProperty_{pair.Key}"),
                PlayerWidget));
            segments.Add(FormatShare(pair.Value, entry.TotalValue));
        }

        return segments;
    }

    /// <summary>
    /// 物理・魔法の内訳。<b>並びは 物理 → 魔法 → ――</b> で、割合が 0 のものは出さない。
    /// 「――」は <c>DamageNormal</c>(物理でも魔法でもない)で、記号なのでリソースを持たない。
    /// </summary>
    private static string CreateDamageModeText(MetricSkillTableRowSnapshot entry)
    {
        if (entry.TotalValue == 0UL || entry.ValueByMode.Count == 0)
        {
            return string.Empty;
        }

        var localization = LocalizationManager.Instance;
        var parts = new List<string>();
        foreach (var mode in DamageModeDisplayOrder)
        {
            var value = 0UL;
            foreach (var pair in entry.ValueByMode)
            {
                if (pair.Key == mode)
                {
                    value = pair.Value;
                    break;
                }
            }

            if (value == 0UL)
            {
                continue;
            }

            var label = mode switch
            {
                Zproto.EDamageMode.DamagePhysical => localization.GetString("Metric_DamageMode_Physical"),
                Zproto.EDamageMode.DamageMagical => localization.GetString("Metric_DamageMode_Magical"),
                _ => NoDamageModeText
            };

            parts.Add(label + FormatShare(value, entry.TotalValue));
        }

        return string.Join(" ", parts);
    }

    private static string FormatShare(ulong value, ulong total)
    {
        return ((double)value / total * 100d).ToString("F2", CultureInfo.CurrentCulture) + "%";
    }

    private void ClearSkillEntries()
    {
        _skillEntriesBySkillId.Clear();

        if (_skillEntries.Count > 0)
        {
            _skillEntries.Clear();
        }
    }
}
