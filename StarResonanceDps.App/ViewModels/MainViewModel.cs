using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows.Data;
using CommunityToolkit.Mvvm.ComponentModel;
using StarResonanceDps.App.Config;
using StarResonanceDps.App.Localization;
using StarResonanceDps.Core.Models;

namespace StarResonanceDps.App.ViewModels;

public sealed partial class MainViewModel : ViewModelBase
{
    private readonly ObservableCollection<WidgetListItemViewModel> _widgetItems = new();
    private readonly WidgetStateManager _widgetStateManager = WidgetStateManager.Instance;

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
        AddWidget(WidgetKind.DpsMeter, "Menu_DpsMeter", WidgetState.Running);
        AddWidget(WidgetKind.HpsMeter, "Menu_HpsMeter", WidgetState.Running);
        AddWidget(WidgetKind.DtpsMeter, "Menu_DtpsMeter", WidgetState.Stopped);
        AddWidget(WidgetKind.SkillLog, "Menu_SkillDiary", WidgetState.Running);
        AddWidget(WidgetKind.TrainingMode, "Menu_Training", WidgetState.Stopped);
        AddWidget(WidgetKind.PlayerInfoDebug, "Widget_PlayerInfoDebug", WidgetState.Running);

        Widgets = CollectionViewSource.GetDefaultView(_widgetItems);
        Widgets.Filter = FilterWidget;
        ApplyWidgetSort();

        LocalizationManager.Instance.CultureChanged += LocalizationManager_CultureChanged;
    }

    private void AddWidget(WidgetKind kind, string displayNameResourceKey, WidgetState state)
    {
        var widget = new WidgetListItemViewModel
        {
            Kind = kind,
            DisplayNameResourceKey = displayNameResourceKey,
            State = state,
            OriginalIndex = _widgetItems.Count
        };
        widget.RefreshLocalizedText();

        _isLoadingWidgets = true;
        try
        {
            widget.ApplyWidgetConfig(_widgetStateManager.GetWidgetSnapshot(kind));
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

            default:
                return;
        }

        BulkActionIndex = -1;
    }
}
