using System.Collections.ObjectModel;
using System.Windows.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using StarResonanceDps.App.Localization;
using StarResonanceDps.App.Models.Widgets;
using StarResonanceDps.Core.CombatRuntime;
using StarResonanceDps.Core.Models;

namespace StarResonanceDps.App.ViewModels;

public sealed partial class PlayerSkillInfoWidgetViewModel : PlayerWidgetWindowViewModel, IDisposable
{
    private readonly ObservableCollection<PlayerSkillInfoEntry> _entries = [];
    private readonly DispatcherTimer _refreshTimer;
    private bool _isDisposed;

    [ObservableProperty]
    private string _noDataText = string.Empty;

    [ObservableProperty]
    private bool _hasEntries;

    public PlayerSkillInfoWidgetViewModel(
        WidgetListItemViewModel playerWidget,
        long? requestedCharacterId,
        PlayerRosterEntry? initialPlayer)
        : base(playerWidget, requestedCharacterId)
    {
        Entries = new ReadOnlyObservableCollection<PlayerSkillInfoEntry>(_entries);
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

    public ReadOnlyObservableCollection<PlayerSkillInfoEntry> Entries { get; }

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

        NoDataText = LocalizationManager.Instance.GetString("Widget_NoSkillInfoData");

        if (SelectedCharacterId is not { } characterId)
        {
            SynchronizeEntries(Array.Empty<PlayerSkillInfoSnapshot>());
            return;
        }

        var playerIdentity = MeterSnapshotProvider.GetPlayerIdentity(characterId);
        if (playerIdentity is not null)
        {
            SetHeaderText(playerIdentity.Name, playerIdentity.UserId);
        }

        SynchronizeEntries(MeterSnapshotProvider.GetPlayerSkillInfo(characterId));
    }

    private void SynchronizeEntries(IReadOnlyList<PlayerSkillInfoSnapshot> snapshots)
    {
        for (var index = 0; index < snapshots.Count; index++)
        {
            if (index < _entries.Count)
            {
                _entries[index].Update(snapshots[index]);
            }
            else
            {
                _entries.Add(new PlayerSkillInfoEntry(snapshots[index]));
            }
        }

        while (_entries.Count > snapshots.Count)
        {
            _entries.RemoveAt(_entries.Count - 1);
        }

        HasEntries = _entries.Count > 0;
    }
}
