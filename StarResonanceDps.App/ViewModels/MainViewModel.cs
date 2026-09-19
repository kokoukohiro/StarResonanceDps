using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows;
using System.Windows.Data;
using CommunityToolkit.Mvvm.ComponentModel;
using StarResonanceDps.App.Config;
using StarResonanceDps.App.Localization;
using StarResonanceDps.App.Services;
using StarResonanceDps.App.Models.Widgets;
using StarResonanceDps.Core.CombatRuntime;
using StarResonanceDps.Core.Models;
using StarResonanceDps.Core.Services;

namespace StarResonanceDps.App.ViewModels;

public sealed partial class MainViewModel : ViewModelBase
{
    private readonly ObservableCollection<WidgetListItemViewModel> _widgetItems = new();
    private readonly ObservableCollection<PluginListItemViewModel> _pluginItems = new();
    private readonly ConfigManager _configManager = ConfigManager.Instance;
    private readonly WidgetStateManager _widgetStateManager = WidgetStateManager.Instance;
    private readonly WidgetWindowManager _widgetWindowManager = WidgetWindowManager.Instance;
    private readonly PluginManager _pluginManager = PluginManager.Instance;
    private readonly PlayerRosterPresentationStore _playerRosterStore = PlayerRosterPresentationStore.Instance;
    private readonly PartyStateStore _partyStateStore = PartyStateStore.Instance;
    private readonly NearbyEntityStore _nearbyEntityStore = NearbyEntityStore.Instance;
    private readonly object _playerRosterUpdateSync = new();
    private readonly object _nearbyEntityUpdateSync = new();

    private PlayerRosterSnapshot? _pendingPlayerRosterSnapshot;
    private NearbyEntitySnapshot? _pendingNearbyEntitySnapshot;
    private bool _isPlayerRosterUpdateQueued;
    private bool _isNearbyEntityUpdateQueued;

    private WidgetListItemViewModel? _playerListWidget;
    private WidgetListItemViewModel? _entityListWidget;

    [ObservableProperty]
    private string _searchText = string.Empty;

    [ObservableProperty]
    private int _widgetSortIndex;

    [ObservableProperty]
    private int _widgetActionIndex = -1;

    private bool _isBulkUpdatingWidgets;
    private bool _isLoadingWidgets;

    public ICollectionView Widgets { get; }

    public ReadOnlyObservableCollection<PluginListItemViewModel> PluginItems { get; }

    public MainViewModel()
    {
        var playerListWidget = AddWidget(WidgetKind.PlayerList, "Widget_PlayerList");
        _playerListWidget = playerListWidget;
        var entityListWidget = AddWidget(WidgetKind.EntityList, "Widget_EntityList");
        _entityListWidget = entityListWidget;
        AddWidget(WidgetKind.PlayerInfo, "Widget_PlayerInfo");
        AddWidget(WidgetKind.PlayerStatus, "Widget_PlayerStatus");
        AddWidget(WidgetKind.PlayerEquipment, "Widget_PlayerEquipment");
        AddWidget(WidgetKind.BuffList, "Widget_BuffList");
        AddWidget(WidgetKind.DebuffList, "Widget_DebuffList");
        AddWidget(WidgetKind.BuffDebuffCard, "Widget_BuffDebuffCard");
        playerListWidget.PlayerWindowRequested += PlayerListWidget_PlayerWindowRequested;
        entityListWidget.EntityWindowRequested += EntityListWidget_EntityWindowRequested;
        AddWidget(WidgetKind.DpsMeter, "Menu_DpsMeter");
        AddWidget(WidgetKind.HpsMeter, "Menu_HpsMeter");
        AddWidget(WidgetKind.TakenDamageLog, "Widget_TakenDamageLog");
        AddWidget(WidgetKind.DamageContribution, "Widget_DamageSkillDetails");
        AddWidget(WidgetKind.DamageSummary, "Widget_DamageContribution");
        AddWidget(WidgetKind.DpsGraph, "Widget_DpsGraph");
        AddWidget(WidgetKind.HealingContribution, "Widget_HealingSkillDetails");
        AddWidget(WidgetKind.HealingSummary, "Widget_HealingContribution");
        AddWidget(WidgetKind.HpsGraph, "Widget_HpsGraph");

        Widgets = CollectionViewSource.GetDefaultView(_widgetItems);
        Widgets.Filter = FilterWidget;
        ApplyWidgetSort();

        foreach (var manifest in _pluginManager.DiscoverPlugins())
        {
            AddPluginItem(manifest);
        }

        AddPluginAddItem();
        PluginItems = new ReadOnlyObservableCollection<PluginListItemViewModel>(_pluginItems);

        LocalizationManager.Instance.CultureChanged += LocalizationManager_CultureChanged;
        _configManager.SettingsChanged += ConfigManager_SettingsChanged;
        _configManager.SettingsPreviewChanged += ConfigManager_SettingsPreviewChanged;

        _playerRosterStore.RosterChanged += PlayerRosterPresentationStore_RosterChanged;
        _partyStateStore.Changed += PartyStateStore_Changed;
        _nearbyEntityStore.EntitiesChanged += NearbyEntityStore_EntitiesChanged;

        var roster = _playerRosterStore.Current;
        ApplyPlayerRosterSnapshot(roster);
        ApplyNearbyEntitySnapshot(_nearbyEntityStore.Current);
    }

    private void AddPluginItem(PluginInfo pluginInfo)
    {
        var plugin = new PluginListItemViewModel
        {
            PluginId = pluginInfo.Id,
            OriginalIndex = _pluginItems.Count,
            DisplayNames = new Dictionary<string, string>(pluginInfo.DisplayNames, StringComparer.OrdinalIgnoreCase)
        };

        plugin.RefreshLocalizedText();
        _pluginItems.Add(plugin);
    }

    private void AddPluginAddItem()
    {
        _pluginItems.Add(new PluginListItemViewModel
        {
            OriginalIndex = _pluginItems.Count,
            IsAddItem = true
        });
    }

    private WidgetListItemViewModel AddWidget(WidgetKind kind, string displayNameResourceKey)
    {
        var widget = new WidgetListItemViewModel
        {
            Kind = kind,
            DisplayNameResourceKey = displayNameResourceKey,
            State = WidgetState.Stopped,
            OriginalIndex = _widgetItems.Count
        };
        widget.RefreshLocalizedText();

        _isLoadingWidgets = true;
        try
        {
            var persistedConfig = _widgetStateManager.GetWidgetSnapshot(kind);
            widget.ApplyWidgetConfig(persistedConfig);

            if (persistedConfig.State is null)
            {
                _widgetStateManager.SaveWidgetState(kind, widget.State);
            }
        }
        finally
        {
            _isLoadingWidgets = false;
        }

        widget.PropertyChanged += OnWidgetPropertyChanged;
        _widgetItems.Add(widget);
        _widgetWindowManager.RegisterPlayerWindowWidget(widget);
        return widget;
    }

    private void PlayerListWidget_PlayerWindowRequested(WidgetKind kind, long characterId)
    {
        _widgetWindowManager.OpenPlayerWindow(kind, characterId);
    }

    private void EntityListWidget_EntityWindowRequested(WidgetKind kind, EntityListEntry entity)
    {
        _widgetWindowManager.OpenEntityWindow(kind, entity);
    }

    private void PlayerRosterPresentationStore_RosterChanged(object? sender, PlayerRosterChangedEventArgs e)
    {
        QueuePlayerRosterSnapshot(new PlayerRosterSnapshot(
            e.Snapshot,
            e.MapName,
            e.MapChannel,
            e.MapGeneration));
    }

    private void PartyStateStore_Changed(object? sender, EventArgs e)
    {
        QueuePlayerRosterSnapshot(_playerRosterStore.Current);
    }

    private void QueuePlayerRosterSnapshot(PlayerRosterSnapshot roster)
    {
        lock (_playerRosterUpdateSync)
        {
            _pendingPlayerRosterSnapshot = roster;
            if (_isPlayerRosterUpdateQueued)
            {
                return;
            }

            _isPlayerRosterUpdateQueued = true;
        }

        var dispatcher = Application.Current?.Dispatcher;
        if (dispatcher is null)
        {
            ProcessPendingPlayerRosterSnapshot();
            return;
        }

        dispatcher.BeginInvoke(ProcessPendingPlayerRosterSnapshot);
    }

    private void ProcessPendingPlayerRosterSnapshot()
    {
        PlayerRosterSnapshot? roster;

        lock (_playerRosterUpdateSync)
        {
            roster = _pendingPlayerRosterSnapshot;
            _pendingPlayerRosterSnapshot = null;
            _isPlayerRosterUpdateQueued = false;
        }

        if (roster is not null)
        {
            ApplyPlayerRosterSnapshot(roster);
        }
    }

    private void ApplyPlayerRosterSnapshot(PlayerRosterSnapshot roster)
    {
        _playerListWidget?.UpdatePlayerRoster(
            roster.Entries,
            roster.MapName,
            roster.MapChannel,
            roster.MapGeneration);
        _widgetWindowManager.UpdatePlayerWindowPresentations(roster.Entries);
    }

    private void NearbyEntityStore_EntitiesChanged(object? sender, NearbyEntitiesChangedEventArgs e)
    {
        QueueNearbyEntitySnapshot(new NearbyEntitySnapshot(
            e.Snapshot,
            e.MapName,
            e.MapChannel,
            e.MapGeneration));
    }

    private void QueueNearbyEntitySnapshot(NearbyEntitySnapshot snapshot)
    {
        lock (_nearbyEntityUpdateSync)
        {
            _pendingNearbyEntitySnapshot = snapshot;
            if (_isNearbyEntityUpdateQueued)
            {
                return;
            }

            _isNearbyEntityUpdateQueued = true;
        }

        var dispatcher = Application.Current?.Dispatcher;
        if (dispatcher is null)
        {
            ProcessPendingNearbyEntitySnapshot();
            return;
        }

        dispatcher.BeginInvoke(ProcessPendingNearbyEntitySnapshot);
    }

    private void ProcessPendingNearbyEntitySnapshot()
    {
        NearbyEntitySnapshot? snapshot;

        lock (_nearbyEntityUpdateSync)
        {
            snapshot = _pendingNearbyEntitySnapshot;
            _pendingNearbyEntitySnapshot = null;
            _isNearbyEntityUpdateQueued = false;
        }

        if (snapshot is not null)
        {
            ApplyNearbyEntitySnapshot(snapshot);
        }
    }

    private void ApplyNearbyEntitySnapshot(NearbyEntitySnapshot snapshot)
    {
        if (_entityListWidget is not { } entityListWidget)
        {
            return;
        }

        entityListWidget.UpdateNearbyEntities(
            snapshot.Entries,
            snapshot.MapName,
            snapshot.MapChannel,
            snapshot.MapGeneration);
        _widgetWindowManager.UpdateEntityWindowPresentations(entityListWidget.EntityListEntries);
    }

    private void ConfigManager_SettingsChanged(object? sender, EventArgs e)
    {
        if (Application.Current?.Dispatcher is { } dispatcher && !dispatcher.CheckAccess())
        {
            dispatcher.BeginInvoke(RefreshPlayerRosterPresentation);
            return;
        }

        RefreshPlayerRosterPresentation();
    }

    private void RefreshPlayerRosterPresentation()
    {
        QueuePlayerRosterSnapshot(_playerRosterStore.Current);
    }

    private void LocalizationManager_CultureChanged(object? sender, EventArgs e)
    {
        RefreshLocalizedPresentation();
    }

    /// <summary>
    /// 設定のプレビュー適用。<b>内部IDの表示切り替えを言語切替と同じ扱いで即反映させる</b>ため、
    /// 保存だけでなくプレビューでも組み直す(保存時も同じイベントが上がる)。
    /// </summary>
    private void ConfigManager_SettingsPreviewChanged(object? sender, EventArgs e)
    {
        if (Application.Current?.Dispatcher is { } dispatcher && !dispatcher.CheckAccess())
        {
            dispatcher.BeginInvoke(RefreshLocalizedPresentation);
            return;
        }

        RefreshLocalizedPresentation();
    }

    /// <summary>
    /// 表示中の文字列を組み直す。言語切替と、内部IDの表示切り替えの両方から呼ぶ。
    /// </summary>
    private void RefreshLocalizedPresentation()
    {
        // シーン名は Core が解決済みの文字列で持っているので、言語や表示設定を変えただけでは
        // 追従しない。引き直して投影へ流し直させる。チャンネルの接尾辞は App 側で組むので、
        // これでマップ表示が丸ごと組み直される。
        EncounterManager.RefreshSceneName();

        foreach (var widget in _widgetItems)
        {
            widget.RefreshLocalizedText();
        }

        foreach (var plugin in _pluginItems)
        {
            plugin.RefreshLocalizedText();
        }

        Widgets.Refresh();
    }

    private void OnWidgetPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (sender is not WidgetListItemViewModel widget || _isLoadingWidgets)
        {
            return;
        }

        if (e.PropertyName is nameof(WidgetListItemViewModel.IsFavorite) or nameof(WidgetListItemViewModel.IsPinned))
        {
            _widgetStateManager.SaveWidgetFlags(widget.Kind, widget.IsFavorite, widget.IsPinned);

            if (e.PropertyName == nameof(WidgetListItemViewModel.IsPinned))
            {
                _widgetWindowManager.ApplyWidgetPinState(
                    widget,
                    bringToFront: !_isBulkUpdatingWidgets);
            }
        }

        if (e.PropertyName == nameof(WidgetListItemViewModel.State))
        {
            _widgetStateManager.SaveWidgetState(widget.Kind, widget.State);
            _widgetWindowManager.ApplyWidgetState(widget);
        }

        if (e.PropertyName is not (nameof(WidgetListItemViewModel.DisplayName)
            or nameof(WidgetListItemViewModel.State)
            or nameof(WidgetListItemViewModel.IsFavorite)
            or nameof(WidgetListItemViewModel.IsPinned)))
        {
            return;
        }

        if (_isBulkUpdatingWidgets)
        {
            return;
        }

        Widgets.Refresh();
    }

    public void RestoreRunningWidgetWindows()
    {
        _widgetWindowManager.BeginStartupRestore();

        try
        {
            foreach (var widget in _widgetItems.Where(widget => widget.State == WidgetState.Running))
            {
                _widgetWindowManager.ApplyWidgetState(widget);
            }
        }
        finally
        {
            _widgetWindowManager.EndStartupRestore();
        }
    }

    private bool FilterWidget(object item)
    {
        if (item is not WidgetListItemViewModel widget)
        {
            return false;
        }

        return MatchesSearchText(widget);
    }

    private bool MatchesSearchText(WidgetListItemViewModel widget)
    {
        var searchText = SearchText?.Trim();
        return string.IsNullOrEmpty(searchText)
            || widget.DisplayName.Contains(searchText, StringComparison.CurrentCultureIgnoreCase);
    }

    private void ApplyWidgetSort()
    {
        using (Widgets.DeferRefresh())
        {
            Widgets.SortDescriptions.Clear();

            switch (WidgetSortIndex)
            {
                case 1:
                    Widgets.SortDescriptions.Add(new SortDescription(nameof(WidgetListItemViewModel.DisplayName), ListSortDirection.Ascending));
                    break;

                case 2:
                    Widgets.SortDescriptions.Add(new SortDescription(nameof(WidgetListItemViewModel.IsFavorite), ListSortDirection.Descending));
                    Widgets.SortDescriptions.Add(new SortDescription(nameof(WidgetListItemViewModel.OriginalIndex), ListSortDirection.Ascending));
                    break;

                case 3:
                    Widgets.SortDescriptions.Add(new SortDescription(nameof(WidgetListItemViewModel.IsRunning), ListSortDirection.Descending));
                    Widgets.SortDescriptions.Add(new SortDescription(nameof(WidgetListItemViewModel.OriginalIndex), ListSortDirection.Ascending));
                    break;

                case 4:
                    Widgets.SortDescriptions.Add(new SortDescription(nameof(WidgetListItemViewModel.IsPinned), ListSortDirection.Descending));
                    Widgets.SortDescriptions.Add(new SortDescription(nameof(WidgetListItemViewModel.OriginalIndex), ListSortDirection.Ascending));
                    break;

                default:
                    Widgets.SortDescriptions.Add(new SortDescription(nameof(WidgetListItemViewModel.OriginalIndex), ListSortDirection.Ascending));
                    break;
            }
        }
    }

    private void StartFavoriteWidgets()
    {
        UpdateWidgetStates(_widgetItems.Where(widget => widget.IsFavorite), WidgetState.Running);
    }

    private void StopAllWidgets()
    {
        UpdateWidgetStates(_widgetItems, WidgetState.Stopped);
    }

    private void PinAllRunningWidgets()
    {
        UpdateWidgetPinStates(
            _widgetItems.Where(widget => widget.State == WidgetState.Running),
            isPinned: true);
    }

    private void UnpinAllWidgets()
    {
        UpdateWidgetPinStates(_widgetItems, isPinned: false);
    }

    private void UpdateWidgetStates(IEnumerable<WidgetListItemViewModel> widgets, WidgetState state)
    {
        _isBulkUpdatingWidgets = true;

        try
        {
            foreach (var widget in widgets)
            {
                widget.State = state;
            }
        }
        finally
        {
            _isBulkUpdatingWidgets = false;
        }

        Widgets.Refresh();
    }

    private void UpdateWidgetPinStates(IEnumerable<WidgetListItemViewModel> widgets, bool isPinned)
    {
        _isBulkUpdatingWidgets = true;

        try
        {
            foreach (var widget in widgets)
            {
                widget.IsPinned = isPinned;
            }
        }
        finally
        {
            _isBulkUpdatingWidgets = false;
        }

        Widgets.Refresh();
    }

    partial void OnSearchTextChanged(string value)
    {
        Widgets.Refresh();
    }

    partial void OnWidgetSortIndexChanged(int value)
    {
        ApplyWidgetSort();
    }

    partial void OnWidgetActionIndexChanged(int value)
    {
        switch (value)
        {
            case 0:
                StartFavoriteWidgets();
                break;

            case 1:
                StopAllWidgets();
                break;

            case 2:
                PinAllRunningWidgets();
                break;

            case 3:
                UnpinAllWidgets();
                break;

            default:
                return;
        }
    }
}
