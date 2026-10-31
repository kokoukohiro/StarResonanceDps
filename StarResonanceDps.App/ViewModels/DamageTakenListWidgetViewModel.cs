using System.Collections.ObjectModel;
using System.Globalization;
using System.Windows;
using System.Windows.Media;
using System.Windows.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
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
/// 被ダメログ(識別子は被ダメリスト)。表示中の回(ライブか履歴、タイムラインと同じ)のプレイヤーの被弾を1回1行で、一番上が一番新しい順に並べる(1枚だけ開く窓)。
/// 設定「最新の被ダメのみ表示」がオンなら1人1行で、その人の出す被弾のうち一番新しいものだけを、最後に被弾した人が上の順に並べる。
/// ヘッダーのボタンでも切り替わる(<see cref="ToggleLatestHitOnlyCommand"/>)。
///
/// <para>
/// 被弾は <see cref="MeterSnapshotProvider.GetDamageTakenList"/> で回の被ダメログの控えを 0.25秒ごとに前回の続きから読む(タイムラインと同じ読み方)。
/// 読んだ被弾はここで全部持ち、設定「フィルター設定」(自傷・フレンドリーファイア以外なら加害者がプレイヤーの被弾を出さない)と名簿で、出す行を決める。
/// 名前・クラス・特化は名簿(蘇生リストと同じ)で、名簿に居ない人の被弾は出さない(名簿は回に被弾の記録がある人を載せるので、出ないのは名簿が届くまでの間だけ)。
/// </para>
///
/// <para>
/// 行の作りはメーターと同じ。左は時刻(タイムラインの行と同じ戦闘の経過)・クラスアイコン・名前、
/// 右は設定「スキル名」の書式(エンティティ名・スキル名・タイプ、決め方はタイムラインの被弾の行と同じ)・被ダメ値・属性アイコン。
/// クラスアイコンは設定「アイコンカラー」の色で染め、ほかの文字とアイコンは白で、自分の強調表示と「(戦闘不能)」だけ色が付く(画面の側)。
/// 下敷きのバーは右から伸び、色はその被弾の属性の色(設定「属性カラー」)、長さは一覧の中で被ダメ値が一番大きい被弾を100%にする。
/// </para>
///
/// <para>
/// 行は作り直さず値だけ入れ替える。名前とクラスは人ごとに1回だけ作り、名簿が変わったらその人の行にだけ当て直す。
/// 出す行が変わる(フィルターか「最新の被ダメのみ表示」を変えた、名簿に人が増えた・減った)ときだけ、持っている被弾から一覧を組み直す。
/// 「最新の被ダメのみ表示」で同じ人が被弾したら、その人の行の被弾を入れ替えて一番上へ動かす。
/// フッターの左は戦闘の時計の経過(メーターのタイマーと同じ)。行の左クリックと右クリックのメニュー(1項目)は、その人の被ダメ詳細を開く。
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

    /// <summary>読んだ被弾の全部(古い順)。出す行はここから決める。回が替わったら捨てる。</summary>
    private readonly List<DamageTakenListHit> _hits = [];

    /// <summary>読んだ被弾に出てくる人。名簿に居るかが変わった人がいれば一覧を組み直す。</summary>
    private readonly HashSet<long> _hitCharacterIds = [];

    private readonly Dictionary<long, PlayerRosterEntry> _rosterRowsByCharacterId = [];

    /// <summary>人ごとの名前とクラス。名簿・設定・言語が変わったら作り直す。</summary>
    private readonly Dictionary<long, PersonDisplay> _personDisplays = [];

    /// <summary>「最新の被ダメのみ表示」の人ごとの行。全部出すときは空。</summary>
    private readonly Dictionary<long, DamageTakenListEntry> _latestEntriesByCharacterId = [];

    private readonly Dictionary<string, SolidColorBrush> _classBrushes = new(StringComparer.Ordinal);
    private readonly Dictionary<string, ImageBrush> _professionIconMasks = new(StringComparer.Ordinal);
    private readonly Dictionary<Zproto.EDamageProperty, Brush> _barBrushes = [];

    private DamageTakenListWidgetSettingsConfig _settings;
    private ElementColorWidgetSettingsConfig _elementColorSettings;
    private Encounter? _encounter;

    /// <summary>次に読む被ダメログの位置(<see cref="DamageTakenListSnapshot.NextIndex"/>)。</summary>
    private int _nextRecordIndex;

    /// <summary>行の時刻を作ったときの戦闘の時計の起点。変わったら全部の行の時刻を作り直す。</summary>
    private DateTime? _combatStartUtc;

    /// <summary>今のバーの長さの基準(出している行の中で一番大きい被ダメ値)。</summary>
    private long _maxHitValue;

    /// <summary>フッターの左。戦闘の時計の経過(メーターのタイマーと同じ書式)。</summary>
    [ObservableProperty]
    private string _elapsedText = string.Empty;

    /// <summary>ヘッダーのボタンの文字。押すとどうなるかを出す(メーターの「計測」/「計測停止」と同じ決まり)。</summary>
    [ObservableProperty]
    private string _latestHitOnlyActionText = string.Empty;

    public DamageTakenListWidgetViewModel(
        WidgetListItemViewModel widget,
        PlayerRosterSnapshot roster,
        Action<WidgetKind, long> requestPlayerWindow)
    {
        _widget = widget;
        _requestPlayerWindow = requestPlayerWindow;
        _settings = widget.GetDamageTakenListSettingsSnapshot();
        _elementColorSettings = widget.GetElementColorSettingsSnapshot();
        LatestHitOnlyActionText = CreateLatestHitOnlyActionText();
        Entries = new ReadOnlyObservableCollection<DamageTakenListEntry>(_entries);
        SetRosterRows(roster);

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

    /// <summary>行。上が新しく下が古い。</summary>
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

    /// <summary>
    /// 名簿が変わった(画面のスレッドで窓の管理から呼ばれる)。被弾に出てくる人が名簿に居るかが変わったら一覧を組み直し、
    /// そうでなければ名前とクラスが変わった人の行にだけ当て直す。
    /// </summary>
    public void UpdateRoster(PlayerRosterSnapshot roster)
    {
        var previousRows = new Dictionary<long, PlayerRosterEntry>(_rosterRowsByCharacterId);
        SetRosterRows(roster);

        if (_hitCharacterIds.Any(id => previousRows.ContainsKey(id) != _rosterRowsByCharacterId.ContainsKey(id)))
        {
            RebuildRows();
            return;
        }

        RefreshPersonDisplays(_hitCharacterIds.Where(id => !Equals(
            previousRows.GetValueOrDefault(id),
            _rosterRowsByCharacterId.GetValueOrDefault(id))));
    }

    private void SetRosterRows(PlayerRosterSnapshot roster)
    {
        _rosterRowsByCharacterId.Clear();
        foreach (var row in roster.Entries)
        {
            _rosterRowsByCharacterId.TryAdd(row.CharacterId, row);
        }
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
    private void RequestDamageTakenSummary(DamageTakenListEntry? player)
    {
        if (player is not null)
        {
            _requestPlayerWindow(WidgetKind.DamageTakenSummary, player.CharacterId);
        }
    }

    /// <summary>ヘッダーのボタン。「最新の被ダメのみ表示」を切り替えて、その項目だけを保存する(設定の窓が開いていればそちらの表示も合わせる)。</summary>
    [RelayCommand]
    private void ToggleLatestHitOnly()
    {
        _widget.SetDamageTakenListShowsLatestHitOnlyFromWidget(!_settings.ShowsLatestHitOnly);
    }

    /// <summary>
    /// 表示設定とアイコンカラー(保存かプレビュー、ヘッダーのボタン)が変わった。フィルターか「最新の被ダメのみ表示」が変われば一覧を組み直し、
    /// そうでなければ名前とクラス・右の文字を当て直す(書式の入力の1文字ごとに来るので、行は作り直さない)。
    /// </summary>
    private void Widget_DamageTakenListSettingsChanged(object? sender, EventArgs e)
    {
        var previousFilterIndex = _settings.AttackerFilterIndex;
        var previousShowsLatestHitOnly = _settings.ShowsLatestHitOnly;
        _settings = _widget.GetDamageTakenListSettingsSnapshot();
        _classBrushes.Clear();
        _personDisplays.Clear();
        LatestHitOnlyActionText = CreateLatestHitOnlyActionText();

        if (_settings.AttackerFilterIndex != previousFilterIndex
            || _settings.ShowsLatestHitOnly != previousShowsLatestHitOnly)
        {
            RebuildRows();
            return;
        }

        RefreshPersonDisplays(_hitCharacterIds);
        RefreshHitTexts();
    }

    /// <summary>属性カラー(保存かプレビュー)が変わった。バーの色のブラシを作り直す。</summary>
    private void Widget_ElementColorSettingsChanged(object? sender, EventArgs e)
    {
        _elementColorSettings = _widget.GetElementColorSettingsSnapshot();
        _barBrushes.Clear();
        foreach (var entry in _entries)
        {
            entry.BarBrush = entry.Hit.DamageElement is { } element ? GetBarBrush(element) : null;
        }
    }

    /// <summary>全体設定(内部IDの表示・名前の無い技の出し方・プレイヤー名)は行の文字に入るので作り直す。</summary>
    private void ConfigManager_SettingsPreviewChanged(object? sender, EventArgs e)
    {
        _personDisplays.Clear();
        RefreshPersonDisplays(_hitCharacterIds);
        RefreshHitTexts();
    }

    private void LocalizationManager_CultureChanged(object? sender, EventArgs e)
    {
        LatestHitOnlyActionText = CreateLatestHitOnlyActionText();
        _personDisplays.Clear();
        RefreshPersonDisplays(_hitCharacterIds);
        RefreshHitTexts();
    }

    private void Refresh()
    {
        var snapshot = MeterSnapshotProvider.GetDamageTakenList(_encounter, _nextRecordIndex);
        if (!ReferenceEquals(snapshot.Encounter, _encounter))
        {
            // 回が替わった(Core は先頭から読み直している)。持っている被弾と行を捨てる。
            _encounter = snapshot.Encounter;
            _hits.Clear();
            _hitCharacterIds.Clear();
            ClearEntries();
        }

        _nextRecordIndex = snapshot.NextIndex;

        var clock = _encounter?.ReadCombatClock();
        var combatStartUtc = clock?.StartUtc;
        if (combatStartUtc != _combatStartUtc)
        {
            _combatStartUtc = combatStartUtc;
            foreach (var entry in _entries)
            {
                entry.TimeText = FormatTime(entry.Hit);
            }
        }

        List<DamageTakenListEntry>? addedEntries = null;
        var latestEntriesChanged = false;
        foreach (var hit in snapshot.Hits)
        {
            _hits.Add(hit);
            if (_hitCharacterIds.Add(hit.CharacterId))
            {
                RefreshPersonDisplays([hit.CharacterId]);
            }

            if (!IsShown(hit))
            {
                continue;
            }

            if (_settings.ShowsLatestHitOnly)
            {
                ShowLatestHit(hit);
                latestEntriesChanged = true;
                continue;
            }

            // 被弾は古い順に届くので、1件ずつ先頭に入れると一番新しいものが一番上になる。
            var entry = CreateEntry(hit);
            _entries.Insert(0, entry);
            (addedEntries ??= []).Add(entry);
        }

        if (latestEntriesChanged)
        {
            // 入れ替えた被弾が前の最大より小さいと最大が下がるので、全部の行から決め直す(1人1行なので行は多くない)。
            UpdateAllBarRatios();
        }
        else if (addedEntries is not null)
        {
            UpdateBarRatios(addedEntries);
        }

        ElapsedText = MeterWidgetViewModel.FormatDuration(clock?.Elapsed ?? TimeSpan.Zero);
        RefreshDataStatus();
    }

    /// <summary>持っている被弾から一覧を組み直す(出す行が変わったとき)。名前とクラスは先に名簿から作り直す。</summary>
    private void RebuildRows()
    {
        ClearEntries();
        RefreshPersonDisplays(_hitCharacterIds);
        for (var index = _hits.Count - 1; index >= 0; index--)
        {
            var hit = _hits[index];
            if (!IsShown(hit))
            {
                continue;
            }

            // 新しい順に見るので、「最新の被ダメのみ表示」ではその人の最初に見つかった被弾が一番新しい。
            if (_settings.ShowsLatestHitOnly)
            {
                if (_latestEntriesByCharacterId.ContainsKey(hit.CharacterId))
                {
                    continue;
                }

                var latestEntry = CreateEntry(hit);
                _latestEntriesByCharacterId.Add(hit.CharacterId, latestEntry);
                _entries.Add(latestEntry);
                continue;
            }

            _entries.Add(CreateEntry(hit));
        }

        UpdateBarRatios(_entries);
    }

    /// <summary>
    /// 「最新の被ダメのみ表示」の1件。その人の行があれば被弾を入れ替えて一番上へ動かし(行は作り直さない)、無ければ一番上に足す。
    /// </summary>
    private void ShowLatestHit(DamageTakenListHit hit)
    {
        if (!_latestEntriesByCharacterId.TryGetValue(hit.CharacterId, out var entry))
        {
            entry = CreateEntry(hit);
            _latestEntriesByCharacterId.Add(hit.CharacterId, entry);
            _entries.Insert(0, entry);
            return;
        }

        entry.Hit = hit;
        ApplyHit(entry);
        var index = _entries.IndexOf(entry);
        if (index > 0)
        {
            _entries.Move(index, 0);
        }
    }

    /// <summary>
    /// 行を全部外す。開いているメニューは閉じる(開いたままだと消えた行のメニューが残る)。
    /// </summary>
    private void ClearEntries()
    {
        foreach (var entry in _entries)
        {
            entry.IsPlayerSelectionMenuOpen = false;
        }

        _entries.Clear();
        _latestEntriesByCharacterId.Clear();
        _maxHitValue = 0;
    }

    /// <summary>出す被弾か。名簿に居る人の被弾で、フィルターが自傷・フレンドリーファイア以外なら加害者がプレイヤーでないもの(タイムラインのフィルターと同じ決まり)。</summary>
    private bool IsShown(DamageTakenListHit hit)
    {
        return _rosterRowsByCharacterId.ContainsKey(hit.CharacterId)
            && !(_settings.AttackerFilterIndex == WidgetConfigDefaults.NoSelfOrFriendlyFireDamageTakenListAttackerFilterIndex
                && hit.Attacker.IsPlayer);
    }

    private DamageTakenListEntry CreateEntry(DamageTakenListHit hit)
    {
        var entry = new DamageTakenListEntry(hit);
        ApplyHit(entry);
        if (_personDisplays.TryGetValue(hit.CharacterId, out var person))
        {
            ApplyPerson(entry, person);
        }

        return entry;
    }

    /// <summary>行の被弾から決まるもの(時刻・被ダメ値・設定と言語で変わる文字・属性のアイコンとバーの色)。</summary>
    private void ApplyHit(DamageTakenListEntry entry)
    {
        var hit = entry.Hit;
        entry.TimeText = FormatTime(hit);
        entry.ValueText = hit.Value.ToString(CultureInfo.InvariantCulture);
        ApplyHitTexts(entry);
        if (hit.DamageElement is { } element)
        {
            entry.HasElement = true;
            entry.ElementIconMask = ElementIconMasks.Get(element);
            entry.BarBrush = GetBarBrush(element);
        }
        else
        {
            entry.HasElement = false;
            entry.ElementIconMask = null;
            entry.BarBrush = null;
        }
    }

    /// <summary>設定・言語で変わる行の文字(右の書式・「(戦闘不能)」・TIPS の属性名)。</summary>
    private void ApplyHitTexts(DamageTakenListEntry entry)
    {
        var hit = entry.Hit;
        entry.HitInfoText = DamageTakenListHitInfoFormatter.Format(_settings.HitInfoFormatString, hit);
        entry.IsDefeatShown = hit.IsLethal && _settings.ShowsDefeatCause;
        entry.ElementToolTipText = hit.DamageElement is { } element
            ? LocalizationManager.Instance.GetString($"DamageProperty_{element}")
            : string.Empty;
    }

    private void RefreshHitTexts()
    {
        foreach (var entry in _entries)
        {
            ApplyHitTexts(entry);
        }
    }

    /// <summary>
    /// 人ごとの名前とクラスを名簿から作り直し、変わった人の行にだけ当て直す。名簿に居ない人は捨てる(その人の行は出さない)。
    /// </summary>
    private void RefreshPersonDisplays(IEnumerable<long> characterIds)
    {
        var nameDisplayMode = (PlayerNameDisplayMode)_configManager.GetSettingsSnapshot().PlayerNameDisplayModeIndex;
        HashSet<long>? changedCharacterIds = null;
        foreach (var characterId in characterIds)
        {
            if (!_rosterRowsByCharacterId.TryGetValue(characterId, out var row))
            {
                _personDisplays.Remove(characterId);
                continue;
            }

            var person = CreatePersonDisplay(row, nameDisplayMode);
            if (_personDisplays.TryGetValue(characterId, out var current) && current == person)
            {
                continue;
            }

            _personDisplays[characterId] = person;
            (changedCharacterIds ??= []).Add(characterId);
        }

        if (changedCharacterIds is null)
        {
            return;
        }

        foreach (var entry in _entries)
        {
            if (changedCharacterIds.Contains(entry.CharacterId))
            {
                ApplyPerson(entry, _personDisplays[entry.CharacterId]);
            }
        }
    }

    private PersonDisplay CreatePersonDisplay(PlayerRosterEntry row, PlayerNameDisplayMode nameDisplayMode)
    {
        var professionKey = PlayerProfession.GetKey(row.ProfessionId, row.ClassSpec);
        return new PersonDisplay(
            PlayerInfoFormatFormatter.Format(row, _settings.PlayerInfoFormatString, nameDisplayMode),
            GetProfessionIconMask(professionKey),
            GetClassBrush(professionKey),
            row.IsSelf && _settings.SelfDisplayModeIndex == WidgetConfigDefaults.DefaultSelfDisplayModeIndex);
    }

    private static void ApplyPerson(DamageTakenListEntry entry, PersonDisplay person)
    {
        entry.DisplayName = person.DisplayName;
        entry.ClassIconMask = person.ClassIconMask;
        entry.ClassBrush = person.ClassBrush;
        entry.IsSelfHighlighted = person.IsSelfHighlighted;
    }

    /// <summary>
    /// バーの長さ。出している行の中で被ダメ値が一番大きい被弾を 1 にする。
    /// 足した行で最大が変わったら全部の行を、変わらなければ足した行だけを当て直す。
    /// </summary>
    private void UpdateBarRatios(IReadOnlyList<DamageTakenListEntry> addedEntries)
    {
        var maxValue = _maxHitValue;
        foreach (var entry in addedEntries)
        {
            maxValue = Math.Max(maxValue, entry.Hit.Value);
        }

        var targets = maxValue != _maxHitValue ? (IReadOnlyList<DamageTakenListEntry>)_entries : addedEntries;
        _maxHitValue = maxValue;
        foreach (var entry in targets)
        {
            entry.BarRatio = maxValue > 0
                ? Math.Clamp(entry.Hit.Value / (double)maxValue, 0d, 1d)
                : 0d;
        }
    }

    /// <summary>バーの長さを全部の行から決め直す。「最新の被ダメのみ表示」で行の被弾を入れ替えたとき(最大が下がることがある)に使う。</summary>
    private void UpdateAllBarRatios()
    {
        _maxHitValue = 0;
        UpdateBarRatios(_entries);
    }

    private string CreateLatestHitOnlyActionText() => LocalizationManager.Instance.GetString(
        _settings.ShowsLatestHitOnly ? "DamageTakenList_ShowAllHits" : "DamageTakenList_ShowLatestHitOnly");

    /// <summary>
    /// 行の時刻。タイムラインの行と同じく戦闘の経過(<see cref="Encounter.ToCombatOffset"/>、自動一時停止で止まっていた区間を除く)で、
    /// 起点の前は負。起点がまだ無い間は空欄。
    /// </summary>
    private string FormatTime(DamageTakenListHit hit)
    {
        return _combatStartUtc is not null && _encounter?.ToCombatOffset(hit.Timestamp) is { } offset
            ? MeterWidgetViewModel.FormatDuration(offset)
            : string.Empty;
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

    /// <summary>1人ぶんの名前とクラス。ブラシは使い回すので、中身が同じなら等しい。</summary>
    private sealed record PersonDisplay(
        string DisplayName,
        Brush ClassIconMask,
        Brush ClassBrush,
        bool IsSelfHighlighted);
}
