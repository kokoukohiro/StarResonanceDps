using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows.Data;
using CommunityToolkit.Mvvm.ComponentModel;
using StarResonanceDps.App.Config;
using StarResonanceDps.App.Localization;
using StarResonanceDps.App.Services;
using StarResonanceDps.Core.Models;

namespace StarResonanceDps.App.ViewModels;

public sealed partial class MainViewModel : ViewModelBase
{
    private readonly ObservableCollection<WidgetListItemViewModel> _widgetItems = new();
    private readonly WidgetStateManager _widgetStateManager = WidgetStateManager.Instance;
    private readonly WidgetWindowManager _widgetWindowManager = WidgetWindowManager.Instance;

    [ObservableProperty]
    private string _searchText = string.Empty;

    [ObservableProperty]
    private int _widgetFilterIndex;

    [ObservableProperty]
    private int _widgetSortIndex;

    [ObservableProperty]
    private int _bulkActionIndex = -1;

    private bool _isBulkUpdatingWidgets;
    private bool _isLoadingWidgets;

    public ICollectionView Widgets { get; }

    public MainViewModel()
    {
        AddWidget(WidgetKind.DpsMeter, "Menu_DpsMeter");
        AddWidget(WidgetKind.HpsMeter, "Menu_HpsMeter");
        AddWidget(WidgetKind.DtpsMeter, "Menu_DtpsMeter");
        AddWidget(WidgetKind.SkillLog, "Menu_SkillDiary");
        AddWidget(WidgetKind.TrainingMode, "Menu_Training");
        AddWidget(WidgetKind.PlayerInfoDebug, "Widget_PlayerInfoDebug");

        Widgets = CollectionViewSource.GetDefaultView(_widgetItems);
        Widgets.Filter = FilterWidget;
        ApplyWidgetSort();

        LocalizationManager.Instance.CultureChanged += LocalizationManager_CultureChanged;
    }

    private void AddWidget(WidgetKind kind, string displayNameResourceKey)
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

            // Existing widget-state documents did not contain State. Persist the
            // stopped initial state without changing any explicitly saved state.
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
    }

    private void LocalizationManager_CultureChanged(object? sender, EventArgs e)
    {
        foreach (var widget in _widgetItems)
        {
            widget.RefreshLocalizedText();
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

        return MatchesSearchText(widget) && MatchesFilter(widget);
    }

    private bool MatchesSearchText(WidgetListItemViewModel widget)
    {
        var searchText = SearchText?.Trim();
        return string.IsNullOrEmpty(searchText)
            || widget.DisplayName.Contains(searchText, StringComparison.CurrentCultureIgnoreCase);
    }

    private bool MatchesFilter(WidgetListItemViewModel widget)
    {
        return WidgetFilterIndex switch
        {
            1 => widget.IsFavorite,
            2 => widget.State == WidgetState.Running,
            3 => widget.State == WidgetState.Stopped,
            _ => true
        };
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

    partial void OnWidgetFilterIndexChanged(int value)
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
