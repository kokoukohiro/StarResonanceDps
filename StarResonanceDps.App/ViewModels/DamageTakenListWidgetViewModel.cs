using System.Collections.ObjectModel;
using System.Globalization;
using System.Windows;
using System.Windows.Media;
using System.Windows.Threading;
using CommunityToolkit.Mvvm.Input;
using StarResonanceDps.App.Config;
using StarResonanceDps.App.Localization;
using StarResonanceDps.App.Models.Widgets;
using StarResonanceDps.App.Services;
using StarResonanceDps.Core.CombatRuntime;
using StarResonanceDps.Core.Models;
using StarResonanceDps.Core.Services;

namespace StarResonanceDps.App.ViewModels;

/// <summary>
/// 被ダメリスト。表示中の回(ライブか履歴、タイムラインと同じ)で被弾したプレイヤーを並べ、右にその人の最後の被弾を出す(1枚だけ開く窓)。
///
/// <para>
/// 被弾は <see cref="MeterSnapshotProvider.GetDamageTakenList"/>、名前・クラス・特化は名簿(蘇生リストと同じ)。名簿に居ない人は出さない。
/// 並びは最後の被弾の新しい順(被弾の通し番号の大きい順)で、新しく被弾した人の行が一番上へ上がる。
/// </para>
///
/// <para>
/// 行の作りはメーターと同じ。左は経過「n秒前」(回の戦闘の時計で数える。メーターのタイマーと同じで、ライブも履歴も同じ式)・クラスアイコン・名前、
/// 右は設定「スキル名」の書式(エンティティ名・スキル名・タイプ、決め方はタイムラインの被弾の行と同じ)・被ダメ値・属性アイコン。
/// クラスアイコンは設定「アイコンカラー」の色で染め、ほかの文字とアイコンは白で、自分の強調表示と「(戦闘不能)」だけ色が付く(画面の側)。
/// 下敷きのバーは右から伸び、色はその被弾の属性の色(設定「属性カラー」)、長さは一覧の中で被ダメ値が一番大きい人を100%にする。
/// </para>
///
/// <para>
/// 被弾は変化の合図が無いので、メーターと同じ 0.25秒ごとに回の被ダメログの控えを前回の続きから読む(タイムラインと同じ読み方)。
/// 1人ずつの最後の被弾はここで持ち、新しい被弾か名簿・設定が変わったときだけ行を当て直す。何も変わらなければ書き換えるのは経過の文字だけ。
/// 行は作り直さず値だけ入れ替え、右の文字は被弾か設定が変わったときだけ作る。行の左クリックと右クリックのメニューは蘇生リストと同じ。
/// </para>
/// </summary>
public sealed partial class DamageTakenListWidgetViewModel : ViewModelBase, IDisposable, IWidgetDataStatusSource
{
    private static readonly TimeSpan RefreshInterval = TimeSpan.FromMilliseconds(250);

    private readonly WidgetListItemViewModel _widget;
    private readonly ConfigManager _configManager = ConfigManager.Instance;

    /// <summary>プレイヤーの窓を開く(種類と人の番号)。窓の管理の <c>OpenPlayerWindow</c>。</summary>
    private readonly Action<WidgetKind, long> _requestPlayerWindow;

    private readonly DispatcherTimer _refreshTimer;
    private readonly ObservableCollection<DamageTakenListEntry> _entries = [];
    private readonly Dictionary<long, DamageTakenListEntry> _entriesByCharacterId = [];
    private readonly Dictionary<string, SolidColorBrush> _classBrushes = new(StringComparer.Ordinal);
    private readonly Dictionary<string, ImageBrush> _professionIconMasks = new(StringComparer.Ordinal);
    private readonly Dictionary<Zproto.EDamageProperty, ImageBrush> _elementIconMasks = [];
    private readonly Dictionary<Zproto.EDamageProperty, Brush> _barBrushes = [];
    private readonly Dictionary<long, PlayerRosterEntry> _rosterRowsByCharacterId = [];

    /// <summary>1人ずつの最後の被弾。回が替わったら捨てる。</summary>
    private readonly Dictionary<long, DamageTakenListHit> _lastHitsByCharacterId = [];

    private DamageTakenListWidgetSettingsConfig _settings;
    private ElementColorWidgetSettingsConfig _elementColorSettings;
    private Encounter? _encounter;

    /// <summary>次に読む被ダメログの位置(<see cref="DamageTakenListSnapshot.NextIndex"/>)。</summary>
    private int _nextRecordIndex;

    /// <summary>行を当て直す要があるか。新しい被弾・名簿・設定・言語・回の切り替わりで立てる。</summary>
    private bool _rowsDirty = true;

    /// <summary>右の文字の版。設定・言語・全体設定が変わったら進め、全部の行の文字を作り直させる。</summary>
    private int _hitTextVersion;

    public DamageTakenListWidgetViewModel(
        WidgetListItemViewModel widget,
        PlayerRosterSnapshot roster,
        Action<WidgetKind, long> requestPlayerWindow)
    {
        _widget = widget;
        _requestPlayerWindow = requestPlayerWindow;
        _settings = widget.GetDamageTakenListSettingsSnapshot();
        _elementColorSettings = widget.GetElementColorSettingsSnapshot();
        SetRoster(roster);
        Entries = new ReadOnlyObservableCollection<DamageTakenListEntry>(_entries);

        _refreshTimer = new DispatcherTimer
        {
            Interval = RefreshInterval
        };
        _refreshTimer.Tick += RefreshTimer_Tick;
        _widget.DamageTakenListSettingsChanged += Widget_DamageTakenListSettingsChanged;
        _widget.ElementColorSettingsChanged += Widget_ElementColorSettingsChanged;
        _configManager.SettingsPreviewChanged += ConfigManager_SettingsPreviewChanged;
        LocalizationManager.Instance.CultureChanged += LocalizationManager_CultureChanged;

        Refresh();
        _refreshTimer.Start();
    }

    public ReadOnlyObservableCollection<DamageTakenListEntry> Entries { get; }

    /// <summary>このウィジェットのカード。右クリックのメニューと行の TIPS が窓の色(<c>WidgetWindowPalette</c>)を引く。</summary>
    public WidgetListItemViewModel Widget => _widget;

    public WidgetDataStatus DataStatus { get; private set; }

    public event EventHandler? DataStatusChanged;

    /// <summary>状態の印はタイムラインと同じ決まり(同じ被弾の記録を出すため)。</summary>
    public void RefreshDataStatus()
    {
        var status = WidgetDataStatusRules.ForTakenDamageLog(
            GameReceptionStateStore.Current,
            EncounterHistoryProvider.SelectedEncounterId is not null,
            EncounterHistoryProvider.HasLoadFailed);
        if (status == DataStatus)
        {
            return;
        }

        DataStatus = status;
        DataStatusChanged?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>名簿が変わった(画面のスレッドで窓の管理から呼ばれる)。</summary>
    public void UpdateRoster(PlayerRosterSnapshot roster)
    {
        SetRoster(roster);
        Refresh();
    }

    /// <summary>名簿の行を人の番号で引けるように控え、行を当て直させる。</summary>
    private void SetRoster(PlayerRosterSnapshot roster)
    {
        _rosterRowsByCharacterId.Clear();
        foreach (var row in roster.Entries)
        {
            _rosterRowsByCharacterId.TryAdd(row.CharacterId, row);
        }

        _rowsDirty = true;
    }

    public void Dispose()
    {
        _refreshTimer.Stop();
        _refreshTimer.Tick -= RefreshTimer_Tick;
        _widget.DamageTakenListSettingsChanged -= Widget_DamageTakenListSettingsChanged;
        _widget.ElementColorSettingsChanged -= Widget_ElementColorSettingsChanged;
        _configManager.SettingsPreviewChanged -= ConfigManager_SettingsPreviewChanged;
        LocalizationManager.Instance.CultureChanged -= LocalizationManager_CultureChanged;
    }

    private void RefreshTimer_Tick(object? sender, EventArgs e)
    {
        Refresh();
    }

    [RelayCommand]
    private void RequestPlayerInfo(DamageTakenListEntry? player)
    {
        RequestPlayerWindow(WidgetKind.PlayerInfo, player);
    }

    [RelayCommand]
    private void RequestPlayerStatus(DamageTakenListEntry? player)
    {
        RequestPlayerWindow(WidgetKind.PlayerStatus, player);
    }

    [RelayCommand]
    private void RequestPlayerEquipment(DamageTakenListEntry? player)
    {
        RequestPlayerWindow(WidgetKind.PlayerEquipment, player);
    }

    [RelayCommand]
    private void RequestPlayerTalent(DamageTakenListEntry? player)
    {
        RequestPlayerWindow(WidgetKind.PlayerTalent, player);
    }

    [RelayCommand]
    private void RequestPlayerSeasonTalent(DamageTakenListEntry? player)
    {
        RequestPlayerWindow(WidgetKind.PlayerSeasonTalent, player);
    }

    [RelayCommand]
    private void RequestPlayerModule(DamageTakenListEntry? player)
    {
        RequestPlayerWindow(WidgetKind.PlayerModule, player);
    }

    [RelayCommand]
    private void RequestBuffList(DamageTakenListEntry? player)
    {
        RequestPlayerWindow(WidgetKind.BuffList, player);
    }

    [RelayCommand]
    private void RequestDebuffList(DamageTakenListEntry? player)
    {
        RequestPlayerWindow(WidgetKind.DebuffList, player);
    }

    private void RequestPlayerWindow(WidgetKind kind, DamageTakenListEntry? player)
    {
        if (player is not null)
        {
            _requestPlayerWindow(kind, player.CharacterId);
        }
    }

    /// <summary>表示設定とアイコンカラー(保存かプレビュー)が変わった。クラスの色のブラシを作り直し、全部の行の文字を作り直す。</summary>
    private void Widget_DamageTakenListSettingsChanged(object? sender, EventArgs e)
    {
        _settings = _widget.GetDamageTakenListSettingsSnapshot();
        _classBrushes.Clear();
        _hitTextVersion++;
        _rowsDirty = true;
        Refresh();
    }

    /// <summary>属性カラー(保存かプレビュー)が変わった。バーの色のブラシを作り直す。</summary>
    private void Widget_ElementColorSettingsChanged(object? sender, EventArgs e)
    {
        _elementColorSettings = _widget.GetElementColorSettingsSnapshot();
        _barBrushes.Clear();
        _rowsDirty = true;
        Refresh();
    }

    /// <summary>全体設定(内部IDの表示・名前の無い技の出し方・プレイヤー名)は行の文字に入るので作り直す。</summary>
    private void ConfigManager_SettingsPreviewChanged(object? sender, EventArgs e)
    {
        _hitTextVersion++;
        _rowsDirty = true;
        Refresh();
    }

    private void LocalizationManager_CultureChanged(object? sender, EventArgs e)
    {
        _hitTextVersion++;
        _rowsDirty = true;
        Refresh();
    }

    private void Refresh()
    {
        var snapshot = MeterSnapshotProvider.GetDamageTakenList(_encounter, _nextRecordIndex);
        if (!ReferenceEquals(snapshot.Encounter, _encounter))
        {
            // 回が替わった(Core は先頭から読み直している)。被弾の通し番号は回をまたいで比べないので、
            // 1人ずつの最後の被弾を捨て、全部の行の文字を作り直させる。
            _encounter = snapshot.Encounter;
            _lastHitsByCharacterId.Clear();
            _hitTextVersion++;
            _rowsDirty = true;
        }

        _nextRecordIndex = snapshot.NextIndex;
        foreach (var hit in snapshot.Hits)
        {
            _lastHitsByCharacterId[hit.CharacterId] = hit;
            _rowsDirty = true;
        }

        if (_rowsDirty)
        {
            _rowsDirty = false;
            RebuildRows();
        }

        UpdateElapsedTexts();
        RefreshDataStatus();
    }

    /// <summary>1人ずつの最後の被弾を行へ当て直す。名簿に居ない人は出さない。</summary>
    private void RebuildRows()
    {
        var nameDisplayMode = (PlayerNameDisplayMode)_configManager.GetSettingsSnapshot().PlayerNameDisplayModeIndex;
        var shownCharacterIds = new HashSet<long>();
        foreach (var hit in _lastHitsByCharacterId.Values)
        {
            if (!_rosterRowsByCharacterId.TryGetValue(hit.CharacterId, out var row))
            {
                continue;
            }

            shownCharacterIds.Add(hit.CharacterId);

            // 新しい行は後ろに足し、並べ直しで最後の被弾の新しさの位置へ動かす。
            if (!_entriesByCharacterId.TryGetValue(hit.CharacterId, out var entry))
            {
                entry = new DamageTakenListEntry(hit.CharacterId);
                _entriesByCharacterId.Add(hit.CharacterId, entry);
                _entries.Add(entry);
            }

            UpdateEntry(entry, row, hit, nameDisplayMode);
        }

        for (var index = _entries.Count - 1; index >= 0; index--)
        {
            var entry = _entries[index];
            if (shownCharacterIds.Contains(entry.CharacterId))
            {
                continue;
            }

            // 一覧から消える人(回が替わった、名簿から外れた)のメニューは閉じる。開いたままだと消えた人のメニューが残る。
            entry.IsPlayerSelectionMenuOpen = false;
            _entriesByCharacterId.Remove(entry.CharacterId);
            _entries.RemoveAt(index);
        }

        UpdateBarRatios();
        SortEntries();
    }

    /// <summary>
    /// 経過の文字。回の戦闘の時計で数える(メーターのタイマーと同じ。自動一時停止で止まっている間は進まず、履歴はタイマーの終わりで止まる)。
    /// 同じ秒の間は同じ文字なので通知は出ない。
    /// </summary>
    private void UpdateElapsedTexts()
    {
        var combatElapsed = _encounter?.ReadCombatClock() is { StartUtc: not null } clock
            ? clock.Elapsed
            : (TimeSpan?)null;
        foreach (var entry in _entries)
        {
            entry.ElapsedText = FormatElapsed(combatElapsed, _encounter?.ToCombatOffset(entry.HitTimestamp));
        }
    }

    private void UpdateEntry(
        DamageTakenListEntry entry,
        PlayerRosterEntry row,
        DamageTakenListHit hit,
        PlayerNameDisplayMode nameDisplayMode)
    {
        var professionKey = PlayerProfession.GetKey(row.ProfessionId, row.ClassSpec);
        entry.IsSelf = row.IsSelf;
        entry.ClassIconMask = GetProfessionIconMask(professionKey);
        entry.ClassBrush = GetClassBrush(professionKey);
        entry.DisplayName = PlayerInfoFormatFormatter.Format(row, _settings.PlayerInfoFormatString, nameDisplayMode);
        entry.IsSelfHighlighted = row.IsSelf
            && _settings.SelfDisplayModeIndex == WidgetConfigDefaults.DefaultSelfDisplayModeIndex;
        entry.SortKey = hit.Sequence;
        entry.HitValue = hit.Value;

        if (entry.HitSequence != hit.Sequence || entry.HitTextVersion != _hitTextVersion)
        {
            entry.HitSequence = hit.Sequence;
            entry.HitTextVersion = _hitTextVersion;
            entry.HitInfoText = DamageTakenListHitInfoFormatter.Format(
                _settings.HitInfoFormatString,
                FormatSourceName(hit),
                PlayerInfoFormatFormatter.GetPartyDisplayName(hit.Attacker),
                FormatDamageModeText(hit.DamageMode));
            entry.ValueText = hit.Value.ToString(CultureInfo.InvariantCulture);
            entry.ElementToolTipText = hit.DamageElement is { } element
                ? LocalizationManager.Instance.GetString($"DamageProperty_{element}")
                : string.Empty;
        }

        entry.IsDefeatShown = hit.IsLethal && _settings.ShowsDefeatCause;
        if (hit.DamageElement is { } hitElement)
        {
            entry.HasElement = true;
            entry.ElementIconMask = GetElementIconMask(hitElement);
            entry.BarBrush = GetBarBrush(hitElement);
        }
        else
        {
            entry.HasElement = false;
            entry.ElementIconMask = null;
            entry.BarBrush = null;
        }

        entry.HitTimestamp = hit.Timestamp;
    }

    /// <summary>バーの長さ。一覧の中で被ダメ値が一番大きい人を 1 にする(スクロールで見えていない行も含める、メーターと同じ)。</summary>
    private void UpdateBarRatios()
    {
        var maxValue = 0L;
        foreach (var entry in _entries)
        {
            maxValue = Math.Max(maxValue, entry.HitValue);
        }

        foreach (var entry in _entries)
        {
            entry.BarRatio = maxValue > 0
                ? Math.Clamp(entry.HitValue / (double)maxValue, 0d, 1d)
                : 0d;
        }
    }

    /// <summary>最後の被弾の新しい順に並べ直す。動く行だけ動かす。</summary>
    private void SortEntries()
    {
        var ordered = _entries.OrderByDescending(entry => entry.SortKey).ToArray();
        for (var targetIndex = 0; targetIndex < ordered.Length; targetIndex++)
        {
            var currentIndex = _entries.IndexOf(ordered[targetIndex]);
            if (currentIndex != targetIndex)
            {
                _entries.Move(currentIndex, targetIndex);
            }
        }
    }

    /// <summary>
    /// 技(またはバフ)の名前。タイムラインの技の行と同じで、落下は「落下」、名前が無ければ「攻撃」、内部ID注記は全体設定に従う。
    /// </summary>
    private static string FormatSourceName(DamageTakenListHit hit)
    {
        var localization = LocalizationManager.Instance;
        if (hit.IsFall)
        {
            return localization.GetString("TakenDamageLog_FallSkill");
        }

        var name = string.IsNullOrEmpty(hit.SourceName)
            ? localization.GetString("TakenDamageLog_UnnamedSkill")
            : hit.SourceName;
        return hit.IsBuffSource
            ? CombatDataCatalog.AppendBuffInternalId(name, hit.SourceId)
            : CombatDataCatalog.AppendSkillInternalId(name, hit.SourceId);
    }

    /// <summary>書式の <c>{Type}</c>。値はスキル詳細の書式の <c>{Type}</c> と同じ(物理 / 魔法 / 無分類)。</summary>
    private static string FormatDamageModeText(Zproto.EDamageMode mode)
    {
        var localization = LocalizationManager.Instance;
        return mode switch
        {
            Zproto.EDamageMode.DamagePhysical => localization.GetString("Metric_DamageMode_Physical"),
            Zproto.EDamageMode.DamageMagical => localization.GetString("Metric_DamageMode_Magical"),
            _ => localization.GetString("Metric_DamageMode_Normal")
        };
    }

    /// <summary>
    /// 経過「n秒前」。戦闘の時計の今の経過 <paramref name="combatElapsed"/> − 被弾の戦闘の経過 <paramref name="hitOffset"/>(切り捨て、何秒でも秒)。
    /// 時計の起点がまだ無ければ空欄(タイムラインの行の時刻と同じ)。
    /// </summary>
    private static string FormatElapsed(TimeSpan? combatElapsed, TimeSpan? hitOffset)
    {
        if (combatElapsed is not { } elapsed || hitOffset is not { } offset)
        {
            return string.Empty;
        }

        var seconds = Math.Max(0, (int)Math.Floor((elapsed - offset).TotalSeconds));
        return LocalizationManager.Instance.Format("DamageTakenList_SecondsAgo", seconds);
    }

    /// <summary>クラスアイコンの形。絵は職業ごとに1つなので鍵ごとに使い回す。</summary>
    private ImageBrush GetProfessionIconMask(string professionKey)
    {
        if (!_professionIconMasks.TryGetValue(professionKey, out var mask))
        {
            mask = new ImageBrush((ImageSource)Application.Current.FindResource($"Icon.Profession.{professionKey}"))
            {
                Stretch = Stretch.Uniform
            };
            mask.Freeze();
            _professionIconMasks[professionKey] = mask;
        }

        return mask;
    }

    /// <summary>クラスアイコンの色。設定が変われば <see cref="_classBrushes"/> ごと捨てて引き直す。</summary>
    private SolidColorBrush GetClassBrush(string professionKey)
    {
        if (!_classBrushes.TryGetValue(professionKey, out var brush))
        {
            brush = new SolidColorBrush(SelectClassColor(professionKey));
            brush.Freeze();
            _classBrushes[professionKey] = brush;
        }

        return brush;
    }

    /// <summary>アイコンカラーで選んでいる色(引き方はプレイヤーリストのクラスカラーと同じ)。設定は正規化済みで、全部の鍵を持つ。</summary>
    private Color SelectClassColor(string professionKey)
    {
        var fallback = Color.FromRgb(0xA8, 0xA8, 0xA8);
        if (!_settings.ClassColorPalettes.TryGetValue(professionKey, out var palette) || palette.Count == 0)
        {
            return fallback;
        }

        var selectedIndex = _settings.ClassColorIndexes.TryGetValue(professionKey, out var index)
            ? index
            : WidgetConfigDefaults.MinClassColorIndex;
        return ColorUtilities.TryParseHex(palette[Math.Clamp(selectedIndex, 0, palette.Count - 1)], out var color)
            ? color
            : fallback;
    }

    /// <summary>属性アイコンの形。塗りをこの形で抜く(タイムラインと同じ)。</summary>
    private ImageBrush GetElementIconMask(Zproto.EDamageProperty element)
    {
        if (!_elementIconMasks.TryGetValue(element, out var mask))
        {
            mask = new ImageBrush((ImageSource)Application.Current.FindResource($"Icon.DamageProperty.{element}"))
            {
                Stretch = Stretch.Uniform
            };
            mask.Freeze();
            _elementIconMasks[element] = mask;
        }

        return mask;
    }

    /// <summary>バーの塗り。設定が変われば <see cref="_barBrushes"/> ごと捨てて作り直す。</summary>
    private Brush GetBarBrush(Zproto.EDamageProperty element)
    {
        if (!_barBrushes.TryGetValue(element, out var brush))
        {
            brush = ElementBarColors.CreateBarBrush(
                _elementColorSettings,
                ElementBarColors.Resolve(_elementColorSettings, element));
            _barBrushes[element] = brush;
        }

        return brush;
    }
}
