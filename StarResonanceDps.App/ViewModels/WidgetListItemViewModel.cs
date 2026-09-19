using System.IO;
using System.Collections.ObjectModel;
using System.Windows.Media;
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

public partial class WidgetListItemViewModel : ViewModelBase
{
    private readonly ObservableCollection<PlayerListEntry> _playerListEntries = [];
    private readonly Dictionary<long, PlayerListEntry> _playerListEntriesByCharacterId = [];
    private readonly ObservableCollection<EntityListEntry> _entityListEntries = [];
    private readonly Dictionary<long, EntityListEntry> _entityListEntriesByUuid = [];
    private WidgetThemeConfig _theme = WidgetConfigDefaults.CreateTheme();
    private MeterWidgetSettingsConfig _meter = WidgetConfigDefaults.CreateMeterSettings(WidgetKind.PlayerList);
    private MetricTimelineWidgetSettingsConfig _metricTimeline = WidgetConfigDefaults.CreateMetricTimelineSettings();
    private BuffCardWidgetSettingsConfig _buffCard = WidgetConfigDefaults.CreateBuffCardSettings();
    private TakenDamageLogWidgetSettingsConfig _takenDamageLog = WidgetConfigDefaults.CreateTakenDamageLogSettings();
    private IReadOnlyList<PlayerRosterEntry> _playerRoster = Array.Empty<PlayerRosterEntry>();
    private IReadOnlyList<NearbyEntityEntry> _nearbyEntities = Array.Empty<NearbyEntityEntry>();
    private string _mapSceneName = string.Empty;
    private uint _mapChannel;
    private long _playerListMapGeneration = -1;
    private long _entityListMapGeneration = -1;

    public WidgetKind Kind { get; init; }

    public int OriginalIndex { get; init; }

    public string DisplayNameResourceKey { get; init; } = string.Empty;

    [ObservableProperty]
    private string _displayName = string.Empty;

    [ObservableProperty]
    private WidgetState _state;

    [ObservableProperty]
    private bool _isFavorite;

    [ObservableProperty]
    private bool _isPinned;

    [ObservableProperty]
    private ThemeColorPalette _themePalette = ThemeColorPalette.Create(Color.FromRgb(0x0B, 0x16, 0x24));

    /// <summary>ピン留め中にヘッダーを隠すか。ウィンドウ側が読む。</summary>
    [ObservableProperty]
    private bool _hideHeaderWhenInactive = true;

    /// <summary>ピン留め中にフッターを隠すか。ウィンドウ側が読む。</summary>
    [ObservableProperty]
    private bool _hideFooterWhenInactive;

    [ObservableProperty]
    private WidgetWindowThemePalette _widgetWindowPalette = WidgetWindowThemePalette.Create(Color.FromRgb(0x0B, 0x16, 0x24), 50);

    [ObservableProperty]
    private string? _backgroundImagePath;

    [ObservableProperty]
    private bool _hasBackgroundImage;

    [ObservableProperty]
    private string _mapName = string.Empty;

    [ObservableProperty]
    private int _openPlayerWindowCount;

    public ReadOnlyObservableCollection<PlayerListEntry> PlayerListEntries { get; }

    public ReadOnlyObservableCollection<EntityListEntry> EntityListEntries { get; }

    public bool IsPlayerList => Kind == WidgetKind.PlayerList;

    public bool IsEntityList => Kind == WidgetKind.EntityList;

    public bool IsPlayerInfo => Kind == WidgetKind.PlayerInfo;

    public bool IsPlayerStatus => Kind == WidgetKind.PlayerStatus;

    public bool IsPlayerEquipment => Kind == WidgetKind.PlayerEquipment;

    public bool IsBuffDebuffCard => Kind == WidgetKind.BuffDebuffCard;

    public bool IsPlayerWindowWidget => Kind is WidgetKind.PlayerInfo
        or WidgetKind.PlayerStatus
        or WidgetKind.PlayerEquipment
        or WidgetKind.BuffList
        or WidgetKind.DebuffList
        or WidgetKind.BuffDebuffCard
        or WidgetKind.DamageContribution
        or WidgetKind.DamageSummary
        or WidgetKind.DpsGraph
        or WidgetKind.HealingContribution
        or WidgetKind.HealingSummary
        or WidgetKind.HpsGraph;

    public bool HasOpenPlayerWindows => IsPlayerWindowWidget && OpenPlayerWindowCount > 0;

    public bool ShowsPlayerWindowCountBadge => IsPlayerWindowWidget && !IsPlayerStatus;

    public MeterWidgetSettingsConfig GetMeterSettingsSnapshot()
    {
        return WidgetConfigDefaults.CloneNormalizedMeter(Kind, _meter);
    }

    public MetricTimelineWidgetSettingsConfig GetMetricTimelineSettingsSnapshot()
    {
        return WidgetConfigDefaults.CloneNormalizedMetricTimeline(_metricTimeline);
    }

    public BuffCardWidgetSettingsConfig GetBuffCardSettingsSnapshot()
    {
        return WidgetConfigDefaults.CloneNormalizedBuffCard(_buffCard);
    }

    public TakenDamageLogWidgetSettingsConfig GetTakenDamageLogSettingsSnapshot()
    {
        return WidgetConfigDefaults.CloneNormalizedTakenDamageLog(_takenDamageLog);
    }

    /// <summary>カードの表示書式。倍率辞書を丸ごと複製しないよう、これだけ直に返す。</summary>
    public string BuffInfoFormatString =>
        _buffCard.BuffInfoFormatString ?? WidgetConfigDefaults.DefaultBuffInfoFormatString;

    /// <summary>保存済みの倍率。無ければ既定(200%)。</summary>
    public int GetBuffCardScale(string scaleKey)
    {
        return !string.IsNullOrWhiteSpace(scaleKey)
            && _buffCard.Scales.TryGetValue(scaleKey, out var scale)
            ? WidgetConfigDefaults.ClampBuffCardScale(scale)
            : WidgetConfigDefaults.DefaultBuffCardScale;
    }

    /// <summary>
    /// 倍率を1件だけ書き戻す。<b>設定一式は触らない</b>ので、開いている他のカードに影響しない。
    /// </summary>
    public void SaveBuffCardScale(string scaleKey, int scale)
    {
        if (!IsBuffDebuffCard || string.IsNullOrWhiteSpace(scaleKey))
        {
            return;
        }

        var normalized = WidgetConfigDefaults.ClampBuffCardScale(scale);
        _buffCard.Scales[scaleKey] = normalized;
        WidgetStateManager.Instance.SaveBuffCardScale(Kind, scaleKey, normalized);
    }

    public string StateText => State == WidgetState.Running
        ? LocalizationManager.Instance.GetString("Widget_State_Running")
        : LocalizationManager.Instance.GetString("Widget_State_Stopped");

    public bool IsRunning => State == WidgetState.Running;

    public event Action<WidgetKind, long>? PlayerWindowRequested;

    public event Action<WidgetKind, EntityListEntry>? EntityWindowRequested;

    public event EventHandler? PlayerWindowPresentationChanged;

    public event EventHandler? MeterSettingsChanged;

    public event EventHandler? TakenDamageLogSettingsChanged;

    public WidgetListItemViewModel()
    {
        PlayerListEntries = new ReadOnlyObservableCollection<PlayerListEntry>(_playerListEntries);
        EntityListEntries = new ReadOnlyObservableCollection<EntityListEntry>(_entityListEntries);
    }

    /// <summary>
    /// マップ名を組み立てる。チャンネルの表記は言語ごとに違う(中国語は「1线」)ので、
    /// <b>素の名前と番号を控えておき、言語が変わったら組み直す。</b>
    /// </summary>
    private void ApplyMapName(string? mapName, uint mapChannel)
    {
        _mapSceneName = mapName ?? string.Empty;
        _mapChannel = mapChannel;
        RenderMapName();
    }

    private void RenderMapName()
    {
        MapName = _mapChannel > 0 && !string.IsNullOrEmpty(_mapSceneName)
            ? $"{_mapSceneName} {LocalizationManager.Instance.Format("Map_ChannelFormat", _mapChannel)}"
            : _mapSceneName;
    }

    public void RefreshLocalizedText()
    {
        DisplayName = LocalizationManager.Instance.GetString(DisplayNameResourceKey);
        RenderMapName();
        OnPropertyChanged(nameof(StateText));
        SynchronizePlayerListEntries(resetEntries: false);
        SynchronizeEntityListEntries(resetEntries: false);
        RaisePlayerWindowPresentationChanged();
    }

    public WidgetConfig CreateWidgetConfig()
    {
        return new WidgetConfig
        {
            IsFavorite = IsFavorite,
            IsPinned = IsPinned,
            State = State,
            Theme = _theme.Clone(),
            Meter = WidgetConfigDefaults.SupportsMeterSettings(Kind)
                ? _meter.Clone()
                : null,
            MetricTimeline = WidgetConfigDefaults.SupportsMetricTimelineSettings(Kind)
                ? _metricTimeline.Clone()
                : null,
            BuffCard = WidgetConfigDefaults.SupportsBuffCardSettings(Kind)
                ? _buffCard.Clone()
                : null,
            TakenDamageLog = WidgetConfigDefaults.SupportsTakenDamageLogSettings(Kind)
                ? _takenDamageLog.Clone()
                : null
        };
    }

    public void ApplyWidgetConfig(WidgetConfig config)
    {
        WidgetConfigDefaults.Normalize(Kind, config);

        IsFavorite = config.IsFavorite;
        IsPinned = config.IsPinned;

        if (config.State is { } state)
        {
            State = state;
        }

        ApplyTheme(config.Theme);
        if (WidgetConfigDefaults.SupportsMeterSettings(Kind))
        {
            _meter = WidgetConfigDefaults.CloneNormalizedMeter(Kind, config.Meter);
        }

        if (WidgetConfigDefaults.SupportsMetricTimelineSettings(Kind))
        {
            _metricTimeline = WidgetConfigDefaults.CloneNormalizedMetricTimeline(config.MetricTimeline);
            RaisePlayerWindowPresentationChanged();
        }

        if (WidgetConfigDefaults.SupportsBuffCardSettings(Kind))
        {
            _buffCard = WidgetConfigDefaults.CloneNormalizedBuffCard(config.BuffCard);
            RaisePlayerWindowPresentationChanged();
        }

        if (WidgetConfigDefaults.SupportsTakenDamageLogSettings(Kind))
        {
            _takenDamageLog = WidgetConfigDefaults.CloneNormalizedTakenDamageLog(config.TakenDamageLog);
            TakenDamageLogSettingsChanged?.Invoke(this, EventArgs.Empty);
        }

        SynchronizePlayerListEntries(resetEntries: false);
        SynchronizeEntityListEntries(resetEntries: false);
        MeterSettingsChanged?.Invoke(this, EventArgs.Empty);
    }

    public void ApplyMeterSettingsPreview(MeterWidgetSettingsConfig meter)
    {
        if (!WidgetConfigDefaults.SupportsMeterSettings(Kind))
        {
            return;
        }

        _meter = WidgetConfigDefaults.CloneNormalizedMeter(Kind, meter);
        SynchronizePlayerListEntries(resetEntries: false);
        SynchronizeEntityListEntries(resetEntries: false);
        MeterSettingsChanged?.Invoke(this, EventArgs.Empty);
    }

    public void ApplyMetricTimelineSettingsPreview(MetricTimelineWidgetSettingsConfig metricTimeline)
    {
        if (!WidgetConfigDefaults.SupportsMetricTimelineSettings(Kind))
        {
            return;
        }

        _metricTimeline = WidgetConfigDefaults.CloneNormalizedMetricTimeline(metricTimeline);
        RaisePlayerWindowPresentationChanged();
    }

    public void ApplyBuffCardSettingsPreview(BuffCardWidgetSettingsConfig buffCard)
    {
        if (!WidgetConfigDefaults.SupportsBuffCardSettings(Kind))
        {
            return;
        }

        _buffCard = WidgetConfigDefaults.CloneNormalizedBuffCard(buffCard);
        RaisePlayerWindowPresentationChanged();
    }

    public void ApplyTakenDamageLogSettingsPreview(TakenDamageLogWidgetSettingsConfig takenDamageLog)
    {
        if (!WidgetConfigDefaults.SupportsTakenDamageLogSettings(Kind))
        {
            return;
        }

        _takenDamageLog = WidgetConfigDefaults.CloneNormalizedTakenDamageLog(takenDamageLog);
        TakenDamageLogSettingsChanged?.Invoke(this, EventArgs.Empty);
    }

    public void ApplyTheme(WidgetThemeConfig theme)
    {
        var normalized = WidgetConfigDefaults.CloneNormalizedTheme(theme);

        _theme = normalized.Clone();

        var selectedHex = normalized.WindowColors[
            Math.Clamp(normalized.WindowColorIndex, 0, normalized.WindowColors.Count - 1)];

        if (!ColorUtilities.TryParseHex(selectedHex, out var windowSurface))
        {
            windowSurface = Color.FromRgb(0x0B, 0x16, 0x24);
        }

        var hasBackgroundImage = !string.IsNullOrWhiteSpace(normalized.BackgroundImagePath)
            && File.Exists(normalized.BackgroundImagePath);
        Color? backgroundImageAverageColor = null;
        if (hasBackgroundImage
            && string.Equals(
                normalized.BackgroundImageAverageColorSourcePath,
                normalized.BackgroundImagePath,
                StringComparison.OrdinalIgnoreCase)
            && ColorUtilities.TryParseHex(normalized.BackgroundImageAverageColor, out var parsedAverageColor))
        {
            backgroundImageAverageColor = parsedAverageColor;
        }

        ThemePalette = ThemeColorPalette.Create(windowSurface);
        WidgetWindowPalette = WidgetWindowThemePalette.Create(
            windowSurface,
            normalized.WindowOpacity,
            backgroundImageAverageColor);

        HasBackgroundImage = hasBackgroundImage;
        BackgroundImagePath = hasBackgroundImage
            ? normalized.BackgroundImagePath
            : null;

        HideHeaderWhenInactive = normalized.HideHeaderWhenInactive;
        HideFooterWhenInactive = normalized.HideFooterWhenInactive;
    }

    public void UpdatePlayerRoster(
        IReadOnlyList<PlayerRosterEntry> playerRoster,
        string mapName,
        uint mapChannel,
        long mapGeneration)
    {
        if (!IsPlayerList)
        {
            return;
        }

        ApplyMapName(mapName, mapChannel);
        _playerRoster = playerRoster;

        var resetEntries = _playerListMapGeneration != mapGeneration;
        _playerListMapGeneration = mapGeneration;
        SynchronizePlayerListEntries(resetEntries);
    }

    public void UpdateNearbyEntities(
        IReadOnlyList<NearbyEntityEntry> nearbyEntities,
        string mapName,
        uint mapChannel,
        long mapGeneration)
    {
        if (!IsEntityList)
        {
            return;
        }

        ApplyMapName(mapName, mapChannel);
        _nearbyEntities = nearbyEntities;

        var resetEntries = _entityListMapGeneration != mapGeneration;
        _entityListMapGeneration = mapGeneration;
        SynchronizeEntityListEntries(resetEntries);
    }

    public void SetOpenPlayerWindowCount(int count)
    {
        OpenPlayerWindowCount = Math.Max(count, 0);
    }

    public void RefreshPlayerListEntries()
    {
        SynchronizePlayerListEntries(resetEntries: false);
    }

    public void RefreshPlayerListSkillEntries(bool refreshEffects)
    {
        if (!IsPlayerList)
        {
            return;
        }

        foreach (var entry in _playerListEntries)
        {
            entry.RefreshSkillDisplay(refreshEffects);
        }
    }

    [RelayCommand]
    private void ToggleFavorite()
    {
        IsFavorite = !IsFavorite;
    }

    [RelayCommand]
    private void TogglePin()
    {
        IsPinned = !IsPinned;
    }

    [RelayCommand]
    private void ToggleRunning()
    {
        State = State == WidgetState.Running
            ? WidgetState.Stopped
            : WidgetState.Running;
    }

    [RelayCommand]
    private void RequestPlayerInfo(PlayerListEntry? player)
    {
        RequestPlayerWindow(WidgetKind.PlayerInfo, player);
    }

    [RelayCommand]
    private void RequestPlayerStatus(PlayerListEntry? player)
    {
        RequestPlayerWindow(WidgetKind.PlayerStatus, player);
    }

    [RelayCommand]
    private void RequestPlayerEquipment(PlayerListEntry? player)
    {
        RequestPlayerWindow(WidgetKind.PlayerEquipment, player);
    }

    [RelayCommand]
    private void RequestBuffList(PlayerListEntry? player)
    {
        RequestPlayerWindow(WidgetKind.BuffList, player);
    }

    [RelayCommand]
    private void RequestDebuffList(PlayerListEntry? player)
    {
        RequestPlayerWindow(WidgetKind.DebuffList, player);
    }

    [RelayCommand]
    private void RequestEntityBuffList(EntityListEntry? entity)
    {
        RequestEntityWindow(WidgetKind.BuffList, entity);
    }

    [RelayCommand]
    private void RequestEntityDebuffList(EntityListEntry? entity)
    {
        RequestEntityWindow(WidgetKind.DebuffList, entity);
    }

    partial void OnStateChanged(WidgetState value)
    {
        OnPropertyChanged(nameof(StateText));
        OnPropertyChanged(nameof(IsRunning));
    }

    partial void OnOpenPlayerWindowCountChanged(int value)
    {
        OnPropertyChanged(nameof(HasOpenPlayerWindows));
    }

    private void RequestPlayerWindow(WidgetKind kind, PlayerListEntry? player)
    {
        if (!IsPlayerList || player is null)
        {
            return;
        }

        PlayerWindowRequested?.Invoke(kind, player.CharacterId);
    }

    private void RequestEntityWindow(WidgetKind kind, EntityListEntry? entity)
    {
        if (!IsEntityList || entity is null)
        {
            return;
        }

        EntityWindowRequested?.Invoke(kind, entity);
    }

    private void SynchronizePlayerListEntries(bool resetEntries)
    {
        if (!IsPlayerList)
        {
            return;
        }

        var visibleRoster = GetVisiblePlayerRoster();

        if (resetEntries)
        {
            foreach (var entry in _playerListEntries)
            {
                entry.IsPlayerSelectionMenuOpen = false;
            }

            _playerListEntriesByCharacterId.Clear();
            _playerListEntries.Clear();
        }
        else
        {
            var activeCharacterIds = visibleRoster
                .Select(entry => entry.CharacterId)
                .ToHashSet();

            for (var index = _playerListEntries.Count - 1; index >= 0; index--)
            {
                var entry = _playerListEntries[index];
                if (activeCharacterIds.Contains(entry.CharacterId))
                {
                    continue;
                }

                entry.IsPlayerSelectionMenuOpen = false;
                _playerListEntriesByCharacterId.Remove(entry.CharacterId);
                _playerListEntries.RemoveAt(index);
            }
        }

        var globalSettings = ConfigManager.Instance.GetSettingsSnapshot();
        var playerNameDisplayMode = (PlayerNameDisplayMode)globalSettings.PlayerNameDisplayModeIndex;

        for (var targetIndex = 0; targetIndex < visibleRoster.Count; targetIndex++)
        {
            var player = visibleRoster[targetIndex];
            if (!_playerListEntriesByCharacterId.TryGetValue(player.CharacterId, out var entry))
            {
                entry = PlayerListEntry.Create(player, _meter, playerNameDisplayMode);
                _playerListEntriesByCharacterId.Add(player.CharacterId, entry);
                _playerListEntries.Insert(targetIndex, entry);
                continue;
            }

            entry.Update(player, _meter, playerNameDisplayMode);

            if (_playerListEntries[targetIndex].CharacterId == player.CharacterId)
            {
                continue;
            }

            var currentIndex = FindPlayerListEntryIndex(player.CharacterId, targetIndex + 1);
            if (currentIndex >= 0)
            {
                _playerListEntries.Move(currentIndex, targetIndex);
            }
        }
    }

    private IReadOnlyList<PlayerRosterEntry> GetVisiblePlayerRoster()
    {
        var mode = (PartyDisplayMode)_meter.PartyDisplayModeIndex;
        var party = PartyStateStore.Instance.Current;
        var visibleRoster = _playerRoster
            .Where(entry => party.ShouldInclude(entry.CharacterId, entry.IsSelf, mode))
            .ToArray();
        if (mode == PartyDisplayMode.NonPartyMembersOnly)
        {
            return SortNonPartyByName(visibleRoster);
        }

        var entriesByCharacterId = visibleRoster
            .GroupBy(entry => entry.CharacterId)
            .ToDictionary(group => group.Key, group => group.Last());
        var orderedPartyEntries = party.OrderedCharacterIds
            .Where(entriesByCharacterId.ContainsKey)
            .Select(characterId => entriesByCharacterId[characterId])
            .ToArray();
        var partyCharacterIds = party.OrderedCharacterIds.ToHashSet();
        return SortNonPartyByName(orderedPartyEntries
            .Concat(visibleRoster.Where(entry => !partyCharacterIds.Contains(entry.CharacterId)))
            .ToArray());
    }

    /// <summary>
    /// 並び替えが名前順のときに、<b>自分とパーティ以外</b>を名前順にする。
    /// 自分とパーティ(PT番号順)の並びと、PT外の 灰色 → ライブ の群は守る。
    /// 名前は画面に出しているものと同じ(伏せ字・NPC の職業名込み)。同じ名前どうしは元の並び(発見順)のまま。
    /// </summary>
    private IReadOnlyList<PlayerRosterEntry> SortNonPartyByName(IReadOnlyList<PlayerRosterEntry> roster)
    {
        if (!SortsListByName)
        {
            return roster;
        }

        var nameDisplayMode = (PlayerNameDisplayMode)ConfigManager.Instance
            .GetSettingsSnapshot()
            .PlayerNameDisplayModeIndex;

        return
        [
            .. roster.Where(entry => entry.IsSelf || entry.IsPartyMember),
            .. roster
                .Where(entry => !entry.IsSelf && !entry.IsPartyMember)
                .OrderBy(PlayerRosterStore.GetGroupRank)
                .ThenBy(
                    entry => PlayerInfoFormatFormatter.GetDisplayName(
                        entry.Name,
                        entry.CharacterId,
                        entry.IsSelf,
                        entry.IsNpc,
                        entry.ProfessionId,
                        nameDisplayMode),
                    StringComparer.CurrentCulture)
        ];
    }

    private int FindPlayerListEntryIndex(long characterId, int startIndex)
    {
        for (var index = startIndex; index < _playerListEntries.Count; index++)
        {
            if (_playerListEntries[index].CharacterId == characterId)
            {
                return index;
            }
        }

        return -1;
    }

    private void SynchronizeEntityListEntries(bool resetEntries)
    {
        if (!IsEntityList)
        {
            return;
        }

        var visibleEntities = GetVisibleNearbyEntities();

        if (resetEntries)
        {
            foreach (var entry in _entityListEntries)
            {
                entry.IsEntitySelectionMenuOpen = false;
            }

            _entityListEntriesByUuid.Clear();
            _entityListEntries.Clear();
        }
        else
        {
            var activeEntityUuids = visibleEntities
                .Select(entry => entry.EntityUuid)
                .ToHashSet();

            for (var index = _entityListEntries.Count - 1; index >= 0; index--)
            {
                var entry = _entityListEntries[index];
                if (activeEntityUuids.Contains(entry.EntityUuid))
                {
                    continue;
                }

                entry.IsEntitySelectionMenuOpen = false;
                _entityListEntriesByUuid.Remove(entry.EntityUuid);
                _entityListEntries.RemoveAt(index);
            }
        }

        for (var targetIndex = 0; targetIndex < visibleEntities.Count; targetIndex++)
        {
            var entity = visibleEntities[targetIndex];
            if (!_entityListEntriesByUuid.TryGetValue(entity.EntityUuid, out var entry))
            {
                entry = EntityListEntry.Create(entity, _meter);
                _entityListEntriesByUuid.Add(entity.EntityUuid, entry);
                _entityListEntries.Insert(targetIndex, entry);
                continue;
            }

            entry.Update(entity, _meter);

            if (_entityListEntries[targetIndex].EntityUuid == entity.EntityUuid)
            {
                continue;
            }

            var currentIndex = FindEntityListEntryIndex(entity.EntityUuid, targetIndex + 1);
            if (currentIndex >= 0)
            {
                _entityListEntries.Move(currentIndex, targetIndex);
            }
        }
    }

    /// <summary>
    /// 一覧に載せる実体。フィルター「オブジェクト以外」は、さらにプレイヤーに見える HP バーを持つ実体だけを残す
    /// (名前はあっても HP バーの見えない実体がいる)。
    /// </summary>
    private IReadOnlyList<NearbyEntityEntry> GetVisibleNearbyEntities()
    {
        var hidesObjects = (EntityDisplayMode)_meter.EntityDisplayModeIndex == EntityDisplayMode.HideObjects;
        var visibleEntities = _nearbyEntities
            .Where(entry => IsListed(entry) && (!hidesObjects || entry.HasHpBar))
            .ToArray();

        if (!SortsListByName)
        {
            return visibleEntities;
        }

        // ボス → 精鋭 → 普通 の群は守り、その中だけを名前順にする。
        // 並べ替えは安定なので、同じ名前どうしは元の並び(発見順)のまま。
        return [.. visibleEntities
            .OrderBy(NearbyEntityStore.GetSortRank)
            .ThenBy(EntityListEntry.ResolveName, StringComparer.CurrentCulture)];
    }

    /// <summary>
    /// 並び替えの設定が名前順か。名前は画面に出しているものを使うので、並べ替えは App 側で行う
    /// (Core で並べると表示言語の切り替えに追従しない)。
    /// </summary>
    private bool SortsListByName =>
        _meter.ListSortModeIndex == WidgetConfigDefaults.NameListSortModeIndex;

    /// <summary>
    /// 一覧に載せるか。
    ///
    /// <para>
    /// <c>AttrId</c> が未着(種別IDが 0)の実体は載せる(名前は「未知の敵」。<see cref="EntityListEntry"/>)。
    /// <c>AttrId</c> は出現の通知でしか届かないので、アプリの起動前から居た実体はずっと種別が分からない。
    /// 種別IDを UUID の通し番号で代えない(個体の番号なので、表を引くと別のモンスターの名前・判定になる)。
    /// </para>
    ///
    /// <para>
    /// プレイヤーに見える HP バーを持つ実体は、名前が無くても載せる(名前は「敵」「味方」。<see cref="EntityListEntry"/>)。
    /// HP バーを持たない実体は、名前があるときだけ載せる。名前は表示と同じ引き方(表示言語、空なら zh-CN)で見る。
    /// </para>
    ///
    /// <para>
    /// <b>表に行が無い実体は載せる</b>(名前は空欄で、内部ID注記だけ)。行が無いのはアプリの表が古いということで、
    /// 消すと表の欠けに気付けない。
    /// </para>
    /// </summary>
    private static bool IsListed(NearbyEntityEntry entry)
    {
        return entry.EntityId == 0
            || entry.HasHpBar
            || !CombatDataCatalog.HasEntityRow(entry.EntityType, entry.EntityId)
            || CombatDataCatalog.HasEntityName(entry.EntityType, entry.EntityId);
    }

    private int FindEntityListEntryIndex(long entityUuid, int startIndex)
    {
        for (var index = startIndex; index < _entityListEntries.Count; index++)
        {
            if (_entityListEntries[index].EntityUuid == entityUuid)
            {
                return index;
            }
        }

        return -1;
    }

    private void RaisePlayerWindowPresentationChanged()
    {
        if (IsPlayerWindowWidget)
        {
            PlayerWindowPresentationChanged?.Invoke(this, EventArgs.Empty);
        }
    }
}
