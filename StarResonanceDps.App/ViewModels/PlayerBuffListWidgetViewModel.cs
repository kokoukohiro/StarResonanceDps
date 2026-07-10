using System.Collections.ObjectModel;
using System.Windows.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using StarResonanceDps.App.Localization;
using StarResonanceDps.App.Models.Widgets;
using StarResonanceDps.Core.CombatRuntime;
using StarResonanceDps.Core.Models;

namespace StarResonanceDps.App.ViewModels;

public sealed partial class PlayerBuffListWidgetViewModel : PlayerWidgetWindowViewModel, IDisposable
{
    private readonly PlayerBuffListKind _kind;
    private readonly ObservableCollection<PlayerBuffEntry> _entries = [];
    private readonly DispatcherTimer _refreshTimer;
    private bool _isDisposed;

    [ObservableProperty]
    private string _noDataText = string.Empty;

    [ObservableProperty]
    private bool _hasEntries;

    public PlayerBuffListWidgetViewModel(
        WidgetListItemViewModel playerWidget,
        long? requestedCharacterId,
        PlayerRosterEntry? initialPlayer,
        PlayerBuffListKind kind)
        : base(playerWidget, requestedCharacterId)
    {
        _kind = kind;
        Entries = new ReadOnlyObservableCollection<PlayerBuffEntry>(_entries);
        _refreshTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromSeconds(1)
        };
        _refreshTimer.Tick += RefreshTimer_Tick;
        LocalizationManager.Instance.CultureChanged += LocalizationManager_CultureChanged;
        InitializePlayer(initialPlayer);
        Refresh();
        _refreshTimer.Start();
    }

    public ReadOnlyObservableCollection<PlayerBuffEntry> Entries { get; }

    public void Dispose()
    {
        if (_isDisposed)
        {
            return;
        }

        _isDisposed = true;
        _refreshTimer.Stop();
        _refreshTimer.Tick -= RefreshTimer_Tick;
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

    private void LocalizationManager_CultureChanged(object? sender, EventArgs e)
    {
        Refresh();
    }

    private void Refresh()
    {
        if (_isDisposed)
        {
            return;
        }

        RefreshNoDataText();

        if (SelectedCharacterId is not { } characterId)
        {
            SynchronizeEntries(Array.Empty<PlayerBuffSnapshot>());
            return;
        }

        var playerIdentity = MeterSnapshotProvider.GetPlayerIdentity(characterId);
        if (playerIdentity is not null)
        {
            SetHeaderText(playerIdentity.Name, playerIdentity.UserId);
        }

        SynchronizeEntries(MeterSnapshotProvider.GetPlayerBuffs(characterId, _kind));
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
