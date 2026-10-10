using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Windows;
using System.Windows.Threading;
using StarResonanceDps.App.Config;
using StarResonanceDps.App.Diagnostics;
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

    /// <summary>顔ぶれを当てる処理を積んだまま、まだ当てていないプレイヤーの窓(<see cref="UpdatePlayerWindowPresentations"/>)。</summary>
    private readonly HashSet<PlayerWidgetWindowSession> _playerWindowsAwaitingRoster = [];

    /// <summary>窓へ当てる最新の顔ぶれ。</summary>
    private PlayerWindowRoster? _latestPlayerWindowRoster;

    private Window? _managerWindow;
    private bool _isManagerClosing;

    private WidgetWindowManager()
    {
        MessageManager.ResetToStartupCompleted += MessageManager_ResetToStartupCompleted;
    }

    /// <summary>
    /// ログアウトで Core を起動時の状態へ戻した。実体の窓を未捕獲に戻し、起動時の復元と同じく種類と種別IDで捕まえ直させる。
    /// 未知の敵(種別ID 0)の窓は捕まえ直せないのでそのままにする。通知はパケット処理のスレッドで来るので、UI のスレッドへ渡す。
    /// </summary>
    private void MessageManager_ResetToStartupCompleted()
    {
        Application.Current?.Dispatcher.InvokeAsync(() =>
        {
            ReleaseEntityWindows();
            ResetPlayerWindowsToStartup();
        });
    }

    /// <summary>
    /// プレイヤーの窓を起動直後の状態へ戻す。空の一覧の通知が先に UI のスレッドへ積まれているので、
    /// 窓が一覧の外れで残した最後の値を、その後で捨てる。
    /// 窓へ当てる処理はそれより低い優先度で積まれているので、先にここで当て切ってから戻す。
    /// </summary>
    private void ResetPlayerWindowsToStartup()
    {
        ApplyAwaitingRosterToPlayerWindows();
        foreach (var session in _openPlayerWindows.ToArray())
        {
            session.ViewModel.ResetToStartup();
        }
    }

    private void ReleaseEntityWindows()
    {
        foreach (var session in _openEntityBuffWindows)
        {
            if (!session.ViewModel.IsEntityAcquired || session.ViewModel.EntityId == 0)
            {
                continue;
            }

            session.ViewModel.ReleaseEntity();
            session.Window.SetHeaderText(session.ViewModel.HeaderText);
        }
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
                && (session.ViewModel.RepresentsEntity(entity.EntityUuid)
                    || (!session.ViewModel.IsEntityAcquired
                        && session.ViewModel.EntityId == entity.EntityId
                        && session.ViewModel.EntityType == entity.EntityType)));

        if (existingWindow is not null)
        {
            existingWindow.ViewModel.TryApplyEntity(entity);
            existingWindow.Window.SetHeaderText(existingWindow.ViewModel.HeaderText);
            RestoreAndActivate(existingWindow.Window);
            return;
        }

        CreateEntityBuffWindow(widget, new EntityWindowTarget(entity));
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

            // ピン留め中はアクティブにしない。WS_EX_NOACTIVATE が防ぐのは
            // クリックによるアクティブ化で、ここで Activate() を呼ぶと
            // こちらからフォーカスを奪ってしまう。

            return;
        }

        if (!_openSingleWindows.TryGetValue(widget.Kind, out var window))
        {
            return;
        }

        window.ApplyPinState(widget.IsPinned);
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

    /// <summary>
    /// バフ/デバフ一覧の行から、そのバフだけを出すカードを開く。
    ///
    /// <para>
    /// 1ウィンドウ = 1プレイヤー × 1バフ。すでに同じ組み合わせが開いていれば前面に出すだけ。
    /// </para>
    /// </summary>
    public void OpenBuffCardForPlayer(
        long characterId,
        PlayerBuffListKind kind,
        string buffKey,
        int baseId)
    {
        if (string.IsNullOrWhiteSpace(buffKey)
            || !_trackedPlayerWidgets.TryGetValue(WidgetKind.BuffDebuffCard, out var widget))
        {
            return;
        }

        TrackPlayerWidget(widget);

        // 料理・薬剤は食べ直すたびに別のIDへ入れ替わる。まとまりとして追う。
        var group = CombatDataCatalog.GetBuffGroup(baseId);

        var existingWindow = _openPlayerWindows
            .FirstOrDefault(session =>
                ReferenceEquals(session.Widget, widget)
                && session.ViewModel is BuffDebuffCardWidgetViewModel card
                && card.RepresentsPlayer(characterId)
                && card.RepresentsBuff(kind, buffKey, group));

        if (existingWindow is not null)
        {
            RestoreAndActivate(existingWindow.Window);
            return;
        }

        CreatePlayerWindow(widget, characterId, kind, buffKey, buffGroup: group);
    }

    /// <summary>モンスターのバフ/デバフ一覧の行から開く版。1ウィンドウ = 1体 × 1バフ。</summary>
    public void OpenBuffCardForEntity(
        EntityWindowTarget source,
        PlayerBuffListKind kind,
        string buffKey,
        int baseId)
    {
        if (string.IsNullOrWhiteSpace(buffKey)
            || source.Entity is not { } entity
            || !_trackedPlayerWidgets.TryGetValue(WidgetKind.BuffDebuffCard, out var widget))
        {
            return;
        }

        TrackPlayerWidget(widget);

        var group = CombatDataCatalog.GetBuffGroup(baseId);

        var existingWindow = _openEntityBuffWindows
            .FirstOrDefault(session =>
                ReferenceEquals(session.Widget, widget)
                && session.ViewModel is EntityBuffDebuffCardWidgetViewModel card
                && card.RepresentsBuff(kind, buffKey, group)
                && (card.RepresentsEntity(entity.EntityUuid)
                    || (!card.IsEntityAcquired
                        && card.EntityId == entity.EntityId
                        && card.EntityType == entity.EntityType)));

        if (existingWindow is not null)
        {
            existingWindow.ViewModel.TryApplyEntity(entity);
            existingWindow.Window.SetHeaderText(existingWindow.ViewModel.HeaderText);
            RestoreAndActivate(existingWindow.Window);
            return;
        }

        CreateEntityBuffCardWindow(widget, new EntityWindowTarget(entity), kind, buffKey, group);
    }

    /// <summary>
    /// 保存しておいた対象で窓を1枚開き直す。
    ///
    /// <para>
    /// 実体の窓は<b>未捕獲の状態</b>で開く。実体IDは再起動で消えるため、
    /// 種類と種別が一致する個体がAOIに現れた時点で
    /// <see cref="UpdateEntityWindowPresentations"/> が捕まえる。
    /// </para>
    /// </summary>
    private void RestoreTargetWindow(WidgetListItemViewModel widget, WidgetOpenTargetConfig target)
    {
        var buffListKind = target.BuffListKind is { } value
            && Enum.IsDefined(typeof(PlayerBuffListKind), value)
                ? (PlayerBuffListKind)value
                : (PlayerBuffListKind?)null;

        var buffGroup = target.BuffGroup is { } groupValue
            && Enum.IsDefined(typeof(BuffGroup), groupValue)
                ? (BuffGroup)groupValue
                : BuffGroup.None;

        if (!target.IsEntity)
        {
            CreatePlayerWindow(
                widget,
                target.CharacterId,
                buffListKind,
                target.BuffKey,
                target.Name,
                buffGroup,
                target.ResolvedCharacterId,
                target.BuffName,
                target.Window);
            return;
        }

        var entityType = target.EntityType is { } typeValue && Enum.IsDefined(typeof(Zproto.EEntityType), typeValue)
            ? (Zproto.EEntityType)typeValue
            : (Zproto.EEntityType?)null;
        var entityTarget = new EntityWindowTarget(target.EntityId, entityType, target.Name);

        if (widget.Kind != WidgetKind.BuffDebuffCard)
        {
            CreateEntityBuffWindow(widget, entityTarget, target.Window);
            return;
        }

        if (buffGroup == BuffGroup.None && string.IsNullOrWhiteSpace(target.BuffKey))
        {
            return;
        }

        CreateEntityBuffCardWindow(
            widget,
            entityTarget,
            buffListKind ?? PlayerBuffListKind.Buff,
            target.BuffKey,
            buffGroup,
            target.BuffName,
            target.Window);
    }

    /// <summary>
    /// 開いている窓の対象一覧を書き出す。窓が増減したときだけ呼ぶ。
    ///
    /// <para>
    /// アプリ終了時は呼ばない。終了処理は全部の窓を閉じるので、
    /// <b>そこで保存すると次回に復元するものが消える。</b>
    /// </para>
    /// </summary>
    private void SaveOpenTargets(WidgetListItemViewModel widget)
    {
        if (_isManagerClosing || !WidgetConfigDefaults.SupportsOpenTargets(widget.Kind))
        {
            return;
        }

        var targets = new List<WidgetOpenTargetConfig>();

        foreach (var session in _openPlayerWindows)
        {
            if (!ReferenceEquals(session.Widget, widget))
            {
                continue;
            }

            var card = session.ViewModel as BuffDebuffCardWidgetViewModel;
            targets.Add(new WidgetOpenTargetConfig
            {
                CharacterId = session.ViewModel.RequestedCharacterId,
                Name = session.ViewModel.LastKnownPlayerName,
                ResolvedCharacterId = session.ViewModel.LastKnownPlayerUid == 0
                    ? null
                    : session.ViewModel.LastKnownPlayerUid,
                BuffListKind = card is null ? null : (int)card.BuffListKind,
                BuffKey = card?.RequestedBuffKey,
                BuffName = card?.LastKnownBuffName,
                BuffGroup = card is null ? null : (int)card.Group,
                Window = session.Window.GetCurrentBounds()
            });
        }

        foreach (var session in _openEntityBuffWindows)
        {
            if (!ReferenceEquals(session.Widget, widget))
            {
                continue;
            }

            // 種別ID 0(未知の敵)の窓は保存しない。復元は種類と種別IDで個体を捕まえるので、
            // 0 のまま戻すと次の起動で別の未知の個体を捕まえる。
            if (session.ViewModel.EntityId == 0)
            {
                continue;
            }

            var card = session.ViewModel as EntityBuffDebuffCardWidgetViewModel;
            targets.Add(new WidgetOpenTargetConfig
            {
                EntityId = session.ViewModel.EntityId,
                EntityType = session.ViewModel.EntityType is { } entityType ? (int)entityType : null,
                Name = session.ViewModel.TargetName,
                BuffListKind = card is null ? null : (int)card.BuffListKind,
                BuffKey = card?.RequestedBuffKey,
                BuffName = card?.LastKnownBuffName,
                BuffGroup = card is null ? null : (int)card.Group,
                Window = session.Window.GetCurrentBounds()
            });
        }

        WidgetStateManager.Instance.SaveWidgetOpenTargets(widget.Kind, targets);
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

            var savedTargets = WidgetStateManager.Instance
                .GetWidgetSnapshot(widget.Kind)
                .OpenTargets;

            if (savedTargets is { Count: > 0 })
            {
                foreach (var target in savedTargets)
                {
                    RestoreTargetWindow(widget, target);
                }

                return;
            }

            // 対象の記録が無い。ここはウィジェットカードから初めて起動したときの経路。
            if (widget.Kind == WidgetKind.BuffDebuffCard)
            {
                // 自分の料理と薬剤を1枚ずつ。どちらも対象が居なくても
                // 「バフ(料理)」「バフ(薬剤)」と名乗れるので、空でも何の枠か分かる。
                CreatePlayerWindow(
                    widget,
                    requestedCharacterId: null,
                    buffGroup: BuffGroup.Cuisine);
                CreatePlayerWindow(
                    widget,
                    requestedCharacterId: null,
                    buffGroup: BuffGroup.Potion);
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

    private void CreatePlayerWindow(
        WidgetListItemViewModel playerWidget,
        long? requestedCharacterId,
        PlayerBuffListKind? requestedBuffListKind = null,
        string? requestedBuffKey = null,
        string? savedPlayerName = null,
        BuffGroup buffGroup = BuffGroup.None,
        long? savedPlayerUid = null,
        string? savedBuffName = null,
        WidgetWindowConfig? savedWindowBounds = null)
    {
        var owner = Application.Current?.MainWindow;
        TrackManagerWindow(owner);

        var roster = PlayerRosterPresentationStore.Instance.Current.Entries;
        var initialPlayer = ResolvePlayer(requestedCharacterId, roster);
        var playerWindowViewModel = CreatePlayerWindowViewModel(
            playerWidget,
            requestedCharacterId,
            initialPlayer,
            requestedBuffListKind,
            requestedBuffKey,
            buffGroup);
        // 種に使うのは観測したUID。自分の窓は requestedCharacterId が null(＝「自分」という指定)
        // なので、それを条件にすると一度も種が入らない。
        playerWindowViewModel.SeedLastKnownPlayer(
            savedPlayerName,
            savedPlayerUid ?? requestedCharacterId ?? 0);

        if (playerWindowViewModel is BuffDebuffCardWidgetViewModel seededCard)
        {
            seededCard.SeedLastKnownBuffName(savedBuffName);
        }

        var content = CreatePlayerWindowContent(playerWindowViewModel);
        var headerActions = CreatePlayerWindowHeaderActions(playerWindowViewModel);
        var savedBounds = savedWindowBounds
            ?? WidgetStateManager.Instance.GetWidgetSnapshot(playerWidget.Kind).Window;
        var window = new WidgetWindow(
            playerWidget,
            content,
            savedBounds,
            owner,
            playerWindowViewModel.HeaderText,
            headerChromeActions: headerActions.HeaderChromeActions,
            headerActions: headerActions.HeaderActions);
        playerWindowViewModel.PropertyChanged += PlayerWindowViewModel_PropertyChanged;
        playerWindowViewModel.SavedTargetInfoResolved += PlayerWindowViewModel_SavedTargetInfoResolved;
        window.Closed += WidgetWindow_Closed;
        // 開き先を持たない種別(常に自分1枚のステータス詳細)は、種別ごとの保存だけを使う
        // (SaveOpenTargets はその種別では何もせず返る)。
        window.SaveWindowBoundsOverride = WidgetConfigDefaults.SupportsOpenTargets(playerWidget.Kind)
            ? () => SaveOpenTargets(playerWidget)
            : null;

        // 復元した位置があるならカスケードで動かさない。
        if (savedWindowBounds is null)
        {
            ApplyPlayerWindowCascade(window, CountOpenTargetWindows(playerWidget));
        }

        _openPlayerWindows.Add(new PlayerWidgetWindowSession(playerWidget, playerWindowViewModel, window));
        UpdatePlayerWindowCount(playerWidget);
        SaveOpenTargets(playerWidget);

        window.Show();

        if (playerWidget.State != WidgetState.Running)
        {
            playerWidget.State = WidgetState.Running;
        }
    }

    private void CreateEntityBuffWindow(
        WidgetListItemViewModel widget,
        EntityWindowTarget target,
        WidgetWindowConfig? savedWindowBounds = null)
    {
        var kind = widget.Kind == WidgetKind.BuffList
            ? PlayerBuffListKind.Buff
            : PlayerBuffListKind.Debuff;
        var viewModel = new EntityBuffListWidgetViewModel(widget, target, kind, OpenBuffCardForEntity);

        CreateEntityWindow(
            widget,
            viewModel,
            new PlayerBuffListWidgetView
            {
                DataContext = viewModel
            },
            headerActions: null,
            headerChromeActions: null,
            savedWindowBounds);
    }

    private void CreateEntityBuffCardWindow(
        WidgetListItemViewModel widget,
        EntityWindowTarget target,
        PlayerBuffListKind kind,
        string? buffKey,
        BuffGroup group,
        string? savedBuffName = null,
        WidgetWindowConfig? savedWindowBounds = null)
    {
        var viewModel = new EntityBuffDebuffCardWidgetViewModel(widget, target, kind, buffKey, group);
        viewModel.SeedLastKnownBuffName(savedBuffName);
        viewModel.SavedTargetInfoResolved += EntityBuffCardViewModel_SavedTargetInfoResolved;

        CreateEntityWindow(
            widget,
            viewModel,
            new BuffDebuffCardWidgetView
            {
                DataContext = viewModel
            },
            new BuffDebuffCardHeaderActionsView
            {
                DataContext = viewModel
            },
            new BuffDebuffCardHeaderLabelsView
            {
                DataContext = viewModel
            },
            savedWindowBounds);
    }

    private void CreateEntityWindow(
        WidgetListItemViewModel widget,
        IEntityWidgetWindowViewModel viewModel,
        FrameworkElement content,
        FrameworkElement? headerActions,
        FrameworkElement? headerChromeActions,
        WidgetWindowConfig? savedWindowBounds = null)
    {
        var owner = Application.Current?.MainWindow;
        TrackManagerWindow(owner);

        var savedBounds = savedWindowBounds
            ?? WidgetStateManager.Instance.GetWidgetSnapshot(widget.Kind).Window;
        var window = new WidgetWindow(
            widget,
            content,
            savedBounds,
            owner,
            viewModel.HeaderText,
            headerChromeActions: headerChromeActions,
            headerActions: headerActions);
        viewModel.PropertyChanged += EntityBuffListWindowViewModel_PropertyChanged;
        window.Closed += WidgetWindow_Closed;
        window.SaveWindowBoundsOverride = () => SaveOpenTargets(widget);

        if (savedWindowBounds is null)
        {
            ApplyPlayerWindowCascade(window, CountOpenTargetWindows(widget));
        }

        _openEntityBuffWindows.Add(new EntityBuffListWindowSession(widget, viewModel, window));
        UpdatePlayerWindowCount(widget);
        SaveOpenTargets(widget);

        window.Show();

        if (widget.State != WidgetState.Running)
        {
            widget.State = WidgetState.Running;
        }
    }

    private static PlayerWidgetWindowViewModel CreatePlayerWindowViewModel(
        WidgetListItemViewModel playerWidget,
        long? requestedCharacterId,
        PlayerRosterEntry? initialPlayer,
        PlayerBuffListKind? requestedBuffListKind = null,
        string? requestedBuffKey = null,
        BuffGroup buffGroup = BuffGroup.None)
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
                PlayerBuffListKind.Buff,
                Instance.OpenBuffCardForPlayer),
            WidgetKind.DebuffList => new PlayerBuffListWidgetViewModel(
                playerWidget,
                requestedCharacterId,
                initialPlayer,
                PlayerBuffListKind.Debuff,
                Instance.OpenBuffCardForPlayer),
            WidgetKind.BuffDebuffCard => new BuffDebuffCardWidgetViewModel(
                playerWidget,
                requestedCharacterId,
                initialPlayer,
                requestedBuffListKind ?? PlayerBuffListKind.Buff,
                requestedBuffKey,
                // 対象の指定が無い(ウィジェットカードから直接開いた)ときは料理を追う。
                requestedBuffKey is null && buffGroup == BuffGroup.None
                    ? BuffGroup.Cuisine
                    : buffGroup),
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
            BuffDebuffCardWidgetViewModel buffCardViewModel => new BuffDebuffCardWidgetView
            {
                DataContext = buffCardViewModel
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

    /// <summary>
    /// プレイヤー用ウィンドウのヘッダーボタン。
    ///
    /// <para>
    /// ヘッダーの文字ボタンは<b>2層で1組</b>。押せるだけの層(<c>HeaderActions</c>)と、
    /// 見える文字を描く枠側の層(<c>HeaderChromeActions</c>)を両方渡さないと文字が出ない。
    /// </para>
    /// </summary>
    private static (FrameworkElement? HeaderActions, FrameworkElement? HeaderChromeActions)
        CreatePlayerWindowHeaderActions(PlayerWidgetWindowViewModel playerWindowViewModel)
    {
        if (playerWindowViewModel is not BuffDebuffCardWidgetViewModel)
        {
            return (null, null);
        }

        return (
            new BuffDebuffCardHeaderActionsView
            {
                DataContext = playerWindowViewModel
            },
            new BuffDebuffCardHeaderLabelsView
            {
                DataContext = playerWindowViewModel
            });
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
            WidgetKind.TakenDamageLog => new WidgetWindowComposition(
                new TakenDamageLogWidgetView
                {
                    DataContext = new TakenDamageLogWidgetViewModel(widget)
                },
                null,
                null,
                null),
            _ => new WidgetWindowComposition(null, null, null, null)
        };
    }

    private WidgetWindowComposition CreateMeterWidgetComposition(
        WidgetListItemViewModel widget,
        MeterSnapshotKind kind)
    {
        var viewModel = new MeterWidgetViewModel(widget, kind, OpenPlayerWindow);

        // ヘッダーの文字ボタンは2層で1組。**片方だけ渡すと文字の無いボタンか、押せない文字が残る。**
        // Labels が枠側で文字を描き(IsHitTestVisible=False)、Actions が中身を透明にして当たり判定だけ持つ。
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

    /// <summary>その相手の名刺の値(顔写真・名刺)が変わった。開いているプレイヤーの窓へ渡す。</summary>
    public void UpdatePlayerWindowSocialData(long characterId)
    {
        foreach (var playerWindow in _openPlayerWindows.ToArray())
        {
            playerWindow.ViewModel.NotifySocialDataChanged(characterId);
        }
    }

    /// <summary>名刺の値の控えが全部消えた。開いているプレイヤーの窓すべてへ渡す(一覧から外れて値を残している窓も含む)。</summary>
    public void ClearPlayerWindowSocialData()
    {
        foreach (var playerWindow in _openPlayerWindows.ToArray())
        {
            playerWindow.ViewModel.NotifySocialDataCleared();
        }
    }

    /// <summary>自分の装備の値が変わった。装備詳細の窓に知らせる(自分を出している窓だけが作り直す)。</summary>
    public void UpdatePlayerWindowSelfEquipment()
    {
        foreach (var playerWindow in _openPlayerWindows.ToArray())
        {
            if (playerWindow.ViewModel is PlayerEquipmentWidgetViewModel equipment)
            {
                equipment.NotifySelfEquipmentChanged();
            }
        }
    }

    /// <summary>
    /// 顔ぶれをプレイヤーの窓へ当てる。<b>窓ごとに別の処理(Background)として積む。</b>
    /// 相手が変わった窓はその場で中身を作り直すので、1つの処理で全部の窓に当てると、全部の窓の作り直しの間は入力も描画も止まる。
    /// 積んだ窓に当たる前に次の顔ぶれが来たら、その窓は最新の顔ぶれで1回だけ当てる。閉じた窓は飛ばす。
    /// </summary>
    public void UpdatePlayerWindowPresentations(PlayerRosterSnapshot roster)
    {
        _latestPlayerWindowRoster = new PlayerWindowRoster(
            roster,
            roster.Entries
                .Where(player => player.CharacterId != 0)
                .ToDictionary(player => player.CharacterId),
            roster.Entries.FirstOrDefault(player => player.IsSelf));

        var dispatcher = Application.Current?.Dispatcher;
        foreach (var playerWindow in _openPlayerWindows.ToArray())
        {
            if (!_playerWindowsAwaitingRoster.Add(playerWindow))
            {
                continue;
            }

            if (dispatcher is null)
            {
                ApplyRosterToPlayerWindow(playerWindow);
                continue;
            }

            dispatcher.BeginInvoke(DispatcherPriority.Background, () => ApplyRosterToPlayerWindow(playerWindow));
        }
    }

    /// <summary>積んである顔ぶれを窓へ当てる。積んでいない窓・閉じた窓には何もしない。</summary>
    private void ApplyRosterToPlayerWindow(PlayerWidgetWindowSession playerWindow)
    {
        if (!_playerWindowsAwaitingRoster.Remove(playerWindow)
            || _latestPlayerWindowRoster is not { } latest
            || !_openPlayerWindows.Contains(playerWindow))
        {
            return;
        }

        var probe = HistorySwitchProbe.BeginWindow(playerWindow.ViewModel, playerWindow.Widget.Kind.ToString());
        playerWindow.ViewModel.UpdateRosterContext(latest.Roster.MapName, latest.Roster.MapChannel, latest.Roster.SeasonId);
        playerWindow.ViewModel.UpdatePlayerFromRoster(latest.PlayersByCharacterId, latest.SelfPlayer);
        playerWindow.Window.SetHeaderText(playerWindow.ViewModel.HeaderText);
        probe?.End(string.Empty);
    }

    /// <summary>積んである顔ぶれを、待っている窓全部へその場で当てる。</summary>
    private void ApplyAwaitingRosterToPlayerWindows()
    {
        foreach (var playerWindow in _openPlayerWindows.ToArray())
        {
            ApplyRosterToPlayerWindow(playerWindow);
        }
    }

    public void UpdateEntityWindowPresentations(IReadOnlyList<EntityListEntry> entities)
    {
        var entitiesByUuid = entities.ToDictionary(entity => entity.EntityUuid);

        foreach (var entityWindow in _openEntityBuffWindows.ToArray())
        {
            var viewModel = entityWindow.ViewModel;
            EntityListEntry? entity;

            if (viewModel.IsEntityAcquired)
            {
                entitiesByUuid.TryGetValue(viewModel.EntityUuid, out entity);
            }
            else
            {
                // 設定から復元した直後の窓。種類と種別が一致する個体を1体だけ捕まえる。
                entity = entities.FirstOrDefault(
                    candidate => candidate.EntityId == viewModel.EntityId
                        && candidate.EntityType == viewModel.EntityType);
            }

            if (entity is null)
            {
                continue;
            }

            viewModel.TryApplyEntity(entity);
            entityWindow.Window.SetHeaderText(viewModel.HeaderText);
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

    /// <summary>
    /// 相手の素性が確定したら対象一覧を書き直す。
    /// 窓の開閉だけで保存していると、開いた直後はまだ名前が空なので
    /// <b>保存に名前が入らず、次の起動でタイトルを復元できない</b>。
    /// </summary>
    private void PlayerWindowViewModel_SavedTargetInfoResolved(object? sender, EventArgs e)
    {
        if (sender is not PlayerWidgetWindowViewModel playerWindowViewModel)
        {
            return;
        }

        var session = _openPlayerWindows.FirstOrDefault(
            candidate => ReferenceEquals(candidate.ViewModel, playerWindowViewModel));

        if (session is not null)
        {
            SaveOpenTargets(session.Widget);
        }
    }

    private void EntityBuffCardViewModel_SavedTargetInfoResolved(object? sender, EventArgs e)
    {
        var session = _openEntityBuffWindows.FirstOrDefault(
            candidate => ReferenceEquals(candidate.ViewModel, sender));

        if (session is not null)
        {
            SaveOpenTargets(session.Widget);
        }
    }

    private void EntityBuffListWindowViewModel_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName != nameof(IEntityWidgetWindowViewModel.HeaderText)
            || sender is not IEntityWidgetWindowViewModel viewModel)
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
            or WidgetKind.BuffDebuffCard
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
            playerWindow.ViewModel.SavedTargetInfoResolved -= PlayerWindowViewModel_SavedTargetInfoResolved;

            if (playerWindow.ViewModel is IDisposable disposable)
            {
                disposable.Dispose();
            }

            _openPlayerWindows.Remove(playerWindow);
            _playerWindowsAwaitingRoster.Remove(playerWindow);
            UpdatePlayerWindowCount(playerWindow.Widget);
            SaveOpenTargets(playerWindow.Widget);

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
            if (entityWindow.ViewModel is EntityBuffDebuffCardWidgetViewModel entityCard)
            {
                entityCard.SavedTargetInfoResolved -= EntityBuffCardViewModel_SavedTargetInfoResolved;
            }

            entityWindow.ViewModel.Dispose();
            _openEntityBuffWindows.Remove(entityWindow);
            UpdatePlayerWindowCount(entityWindow.Widget);
            SaveOpenTargets(entityWindow.Widget);

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

    private sealed record PlayerWindowRoster(
        PlayerRosterSnapshot Roster,
        IReadOnlyDictionary<long, PlayerRosterEntry> PlayersByCharacterId,
        PlayerRosterEntry? SelfPlayer);

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
            IEntityWidgetWindowViewModel viewModel,
            WidgetWindow window)
        {
            Widget = widget;
            ViewModel = viewModel;
            Window = window;
        }

        public WidgetListItemViewModel Widget { get; }

        public IEntityWidgetWindowViewModel ViewModel { get; }

        public WidgetWindow Window { get; }
    }

    private sealed record WidgetWindowComposition(
        FrameworkElement? Content,
        FrameworkElement? HeaderChromeActions,
        FrameworkElement? HeaderActions,
        FrameworkElement? FooterContent);

}
