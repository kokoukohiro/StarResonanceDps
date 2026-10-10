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
/// スキル詳細の属性列に出すアイコン。クラスアイコンと同じ染め方で、塗りは白(行の色は下のバーが持つ)。
/// TIPS は欄そのものに1つ付けるので、アイコンは持たない。
///
/// <para>
/// 2種類以上の属性を含む行は、枠を中央の縦線で2等分し、
/// <b>ダメージの多い1位を左、2位を右</b>に切り抜いて重ねる。3つ目以降は出さない(TIPS には出る)。
/// </para>
/// </summary>
/// <param name="PrimaryMask">1位の属性アイコンの形。2位があるときは左半分に切り抜く。</param>
/// <param name="SecondaryMask">2位の属性アイコンの形。無ければ <c>null</c>。</param>
public sealed record MetricElementIconSegment(Brush PrimaryMask, Brush? SecondaryMask)
{
    /// <summary>2位があるか。切り抜きと2枚目の出し分けに使う。</summary>
    public bool HasSecondary => SecondaryMask is not null;
}

public sealed class PlayerMetricWidgetViewModel : PlayerWidgetWindowViewModel, IDisposable
{
    /// <summary>タイプ列の並び(ユーザー決定)。enum の値の順ではない。</summary>
    private static readonly Zproto.EDamageMode[] DamageModeDisplayOrder =
    [
        Zproto.EDamageMode.DamagePhysical,
        Zproto.EDamageMode.DamageMagical,
        Zproto.EDamageMode.DamageNormal
    ];

    /// <summary>属性・種類の並びと、その TIPS の区切り。記号なので4言語とも同じ。</summary>
    private const string ShareSeparator = "/";

    /// <summary>行の TIPS で、属性の内訳とタイプの内訳を分ける区切り。</summary>
    private const string ToolTipPartSeparator = ", ";

    /// <summary>
    /// ホイールが止まったとみなすまでの待ち。最後に回してからこの時間何も来なければ横軸の長さを保存する。
    /// 窓の位置と大きさの保存(<c>WidgetWindow</c> の保存待ち)と同じ値。
    /// </summary>
    private static readonly TimeSpan WheelSaveDelay = TimeSpan.FromMilliseconds(300);

    private readonly MeterSnapshotKind _kind;
    private readonly PlayerMetricDisplayMode _displayMode;
    private ElementColorWidgetSettingsConfig _elementColorSettings;
    private SkillDetailWidgetSettingsConfig _skillDetailSettings;
    private readonly ConfigManager _configManager = ConfigManager.Instance;
    private readonly DispatcherTimer _refreshTimer;
    private readonly ObservableCollection<MetricSkillTableEntry> _skillEntries = [];
    private readonly Dictionary<long, MetricSkillTableEntry> _skillEntriesBySkillId = [];
    private readonly Dictionary<string, ImageBrush> _elementIconMasks = new(StringComparer.Ordinal);
    private IReadOnlyList<MetricTimelinePoint> _timelinePoints = Array.Empty<MetricTimelinePoint>();
    private IReadOnlyList<MetricTimelineSkillMarker> _timelineSkillMarkers = Array.Empty<MetricTimelineSkillMarker>();
    private int _timelineVisibleSeconds = WidgetConfigDefaults.DefaultMetricTimelineVisibleSeconds;
    private bool _timelineShowsSkillLog = true;
    private Brush? _timelineLineBrush;
    private readonly DispatcherTimer _wheelSaveTimer;

    /// <summary>ホイールで決めて、まだ保存していない横軸の長さ。保存待ちが無ければ null。</summary>
    private int? _pendingWheelVisibleSeconds;
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
        _skillDetailSettings = playerWidget.GetSkillDetailSettingsSnapshot();
        SkillEntries = new ReadOnlyObservableCollection<MetricSkillTableEntry>(_skillEntries);
        _refreshTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(250)
        };
        _refreshTimer.Tick += RefreshTimer_Tick;
        _wheelSaveTimer = new DispatcherTimer
        {
            Interval = WheelSaveDelay
        };
        _wheelSaveTimer.Tick += WheelSaveTimer_Tick;
        _configManager.SettingsPreviewChanged += ConfigManager_SettingsPreviewChanged;
        playerWidget.ElementColorSettingsChanged += Widget_ElementColorSettingsChanged;
        playerWidget.SkillDetailSettingsChanged += Widget_SkillDetailSettingsChanged;
        LocalizationManager.Instance.CultureChanged += LocalizationManager_CultureChanged;
        InitializePlayer(initialPlayer);
        Refresh();
        _refreshTimer.Start();
    }

    public bool IsContribution => _displayMode == PlayerMetricDisplayMode.Contribution;

    public bool IsTimeline => _displayMode == PlayerMetricDisplayMode.Timeline;

    /// <summary>
    /// 推移グラフの点。値が前回と同じなら差し替えない(毎回新しい配列を入れると、値が同じでもグラフが線と配置を全部やり直す)。
    /// </summary>
    public IReadOnlyList<MetricTimelinePoint> TimelinePoints
    {
        get => _timelinePoints;
        private set
        {
            if (!_timelinePoints.SequenceEqual(value))
            {
                SetProperty(ref _timelinePoints, value);
            }
        }
    }

    /// <summary>推移グラフの横軸の長さ(秒)。設定「横軸の長さ」(窓の上のホイールでも変わる)。</summary>
    public int TimelineVisibleSeconds
    {
        get => _timelineVisibleSeconds;
        private set => SetProperty(ref _timelineVisibleSeconds, value);
    }

    /// <summary>推移グラフの横軸の下に技のアイコンの行を出すか。設定「スキルログを表示」。</summary>
    public bool TimelineShowsSkillLog
    {
        get => _timelineShowsSkillLog;
        private set => SetProperty(ref _timelineShowsSkillLog, value);
    }

    /// <summary>推移グラフの線の色。その人のクラスのグラフカラー(フィルターを掛けた色)。相手が分かるまでは null。</summary>
    public Brush? TimelineLineBrush
    {
        get => _timelineLineBrush;
        private set => SetProperty(ref _timelineLineBrush, value);
    }

    /// <summary>推移グラフの横軸の下に出す、その人の技の開始のアイコン(届いた順)。</summary>
    public IReadOnlyList<MetricTimelineSkillMarker> TimelineSkillMarkers
    {
        get => _timelineSkillMarkers;
        private set => SetProperty(ref _timelineSkillMarkers, value);
    }

    public ReadOnlyObservableCollection<MetricSkillTableEntry> SkillEntries { get; }

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

        // ホイールの保存待ちのまま窓を閉じた(アプリの終了で閉じたときも含む)。その値をここで書く。
        SavePendingWheelVisibleSeconds();
        _wheelSaveTimer.Tick -= WheelSaveTimer_Tick;

        _isDisposed = true;
        _refreshTimer.Stop();
        _refreshTimer.Tick -= RefreshTimer_Tick;
        _configManager.SettingsPreviewChanged -= ConfigManager_SettingsPreviewChanged;
        PlayerWidget.ElementColorSettingsChanged -= Widget_ElementColorSettingsChanged;
        PlayerWidget.SkillDetailSettingsChanged -= Widget_SkillDetailSettingsChanged;
        LocalizationManager.Instance.CultureChanged -= LocalizationManager_CultureChanged;
    }

    /// <summary>
    /// 推移グラフの窓の上のホイール。<paramref name="notches"/> は奥へ回すと正で、1回ごとに横軸を
    /// <see cref="WidgetConfigDefaults.MetricTimelineVisibleSecondsWheelStep"/> 秒短くする(手前へ回すと長くする)。
    /// 同じ種類の窓の描き直しと設定の窓の表示はすぐ変え、保存はホイールが止まってから(<see cref="WheelSaveDelay"/>)行う。
    /// 保存するのはホイールが決めた値(止まるまでに設定の窓のスライダーを動かしても、そのプレビューの値は書かない)。
    /// </summary>
    public void ChangeTimelineVisibleSecondsByWheel(int notches)
    {
        if (!IsTimeline || notches == 0)
        {
            return;
        }

        var current = PlayerWidget.MetricTimelineVisibleSeconds;
        var visibleSeconds = WidgetConfigDefaults.ClampMetricTimelineVisibleSeconds(
            current - notches * WidgetConfigDefaults.MetricTimelineVisibleSecondsWheelStep);
        if (visibleSeconds == current)
        {
            return;
        }

        PlayerWidget.SetMetricTimelineVisibleSecondsFromWidget(visibleSeconds);
        _pendingWheelVisibleSeconds = visibleSeconds;
        _wheelSaveTimer.Stop();
        _wheelSaveTimer.Start();
    }

    protected override void OnSelectedPlayerChanged(PlayerRosterEntry? player)
    {
        Refresh();
    }

    private void WheelSaveTimer_Tick(object? sender, EventArgs e)
    {
        SavePendingWheelVisibleSeconds();
    }

    private void SavePendingWheelVisibleSeconds()
    {
        _wheelSaveTimer.Stop();
        if (_pendingWheelVisibleSeconds is not { } visibleSeconds)
        {
            return;
        }

        _pendingWheelVisibleSeconds = null;
        PlayerWidget.SaveMetricTimelineVisibleSeconds(visibleSeconds);
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

    private void Widget_SkillDetailSettingsChanged(object? sender, EventArgs e)
    {
        _skillDetailSettings = PlayerWidget.GetSkillDetailSettingsSnapshot();
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

        var timelineSettings = IsTimeline ? PlayerWidget.GetMetricTimelineSettingsSnapshot() : null;
        if (timelineSettings is not null)
        {
            TimelineVisibleSeconds = timelineSettings.VisibleSeconds;
            TimelineShowsSkillLog = timelineSettings.ShowSkillLog;
        }

        if (SelectedCharacterId is not { } characterId)
        {
            ApplyEmptyData();
            return;
        }

        var playerIdentity = MeterSnapshotProvider.GetPlayerIdentity(characterId);
        if (playerIdentity is not null)
        {
            SetHeaderText(playerIdentity.Name, playerIdentity.UserId, playerIdentity.IsNpc, playerIdentity.ProfessionId);
        }

        if (timelineSettings is not null)
        {
            var timeline = MeterSnapshotProvider.GetPlayerTimeline(
                _kind,
                characterId,
                timelineSettings.AggregationIntervalSeconds);
            TimelinePoints = timeline.Points;
            if (timelineSettings.ShowSkillLog)
            {
                ApplyTimelineSkillMarkers(MeterSnapshotProvider.GetPlayerSkillActivations(characterId));
            }
            else if (TimelineSkillMarkers.Count != 0)
            {
                // 出さない間は技の開始を取りに行かず、アイコンの部品も作らない。
                TimelineSkillMarkers = Array.Empty<MetricTimelineSkillMarker>();
            }
            if (playerIdentity is not null)
            {
                ApplyTimelineLineColor(
                    timelineSettings,
                    PlayerProfession.GetKey(playerIdentity.ProfessionId, playerIdentity.ClassSpec));
            }

            ClearSkillEntries();
            return;
        }

        var table = MeterSnapshotProvider.GetPlayerSkillTable(_kind, characterId);
        TimelinePoints = Array.Empty<MetricTimelinePoint>();
        SynchronizeSkillEntries(table.Entries, _configManager.GetSettingsSnapshot().NumberDisplayFormatIndex);
    }

    private void ApplyEmptyData()
    {
        TimelinePoints = Array.Empty<MetricTimelinePoint>();
        TimelineSkillMarkers = Array.Empty<MetricTimelineSkillMarker>();
        ClearSkillEntries();
    }

    /// <summary>
    /// 技のアイコンの一覧。アイコンは技の表のアイコンの欄を Skills のフォルダで探し(無ければ null = クラス不明のアイコン)、
    /// 名前が無い技の TIPS は「不明」。G○ の数字があれば、プレイヤーリストの技の枠の TIPS と同じく名前の後ろに「 G○」を付ける。
    /// 背景の枠とイマジンの絵かは Core が決めた値をそのまま渡す。
    /// 中身が前と同じなら差し替えない(グラフの並べ直しを毎回起こさない)。
    /// </summary>
    private void ApplyTimelineSkillMarkers(IReadOnlyList<MetricTimelineSkillActivation> activations)
    {
        var unknownName = LocalizationManager.Instance.GetString("MetricTimeline_UnknownSkill");
        var markers = new MetricTimelineSkillMarker[activations.Count];
        for (var index = 0; index < activations.Count; index++)
        {
            var activation = activations[index];
            var name = string.IsNullOrWhiteSpace(activation.Name) ? unknownName : activation.Name;
            markers[index] = new MetricTimelineSkillMarker(
                activation.Seconds,
                CombatIconResolver.ResolveSkillIcon(activation.IconName),
                activation.Grade is { } grade ? $"{name} G{grade}" : name,
                activation.Frame,
                activation.UsesImagineAsset);
        }

        if (markers.SequenceEqual(TimelineSkillMarkers))
        {
            return;
        }

        TimelineSkillMarkers = markers;
    }

    /// <summary>
    /// 線の色。クラスの鍵の選んでいる色にフィルターを掛ける(メーターの行のクラスカラーと同じ掛け方)。グラフカラーは不透明度を持たないので不透明で描く。
    /// </summary>
    private void ApplyTimelineLineColor(MetricTimelineWidgetSettingsConfig settings, string classKey)
    {
        var lineColor = ResolveGraphColor(settings, classKey);
        if (TimelineLineBrush is SolidColorBrush current && current.Color == lineColor)
        {
            return;
        }

        var brush = new SolidColorBrush(lineColor);
        brush.Freeze();
        TimelineLineBrush = brush;
    }

    /// <summary>
    /// グラフカラーの <paramref name="classKey"/> の選んでいる色にフィルターを掛けた不透明の色。
    /// 設定はそろえた後なので、鍵の行と色の形は必ずある。
    /// </summary>
    private static Color ResolveGraphColor(MetricTimelineWidgetSettingsConfig settings, string classKey)
    {
        var palette = settings.ClassColorPalettes[classKey];
        var hex = palette[Math.Clamp(settings.ClassColorIndexes[classKey], 0, palette.Count - 1)];
        if (!ColorUtilities.TryParseHex(hex, out var color))
        {
            throw new InvalidOperationException($"Graph color is not a valid color (class={classKey}, value={hex}).");
        }

        var filtered = ClassColorFilter.Apply(color, settings);
        return Color.FromRgb(filtered.R, filtered.G, filtered.B);
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

        // バーの長さは1位の値で割る(メーターと同じ)。並びは総量の降順なので先頭が1位。
        var topValue = entries.Count == 0 ? 0UL : entries[0].TotalValue;

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
                ApplySkillEntry(item, entry, index, numberDisplayFormatIndex, topValue);
                _skillEntriesBySkillId.Add(entry.SkillId, item);
                _skillEntries.Insert(index, item);
                continue;
            }

            ApplySkillEntry(item, entry, index, numberDisplayFormatIndex, topValue);
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

    /// <param name="topValue">一覧の1位の総量。バーの長さはこれに対する比(メーターと同じ)。</param>
    private void ApplySkillEntry(
        MetricSkillTableEntry item,
        MetricSkillTableRowSnapshot entry,
        int index,
        int numberDisplayFormatIndex,
        ulong topValue)
    {
        item.Update(
            // 並びは総量の降順なので、そのまま振るとメーターの順位と同じ形になる。
            (index + 1).ToString(CultureInfo.CurrentCulture),
            CreateElementSegments(entry),
            CreateRowToolTipText(entry),
            SkillInfoFormatFormatter.Format(
                entry.Name,
                CreateElementText(entry),
                CreateDamageModeText(entry),
                entry.HitCount,
                entry.CritRate,
                _skillDetailSettings.SkillInfoFormatString),
            // 値の書式はメーターの行と同じ(MeterPlayerEntry.ValueText)。
            $"{MeterNumberFormatter.Format(entry.TotalValue, numberDisplayFormatIndex)}"
                + $" ({MeterNumberFormatter.Format(entry.ValuePerSecond, numberDisplayFormatIndex)})"
                + $" {entry.Percentage.ToString("F2", CultureInfo.CurrentCulture)}%",
            CreateBarBrush(entry),
            topValue == 0UL ? 0d : Math.Clamp(entry.TotalValue / (double)topValue, 0d, 1d));
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
    /// 材料(属性の内訳)が無ければ色を作れないので <c>null</c> を返す(バーを出さない)。
    /// </summary>
    private Brush? CreateBarBrush(MetricSkillTableRowSnapshot entry)
    {
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
    /// 属性の内訳。アイコンだけを属性ID順に並べる。<b>割合が 0 の属性は出さない</b>ので、
    /// 混ざっていない行はアイコン1つになる。割合は行の TIPS(<see cref="CreateRowToolTipText"/>)に出す。
    /// </summary>
    private IReadOnlyList<object> CreateElementSegments(MetricSkillTableRowSnapshot entry)
    {
        // ダメージの多い順に上位2つ。3つ目以降は枠に入らないので出さない(TIPS には全部出る)。
        var masks = new List<Brush>();
        foreach (var pair in entry.ValueByElement.OrderByDescending(pair => pair.Value))
        {
            if (pair.Value == 0UL || masks.Count >= 2)
            {
                break;
            }

            masks.Add(GetElementIconMask(pair.Key.ToString()));
        }

        return masks.Count switch
        {
            0 => [],
            1 => [new MetricElementIconSegment(masks[0], null)],
            _ => [new MetricElementIconSegment(masks[0], masks[1])]
        };
    }

    /// <summary>属性アイコンの形。白い塗りをこの形で抜く(クラスアイコンと同じ染め方)。</summary>
    private ImageBrush GetElementIconMask(string elementKey)
    {
        if (!_elementIconMasks.TryGetValue(elementKey, out var mask))
        {
            mask = new ImageBrush((ImageSource)Application.Current.FindResource($"Icon.DamageProperty.{elementKey}"))
            {
                Stretch = Stretch.Uniform
            };
            mask.Freeze();
            _elementIconMasks[elementKey] = mask;
        }

        return mask;
    }

    /// <summary>
    /// 行の TIPS。「火属性33.33%/光属性66.67%, 物理60.00%/無分類40.00%」の形で、
    /// 属性の内訳とタイプの内訳を「, 」でつなぐ。割合が 0 のものは出さない。
    /// </summary>
    private static string CreateRowToolTipText(MetricSkillTableRowSnapshot entry)
    {
        var elements = CreateElementShareText(entry);
        var modes = string.Join(
            ShareSeparator,
            EnumerateDamageModeParts(entry).Select(part => part.Label + FormatShare(part.Value, entry.TotalValue)));

        if (elements.Length == 0)
        {
            return modes;
        }

        return modes.Length == 0 ? elements : elements + ToolTipPartSeparator + modes;
    }

    /// <summary>属性の内訳(割合つき)。「火属性33.33%/光属性66.67%」。</summary>
    private static string CreateElementShareText(MetricSkillTableRowSnapshot entry)
    {
        var localization = LocalizationManager.Instance;
        var parts = new List<string>();
        foreach (var pair in entry.ValueByElement)
        {
            if (pair.Value == 0UL)
            {
                continue;
            }

            parts.Add(localization.GetString($"DamageProperty_{pair.Key}") + FormatShare(pair.Value, entry.TotalValue));
        }

        return string.Join(ShareSeparator, parts);
    }

    /// <summary>書式の {Element} に入れる属性名。2種類以上あれば「/」でつなぐ(割合は付けない)。</summary>
    private static string CreateElementText(MetricSkillTableRowSnapshot entry)
    {
        var localization = LocalizationManager.Instance;
        var parts = new List<string>();
        foreach (var pair in entry.ValueByElement)
        {
            if (pair.Value == 0UL)
            {
                continue;
            }

            parts.Add(localization.GetString($"DamageProperty_{pair.Key}"));
        }

        return string.Join(ShareSeparator, parts);
    }

    /// <summary>
    /// 物理・魔法の内訳。ラベルだけを「/」で並べる。<b>並びは 物理 → 魔法 → 無分類</b> で、割合が 0 のものは出さない。
    /// 「無分類」は <c>DamageNormal</c>(物理でも魔法でもない)。
    /// 割合は行の TIPS(<see cref="CreateRowToolTipText"/>)に出す。
    /// </summary>
    private static string CreateDamageModeText(MetricSkillTableRowSnapshot entry)
    {
        return string.Join(ShareSeparator, EnumerateDamageModeParts(entry).Select(part => part.Label));
    }

    /// <summary>種類の欄に出す分だけを、決めた並びで返す。</summary>
    private static IEnumerable<(string Label, ulong Value)> EnumerateDamageModeParts(MetricSkillTableRowSnapshot entry)
    {
        var localization = LocalizationManager.Instance;
        var parts = new List<(string Label, ulong Value)>();
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
                _ => localization.GetString("Metric_DamageMode_Normal")
            };

            parts.Add((label, value));
        }

        return parts;
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
