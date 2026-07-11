using System.Collections.ObjectModel;
using System.Windows.Threading;
using StarResonanceDps.App.Localization;
using StarResonanceDps.App.Models.Widgets;
using StarResonanceDps.App.Services;
using StarResonanceDps.Core.CombatRuntime;
using StarResonanceDps.Core.Models;

namespace StarResonanceDps.App.ViewModels;

public sealed class PlayerImagineRoleSkillWidgetViewModel : PlayerWidgetWindowViewModel, IDisposable
{
    private const int ImagineSlotCount = 2;
    private const int RoleSlotCount = 4;

    private readonly ObservableCollection<PlayerImagineRoleSkillEntry> _imagineEntries = [];
    private readonly ObservableCollection<PlayerImagineRoleSkillEntry> _roleEntries = [];
    private readonly DispatcherTimer _refreshTimer;
    private bool _isDisposed;

    public PlayerImagineRoleSkillWidgetViewModel(
        WidgetListItemViewModel playerWidget,
        long? requestedCharacterId,
        PlayerRosterEntry? initialPlayer)
        : base(playerWidget, requestedCharacterId)
    {
        FillSlots(_imagineEntries, ImagineSlotCount);
        FillSlots(_roleEntries, RoleSlotCount);
        ImagineEntries = new ReadOnlyObservableCollection<PlayerImagineRoleSkillEntry>(_imagineEntries);
        RoleEntries = new ReadOnlyObservableCollection<PlayerImagineRoleSkillEntry>(_roleEntries);

        _refreshTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(250)
        };
        _refreshTimer.Tick += RefreshTimer_Tick;
        LocalizationManager.Instance.CultureChanged += LocalizationManager_CultureChanged;

        InitializePlayer(initialPlayer);
        Refresh();
        _refreshTimer.Start();
    }

    public ReadOnlyObservableCollection<PlayerImagineRoleSkillEntry> ImagineEntries { get; }

    public ReadOnlyObservableCollection<PlayerImagineRoleSkillEntry> RoleEntries { get; }

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

    private static void FillSlots(
        ObservableCollection<PlayerImagineRoleSkillEntry> entries,
        int count)
    {
        for (var index = 0; index < count; index++)
        {
            entries.Add(new PlayerImagineRoleSkillEntry());
        }
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

        if (SelectedCharacterId is not { } characterId)
        {
            UpdateSlots(_imagineEntries, Array.Empty<PlayerCooldownSkillSnapshot>(), 0);
            UpdateSlots(_roleEntries, Array.Empty<PlayerCooldownSkillSnapshot>(), 0);
            return;
        }

        var playerIdentity = MeterSnapshotProvider.GetPlayerIdentity(characterId);
        if (playerIdentity is not null)
        {
            SetHeaderText(playerIdentity.Name, playerIdentity.UserId);
        }

        var snapshot = MeterSnapshotProvider.GetPlayerImagineRoleSkills(characterId);
        UpdateSlots(_imagineEntries, snapshot.ImagineSkills, snapshot.EntityUuid);
        UpdateSlots(_roleEntries, snapshot.RoleSkills, snapshot.EntityUuid);
    }

    private static void UpdateSlots(
        IReadOnlyList<PlayerImagineRoleSkillEntry> entries,
        IReadOnlyList<PlayerCooldownSkillSnapshot> snapshots,
        long entityUuid)
    {
        var now = DateTime.Now;
        for (var index = 0; index < entries.Count; index++)
        {
            var snapshot = index < snapshots.Count
                ? snapshots[index]
                : null;
            var activationTime = snapshot is null || entityUuid == 0
                ? null
                : SkillCooldownTracker.Instance.GetLastActivationTime(entityUuid, snapshot.SkillId);
            entries[index].Update(snapshot, activationTime, now);
        }
    }
}
