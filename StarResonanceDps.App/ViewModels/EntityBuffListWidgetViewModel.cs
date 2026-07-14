using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using StarResonanceDps.App.Localization;
using StarResonanceDps.App.Models.Widgets;
using StarResonanceDps.Core.CombatRuntime;

namespace StarResonanceDps.App.ViewModels;

public sealed partial class EntityBuffListWidgetViewModel : ViewModelBase, IDisposable
{
    private readonly WidgetListItemViewModel _widget;
    private readonly long _entityUuid;
    private EntityListEntry _entity;
    private readonly PlayerBuffListKind _kind;
    private readonly ObservableCollection<PlayerBuffEntry> _entries = [];
    private readonly DispatcherTimer _refreshTimer;
    private bool _isDisposed;

    [ObservableProperty]
    private string _headerText = string.Empty;

    [ObservableProperty]
    private string _noDataText = string.Empty;

    [ObservableProperty]
    private bool _hasEntries;

    public EntityBuffListWidgetViewModel(
        WidgetListItemViewModel widget,
        EntityListEntry entity,
        PlayerBuffListKind kind)
    {
        _widget = widget;
        _entityUuid = entity.EntityUuid;
        _entity = entity;
        _kind = kind;
        Entries = new ReadOnlyObservableCollection<PlayerBuffEntry>(_entries);
        _refreshTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromSeconds(1)
        };
        _refreshTimer.Tick += RefreshTimer_Tick;
        _entity.PropertyChanged += Entity_PropertyChanged;
        LocalizationManager.Instance.CultureChanged += LocalizationManager_CultureChanged;
        RefreshPresentation();
        Refresh();
        _refreshTimer.Start();
    }

    public long EntityUuid => _entityUuid;

    public ReadOnlyObservableCollection<PlayerBuffEntry> Entries { get; }

    public bool RepresentsEntity(long entityUuid)
    {
        return EntityUuid == entityUuid;
    }

    public void UpdateEntity(EntityListEntry entity)
    {
        if (entity.EntityUuid != EntityUuid
            || ReferenceEquals(_entity, entity))
        {
            return;
        }

        _entity.PropertyChanged -= Entity_PropertyChanged;
        _entity = entity;
        _entity.PropertyChanged += Entity_PropertyChanged;
        RefreshPresentation();
        Refresh();
    }

    public void RefreshPresentation()
    {
        HeaderText = string.IsNullOrWhiteSpace(_entity.DisplayName)
            ? _widget.DisplayName
            : $"{_widget.DisplayName} - {_entity.DisplayName}";
    }

    public void Dispose()
    {
        if (_isDisposed)
        {
            return;
        }

        _isDisposed = true;
        _refreshTimer.Stop();
        _refreshTimer.Tick -= RefreshTimer_Tick;
        _entity.PropertyChanged -= Entity_PropertyChanged;
        LocalizationManager.Instance.CultureChanged -= LocalizationManager_CultureChanged;
    }

    private void RefreshTimer_Tick(object? sender, EventArgs e)
    {
        Refresh();
    }

    private void Entity_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(EntityListEntry.DisplayName))
        {
            RefreshPresentation();
        }
    }

    private void LocalizationManager_CultureChanged(object? sender, EventArgs e)
    {
        RefreshPresentation();
        Refresh();
    }

    private void Refresh()
    {
        if (_isDisposed)
        {
            return;
        }

        RefreshNoDataText();
        SynchronizeEntries(MeterSnapshotProvider.GetEntityBuffs(EntityUuid, _kind));
    }

    private void RefreshNoDataText()
    {
        NoDataText = LocalizationManager.Instance.GetString(_kind == PlayerBuffListKind.Buff
            ? "Widget_NoBuffData"
            : "Widget_NoDebuffData");
    }

    private void SynchronizeEntries(IReadOnlyList<PlayerBuffSnapshot> snapshots)
    {
        var snapshotKeys = snapshots
            .Select(snapshot => snapshot.Key)
            .ToHashSet(StringComparer.Ordinal);

        for (var index = _entries.Count - 1; index >= 0; index--)
        {
            if (snapshotKeys.Contains(_entries[index].Key))
            {
                continue;
            }

            _entries.RemoveAt(index);
        }

        for (var targetIndex = 0; targetIndex < snapshots.Count; targetIndex++)
        {
            var snapshot = snapshots[targetIndex];
            var existingIndex = FindEntryIndex(snapshot.Key);
            if (existingIndex < 0)
            {
                _entries.Insert(targetIndex, new PlayerBuffEntry(snapshot));
                continue;
            }

            var entry = _entries[existingIndex];
            entry.Update(snapshot);
            if (existingIndex != targetIndex)
            {
                _entries.Move(existingIndex, targetIndex);
            }
        }

        HasEntries = _entries.Count > 0;
    }

    private int FindEntryIndex(string key)
    {
        for (var index = 0; index < _entries.Count; index++)
        {
            if (string.Equals(_entries[index].Key, key, StringComparison.Ordinal))
            {
                return index;
            }
        }

        return -1;
    }
}
