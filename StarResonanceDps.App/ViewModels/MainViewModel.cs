using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows;
using System.Windows.Data;
using CommunityToolkit.Mvvm.ComponentModel;
using StarResonanceDps.App.Config;
using StarResonanceDps.App.Localization;
using StarResonanceDps.App.Services;
using StarResonanceDps.App.Models.Widgets;
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
    private readonly object _playerRosterUpdateSync = new();

    private PlayerRosterSnapshot? _pendingPlayerRosterSnapshot;
    private bool _isPlayerRosterUpdateQueued;

    private WidgetListItemViewModel? _playerListWidget;
    private WidgetListItemViewModel? _playerInfoWidget;
    private WidgetListItemViewModel? _playerStatusWidget;
    private WidgetListItemViewModel? _playerEquipmentWidget;

    [ObservableProperty]
    private string _searchText = string.Empty;

    [ObservableProperty]
    private int _widgetSortIndex;

    [ObservableProperty]
    private int _bulkActionIndex = -1;

    private bool _isBulkUpdatingWidgets;
    private bool _isLoadingWidgets;

    public ICollectionView Widgets { get; }

    public ReadOnlyObservableCollection<PluginListItemViewModel> PluginItems { get; }

    public MainViewModel()
    {
        var playerListWidget = AddWidget(WidgetKind.PlayerInfoDebug, "Widget_PlayerList");
        _playerListWidget = playerListWidget;
        _playerInfoWidget = AddWidget(WidgetKind.PlayerInfo, "Widget_PlayerInfo");
        _playerStatusWidget = AddWidget(WidgetKind.PlayerStatus, "Widget_PlayerStatus");
        _playerEquipmentWidget = AddWidget(WidgetKind.PlayerEquipment, "Widget_PlayerEquipment");
        playerListWidget.PlayerWindowRequested += PlayerListWidget_PlayerWindowRequested;
        AddWidget(WidgetKind.DpsMeter, "Menu_DpsMeter");
        AddWidget(WidgetKind.HpsMeter, "Menu_HpsMeter");
        AddWidget(WidgetKind.SkillLog, "Menu_SkillDiary");
        AddWidget(WidgetKind.TrainingMode, "Menu_Training");

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

        _playerRosterStore.RosterChanged += PlayerRosterPresentationStore_RosterChanged;
        var roster = _playerRosterStore.Current;
        ApplyPlayerRosterSnapshot(roster);
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
        return widget;
    }

    private void PlayerListWidget_PlayerWindowRequested(WidgetKind kind, long characterId)
    {
        var widget = kind switch
        {
            WidgetKind.PlayerInfo => _playerInfoWidget,
            WidgetKind.PlayerStatus => _playerStatusWidget,
            WidgetKind.PlayerEquipment => _playerEquipmentWidget,
            _ => null
        };

        if (widget is not null)
        {
            _widgetWindowManager.OpenPlayerWindow(widget, characterId);
        }
    }

    private void PlayerRosterPresentationStore_RosterChanged(object? sender, PlayerRosterChangedEventArgs e)
    {
        QueuePlayerRosterSnapshot(new PlayerRosterSnapshot(
            e.Snapshot,
            e.MapName,
            e.MapGeneration));
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
            roster.MapGeneration);
        _widgetWindowManager.UpdatePlayerWindowPresentations(roster.Entries);
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
        foreach (var widget in _widgetItems.Where(widget => widget.State == WidgetState.Running))
        {
            _widgetWindowManager.ApplyWidgetState(widget);
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

    partial void OnBulkActionIndexChanged(int value)
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

        BulkActionIndex = -1;
    }
}
