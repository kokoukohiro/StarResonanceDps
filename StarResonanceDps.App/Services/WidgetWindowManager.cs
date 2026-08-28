using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Windows;
using StarResonanceDps.App.Config;
using StarResonanceDps.App.Models.Widgets;
using StarResonanceDps.App.ViewModels;
using StarResonanceDps.Core.Models;
using StarResonanceDps.Core.CombatRuntime;
using StarResonanceDps.Core.Services;
using StarResonanceDps.App.Views.Widgets;

namespace StarResonanceDps.App.Services;

public sealed class WidgetWindowManager
{
    private const double PlayerWindowCascadeOffset = 20d;

    private readonly Dictionary<WidgetKind, WidgetWindow> _openSingleWindows = new();
    private readonly List<PlayerWidgetWindowSession> _openPlayerWindows = [];
    private readonly List<EntityBuffListWindowSession> _openEntityBuffWindows = [];
    private readonly Dictionary<WidgetKind, WidgetListItemViewModel> _trackedPlayerWidgets = new();
    private Window? _managerWindow;
    private bool _isManagerClosing;

    private WidgetWindowManager()
    {
    }

    public static WidgetWindowManager Instance { get; } = new();

    public void RegisterPlayerWindowWidget(WidgetListItemViewModel widget)
    {
        if (IsPlayerWindowWidget(widget.Kind))
        {
            TrackPlayerWidget(widget);
        }
    }

    public void OpenPlayerWindow(WidgetKind kind, long characterId)
    {
        if (_trackedPlayerWidgets.TryGetValue(kind, out var playerWidget))
        {
            OpenPlayerWindow(playerWidget, characterId);
        }
    }

    public void OpenEntityWindow(WidgetKind kind, EntityListEntry entity)
    {
        if (kind is not (WidgetKind.BuffList or WidgetKind.DebuffList)
            || !_trackedPlayerWidgets.TryGetValue(kind, out var widget))
        {
            return;
        }

        TrackPlayerWidget(widget);

        var existingWindow = _openEntityBuffWindows
            .FirstOrDefault(session =>
                ReferenceEquals(session.Widget, widget)
                && session.ViewModel.RepresentsEntity(entity.EntityUuid));

        if (existingWindow is not null)
        {
            existingWindow.ViewModel.UpdateEntity(entity);
            RestoreAndActivate(existingWindow.Window);
            return;
        }

        CreateEntityBuffWindow(widget, entity);
    }

    public void ApplyWidgetState(WidgetListItemViewModel widget)
    {
        if (IsPlayerWindowWidget(widget.Kind))
        {
            ApplyPlayerWindowWidgetState(widget);
            return;
        }

        if (widget.State == WidgetState.Running)
        {
            Open(widget);
            return;
        }

        Close(widget.Kind);
    }

    public void ApplyWidgetPinState(WidgetListItemViewModel widget, bool bringToFront)
    {
        if (IsPlayerWindowWidget(widget.Kind))
        {
            var targetWindows = _openPlayerWindows
                .Where(session => ReferenceEquals(session.Widget, widget))
                .Select(session => session.Window)
                .Concat(_openEntityBuffWindows
                    .Where(session => ReferenceEquals(session.Widget, widget))
                    .Select(session => session.Window))
                .ToArray();

            foreach (var targetWindow in targetWindows)
            {
                targetWindow.ApplyPinState(widget.IsPinned);
            }

            if (widget.IsPinned && bringToFront && targetWindows.LastOrDefault() is { } lastTargetWindow)
            {
                Activate(lastTargetWindow);
            }

            return;
        }

        if (!_openSingleWindows.TryGetValue(widget.Kind, out var window))
        {
            return;
        }

        window.ApplyPinState(widget.IsPinned);

        if (widget.IsPinned && bringToFront)
        {
            Activate(window);
        }
    }

    public void OpenPlayerWindow(WidgetListItemViewModel playerWidget, long characterId)
    {
        if (!IsPlayerWindowWidget(playerWidget.Kind))
        {
            return;
        }

        TrackPlayerWidget(playerWidget);

        if (playerWidget.Kind == WidgetKind.PlayerStatus)
        {
            var openStatusWindow = _openPlayerWindows
                .FirstOrDefault(session => ReferenceEquals(session.Widget, playerWidget));

            if (openStatusWindow is not null)
            {
                RestoreAndActivate(openStatusWindow.Window);
                return;
            }

            CreatePlayerWindow(playerWidget, requestedCharacterId: null);
            return;
        }

        var existingWindow = _openPlayerWindows
            .FirstOrDefault(session =>
                ReferenceEquals(session.Widget, playerWidget)
                && session.ViewModel.RepresentsPlayer(characterId));

        if (existingWindow is not null)
        {
            RestoreAndActivate(existingWindow.Window);
            return;
        }

        CreatePlayerWindow(playerWidget, characterId);
    }

    private void ApplyPlayerWindowWidgetState(WidgetListItemViewModel widget)
    {
        TrackPlayerWidget(widget);

        if (widget.State == WidgetState.Running)
        {
            if (HasOpenTargetWindows(widget))
            {
                UpdatePlayerWindowCount(widget);
                return;
            }

            CreatePlayerWindow(widget, requestedCharacterId: null);
            return;
        }

        ClosePlayerWindows(widget);
    }

    private void Open(WidgetListItemViewModel widget)
    {
        if (_openSingleWindows.TryGetValue(widget.Kind, out var existingWindow))
        {
            existingWindow.ApplyPinState(widget.IsPinned);
            RestoreAndActivate(existingWindow);
            return;
        }

        var owner = Application.Current?.MainWindow;
        TrackManagerWindow(owner);

        var savedBounds = WidgetStateManager.Instance.GetWidgetSnapshot(widget.Kind).Window;
        var composition = CreateWidgetWindowComposition(widget);
        var window = new WidgetWindow(
            widget,
            composition.Content,
            savedBounds,
            owner,
            headerChromeActions: composition.HeaderChromeActions,
            headerActions: composition.HeaderActions,
            footerContent: composition.FooterContent);
        window.Closed += WidgetWindow_Closed;

        _openSingleWindows.Add(widget.Kind, window);
        window.Show();
    }

    private void CreatePlayerWindow(WidgetListItemViewModel playerWidget, long? requestedCharacterId)
    {
        var owner = Application.Current?.MainWindow;
        TrackManagerWindow(owner);

        var roster = PlayerRosterPresentationStore.Instance.Current.Entries;
        var initialPlayer = ResolvePlayer(requestedCharacterId, roster);
        var playerWindowViewModel = CreatePlayerWindowViewModel(
            playerWidget,
            requestedCharacterId,
            initialPlayer);
        var content = CreatePlayerWindowContent(playerWindowViewModel);
        var savedBounds = WidgetStateManager.Instance.GetWidgetSnapshot(playerWidget.Kind).Window;
        var window = new WidgetWindow(
            playerWidget,
            content,
            savedBounds,
            owner,
            playerWindowViewModel.HeaderText);
        playerWindowViewModel.PropertyChanged += PlayerWindowViewModel_PropertyChanged;
        window.Closed += WidgetWindow_Closed;

        var cascadeIndex = CountOpenTargetWindows(playerWidget);
        ApplyPlayerWindowCascade(window, cascadeIndex);

        _openPlayerWindows.Add(new PlayerWidgetWindowSession(playerWidget, playerWindowViewModel, window));
        UpdatePlayerWindowCount(playerWidget);

        window.Show();

        if (playerWidget.State != WidgetState.Running)
        {
            playerWidget.State = WidgetState.Running;
        }
    }

    private void CreateEntityBuffWindow(
        WidgetListItemViewModel widget,
        EntityListEntry entity)
    {
        var owner = Application.Current?.MainWindow;
        TrackManagerWindow(owner);

        var kind = widget.Kind == WidgetKind.BuffList
            ? PlayerBuffListKind.Buff
            : PlayerBuffListKind.Debuff;
        var viewModel = new EntityBuffListWidgetViewModel(widget, entity, kind);
        var content = new PlayerBuffListWidgetView
        {
            DataContext = viewModel
        };
        var savedBounds = WidgetStateManager.Instance.GetWidgetSnapshot(widget.Kind).Window;
        var window = new WidgetWindow(
            widget,
            content,
            savedBounds,
            owner,
            viewModel.HeaderText);
        viewModel.PropertyChanged += EntityBuffListWindowViewModel_PropertyChanged;
        window.Closed += WidgetWindow_Closed;

        var cascadeIndex = CountOpenTargetWindows(widget);
        ApplyPlayerWindowCascade(window, cascadeIndex);

        _openEntityBuffWindows.Add(new EntityBuffListWindowSession(widget, viewModel, window));
        UpdatePlayerWindowCount(widget);

        window.Show();

        if (widget.State != WidgetState.Running)
        {
            widget.State = WidgetState.Running;
        }
    }

    private static PlayerWidgetWindowViewModel CreatePlayerWindowViewModel(
        WidgetListItemViewModel playerWidget,
        long? requestedCharacterId,
        PlayerRosterEntry? initialPlayer)
    {
        return playerWidget.Kind switch
        {
            WidgetKind.PlayerInfo => new PlayerInfoWidgetViewModel(
                playerWidget,
                requestedCharacterId,
                initialPlayer),
            WidgetKind.PlayerStatus => new PlayerStatusWidgetViewModel(
                playerWidget,
                requestedCharacterId,
                initialPlayer),
            WidgetKind.PlayerEquipment => new PlayerEquipmentWidgetViewModel(
                playerWidget,
                requestedCharacterId,
                initialPlayer),
            WidgetKind.BuffList => new PlayerBuffListWidgetViewModel(
                playerWidget,
                requestedCharacterId,
                initialPlayer,
                PlayerBuffListKind.Buff),
            WidgetKind.DebuffList => new PlayerBuffListWidgetViewModel(
                playerWidget,
                requestedCharacterId,
                initialPlayer,
                PlayerBuffListKind.Debuff),
            WidgetKind.DamageContribution => new PlayerMetricWidgetViewModel(
                playerWidget,
                requestedCharacterId,
                initialPlayer,
                MeterSnapshotKind.Damage,
                PlayerMetricDisplayMode.Contribution),
            WidgetKind.DamageSummary => new PlayerMetricSummaryWidgetViewModel(
                playerWidget,
                requestedCharacterId,
                initialPlayer,
                MeterSnapshotKind.Damage),
            WidgetKind.DpsGraph => new PlayerMetricWidgetViewModel(
                playerWidget,
                requestedCharacterId,
                initialPlayer,
                MeterSnapshotKind.Damage,
                PlayerMetricDisplayMode.Timeline),
            WidgetKind.HealingContribution => new PlayerMetricWidgetViewModel(
                playerWidget,
                requestedCharacterId,
                initialPlayer,
                MeterSnapshotKind.Healing,
                PlayerMetricDisplayMode.Contribution),
            WidgetKind.HealingSummary => new PlayerMetricSummaryWidgetViewModel(
                playerWidget,
                requestedCharacterId,
                initialPlayer,
                MeterSnapshotKind.Healing),
            WidgetKind.HpsGraph => new PlayerMetricWidgetViewModel(
                playerWidget,
                requestedCharacterId,
                initialPlayer,
                MeterSnapshotKind.Healing,
                PlayerMetricDisplayMode.Timeline),
            _ => throw new ArgumentOutOfRangeException(nameof(playerWidget))
        };
    }

    private static FrameworkElement CreatePlayerWindowContent(PlayerWidgetWindowViewModel playerWindowViewModel)
    {
        return playerWindowViewModel switch
        {
            PlayerInfoWidgetViewModel infoViewModel => new PlayerInfoWidgetView
            {
                DataContext = infoViewModel
            },
            PlayerStatusWidgetViewModel statusViewModel => new PlayerStatusWidgetView
            {
                DataContext = statusViewModel
            },
            PlayerEquipmentWidgetViewModel equipmentViewModel => new PlayerEquipmentWidgetView
            {
                DataContext = equipmentViewModel
            },
            PlayerBuffListWidgetViewModel buffListViewModel => new PlayerBuffListWidgetView
            {
                DataContext = buffListViewModel
            },
            PlayerMetricWidgetViewModel { IsContribution: true } metricViewModel => new PlayerMetricContributionWidgetView
            {
                DataContext = metricViewModel
            },
            PlayerMetricSummaryWidgetViewModel summaryViewModel => new PlayerMetricSummaryWidgetView
            {
                DataContext = summaryViewModel
            },
            PlayerMetricWidgetViewModel metricViewModel => new PlayerMetricTimelineWidgetView
            {
                DataContext = metricViewModel
            },
            _ => throw new ArgumentOutOfRangeException(nameof(playerWindowViewModel))
        };
    }

    private WidgetWindowComposition CreateWidgetWindowComposition(WidgetListItemViewModel widget)
    {
        return widget.Kind switch
        {
            WidgetKind.PlayerList => new WidgetWindowComposition(
                new PlayerListWidgetView(),
                null,
                null,
                new PlayerListWidgetFooterView()),
            WidgetKind.EntityList => new WidgetWindowComposition(
                new EntityListWidgetView(),
                null,
                null,
                new PlayerListWidgetFooterView()),
            WidgetKind.DpsMeter => CreateMeterWidgetComposition(widget, MeterSnapshotKind.Damage),
            WidgetKind.HpsMeter => CreateMeterWidgetComposition(widget, MeterSnapshotKind.Healing),
            _ => new WidgetWindowComposition(null, null, null, null)
        };
    }

    private WidgetWindowComposition CreateMeterWidgetComposition(
        WidgetListItemViewModel widget,
        MeterSnapshotKind kind)
    {
        var viewModel = new MeterWidgetViewModel(widget, kind, OpenPlayerWindow);

        return new WidgetWindowComposition(
            new MeterWidgetView
            {
                DataContext = viewModel
            },
            new MeterWidgetHeaderLabelsView
            {
                DataContext = viewModel
            },
            new MeterWidgetHeaderActionsView
            {
                DataContext = viewModel
            },
            new MeterWidgetFooterView
            {
                DataContext = viewModel
            });
    }

    private void Close(WidgetKind kind)
    {
        if (!_openSingleWindows.TryGetValue(kind, out var window))
        {
            return;
        }

        window.Close();
    }

    private void ClosePlayerWindows(WidgetListItemViewModel playerWidget)
    {
        foreach (var targetWindow in _openPlayerWindows
                     .Where(session => ReferenceEquals(session.Widget, playerWidget))
                     .Select(session => session.Window)
                     .Concat(_openEntityBuffWindows
                         .Where(session => ReferenceEquals(session.Widget, playerWidget))
                         .Select(session => session.Window))
                     .ToArray())
        {
            targetWindow.Close();
        }

        UpdatePlayerWindowCount(playerWidget);
    }

    private void TrackPlayerWidget(WidgetListItemViewModel playerWidget)
    {
        if (_trackedPlayerWidgets.TryGetValue(playerWidget.Kind, out var trackedWidget)
            && ReferenceEquals(trackedWidget, playerWidget))
        {
            return;
        }

        if (trackedWidget is not null)
        {
            trackedWidget.PlayerWindowPresentationChanged -= PlayerWidget_PresentationChanged;
        }

        _trackedPlayerWidgets[playerWidget.Kind] = playerWidget;
        playerWidget.PlayerWindowPresentationChanged += PlayerWidget_PresentationChanged;
    }

    public void UpdatePlayerWindowPresentations(IReadOnlyList<PlayerRosterEntry> roster)
    {
        var playersByCharacterId = roster
            .Where(player => player.CharacterId != 0)
            .ToDictionary(player => player.CharacterId);
        var selfPlayer = roster.FirstOrDefault(player => player.IsSelf);

        foreach (var playerWindow in _openPlayerWindows.ToArray())
        {
            playerWindow.ViewModel.UpdatePlayerFromRoster(playersByCharacterId, selfPlayer);
            playerWindow.Window.SetHeaderText(playerWindow.ViewModel.HeaderText);
        }
    }

    public void UpdateEntityWindowPresentations(IReadOnlyList<EntityListEntry> entities)
    {
        var entitiesByUuid = entities.ToDictionary(entity => entity.EntityUuid);

        foreach (var entityWindow in _openEntityBuffWindows.ToArray())
        {
            if (!entitiesByUuid.TryGetValue(entityWindow.ViewModel.EntityUuid, out var entity))
            {
                continue;
            }

            entityWindow.ViewModel.UpdateEntity(entity);
            entityWindow.Window.SetHeaderText(entityWindow.ViewModel.HeaderText);
        }
    }

    private void PlayerWindowViewModel_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName != nameof(PlayerWidgetWindowViewModel.HeaderText)
            || sender is not PlayerWidgetWindowViewModel playerWindowViewModel)
        {
            return;
        }

        var session = _openPlayerWindows.FirstOrDefault(
            candidate => ReferenceEquals(candidate.ViewModel, playerWindowViewModel));
        session?.Window.SetHeaderText(playerWindowViewModel.HeaderText);
    }

    private void EntityBuffListWindowViewModel_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName != nameof(EntityBuffListWidgetViewModel.HeaderText)
            || sender is not EntityBuffListWidgetViewModel viewModel)
        {
            return;
        }

        var session = _openEntityBuffWindows.FirstOrDefault(
            candidate => ReferenceEquals(candidate.ViewModel, viewModel));
        session?.Window.SetHeaderText(viewModel.HeaderText);
    }

    private void PlayerWidget_PresentationChanged(object? sender, EventArgs e)
    {
        if (sender is not WidgetListItemViewModel playerWidget)
        {
            return;
        }

        foreach (var playerWindow in _openPlayerWindows
                     .Where(session => ReferenceEquals(session.Widget, playerWidget))
                     .ToArray())
        {
            playerWindow.ViewModel.RefreshPlayerPresentation();
            playerWindow.Window.SetHeaderText(playerWindow.ViewModel.HeaderText);
        }

        foreach (var entityWindow in _openEntityBuffWindows
                     .Where(session => ReferenceEquals(session.Widget, playerWidget))
                     .ToArray())
        {
            entityWindow.ViewModel.RefreshPresentation();
            entityWindow.Window.SetHeaderText(entityWindow.ViewModel.HeaderText);
        }
    }

    private static PlayerRosterEntry? ResolvePlayer(
        long? requestedCharacterId,
        IReadOnlyList<PlayerRosterEntry> roster)
    {
        return requestedCharacterId is { } characterId
            ? roster.FirstOrDefault(player => player.CharacterId == characterId)
            : roster.FirstOrDefault(player => player.IsSelf);
    }

    private void UpdatePlayerWindowCount(WidgetListItemViewModel playerWidget)
    {
        playerWidget.SetOpenPlayerWindowCount(CountOpenTargetWindows(playerWidget));
    }

    private int CountOpenTargetWindows(WidgetListItemViewModel widget)
    {
        return _openPlayerWindows.Count(session => ReferenceEquals(session.Widget, widget))
            + _openEntityBuffWindows.Count(session => ReferenceEquals(session.Widget, widget));
    }

    private bool HasOpenTargetWindows(WidgetListItemViewModel widget)
    {
        return CountOpenTargetWindows(widget) > 0;
    }

    private static bool IsPlayerWindowWidget(WidgetKind kind)
    {
        return kind is WidgetKind.PlayerInfo
            or WidgetKind.PlayerStatus
            or WidgetKind.PlayerEquipment
            or WidgetKind.BuffList
            or WidgetKind.DebuffList
            or WidgetKind.DamageContribution
            or WidgetKind.DamageSummary
            or WidgetKind.DpsGraph
            or WidgetKind.HealingContribution
            or WidgetKind.HealingSummary
            or WidgetKind.HpsGraph;
    }

    private static void RestoreAndActivate(WidgetWindow window)
    {
        if (window.WindowState == WindowState.Minimized)
        {
            window.WindowState = WindowState.Normal;
        }

        Activate(window);
    }

    private static void Activate(WidgetWindow window)
    {
        window.Activate();
    }

    private static void ApplyPlayerWindowCascade(WidgetWindow window, int cascadeIndex)
    {
        if (cascadeIndex <= 0)
        {
            return;
        }

        var offset = PlayerWindowCascadeOffset * cascadeIndex;
        window.Left += offset;
        window.Top += offset;
    }

    private void TrackManagerWindow(Window? managerWindow)
    {
        if (managerWindow is null || ReferenceEquals(_managerWindow, managerWindow))
        {
            return;
        }

        if (_managerWindow is not null)
        {
            _managerWindow.Closing -= ManagerWindow_Closing;
        }

        _managerWindow = managerWindow;
        _managerWindow.Closing += ManagerWindow_Closing;
    }

    private void ManagerWindow_Closing(object? sender, CancelEventArgs e)
    {
        if (e.Cancel)
        {
            return;
        }

        _isManagerClosing = true;

        foreach (var window in _openSingleWindows.Values
                     .Concat(_openPlayerWindows.Select(session => session.Window))
                     .Concat(_openEntityBuffWindows.Select(session => session.Window))
                     .ToArray())
        {
            window.Close();
        }
    }

    private void WidgetWindow_Closed(object? sender, EventArgs e)
    {
        if (sender is not WidgetWindow window)
        {
            return;
        }

        window.Closed -= WidgetWindow_Closed;

        var playerWindow = _openPlayerWindows.FirstOrDefault(
            session => ReferenceEquals(session.Window, window));

        if (playerWindow is not null)
        {
            playerWindow.ViewModel.PropertyChanged -= PlayerWindowViewModel_PropertyChanged;

            if (playerWindow.ViewModel is IDisposable disposable)
            {
                disposable.Dispose();
            }

            _openPlayerWindows.Remove(playerWindow);
            UpdatePlayerWindowCount(playerWindow.Widget);

            if (!_isManagerClosing
                && playerWindow.Widget.State == WidgetState.Running
                && !HasOpenTargetWindows(playerWindow.Widget))
            {
                playerWindow.Widget.State = WidgetState.Stopped;
            }

            return;
        }

        var entityWindow = _openEntityBuffWindows.FirstOrDefault(
            session => ReferenceEquals(session.Window, window));

        if (entityWindow is not null)
        {
            entityWindow.ViewModel.PropertyChanged -= EntityBuffListWindowViewModel_PropertyChanged;
            entityWindow.ViewModel.Dispose();
            _openEntityBuffWindows.Remove(entityWindow);
            UpdatePlayerWindowCount(entityWindow.Widget);

            if (!_isManagerClosing
                && entityWindow.Widget.State == WidgetState.Running
                && !HasOpenTargetWindows(entityWindow.Widget))
            {
                entityWindow.Widget.State = WidgetState.Stopped;
            }

            return;
        }

        _openSingleWindows.Remove(window.Widget.Kind);

        if (!_isManagerClosing && window.Widget.State == WidgetState.Running)
        {
            window.Widget.State = WidgetState.Stopped;
        }
    }

    private sealed class PlayerWidgetWindowSession
    {
        public PlayerWidgetWindowSession(
            WidgetListItemViewModel widget,
            PlayerWidgetWindowViewModel viewModel,
            WidgetWindow window)
        {
            Widget = widget;
            ViewModel = viewModel;
            Window = window;
        }

        public WidgetListItemViewModel Widget { get; }

        public PlayerWidgetWindowViewModel ViewModel { get; }

        public WidgetWindow Window { get; }
    }

    private sealed class EntityBuffListWindowSession
    {
        public EntityBuffListWindowSession(
            WidgetListItemViewModel widget,
            EntityBuffListWidgetViewModel viewModel,
            WidgetWindow window)
        {
            Widget = widget;
            ViewModel = viewModel;
            Window = window;
        }

        public WidgetListItemViewModel Widget { get; }

        public EntityBuffListWidgetViewModel ViewModel { get; }

        public WidgetWindow Window { get; }
    }

    private sealed record WidgetWindowComposition(
        FrameworkElement? Content,
        FrameworkElement? HeaderChromeActions,
        FrameworkElement? HeaderActions,
        FrameworkElement? FooterContent);

}
