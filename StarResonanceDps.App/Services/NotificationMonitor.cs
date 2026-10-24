using System.Windows.Threading;
using StarResonanceDps.App.Config;
using StarResonanceDps.App.Models.Notifications;
using StarResonanceDps.App.Models.Widgets;
using StarResonanceDps.Core.CombatRuntime;
using StarResonanceDps.Core.Models;
using StarResonanceDps.Core.Services;

namespace StarResonanceDps.App.Services;

/// <summary>
/// ウィジェットを開いていなくても通知する項目(マッチング成立・HP低下・プレイヤー出現・エンティティ出現・予兆技)の判定を持つ。
/// 起動時から動き、画面のスレッドで使う。
/// バフ・デバフ効果切れ・薬剤・料理効果切れは、開いているバフ・デバフカードとバフ・デバフグループが判定する(<see cref="BuffNotificationRegistry"/>)。
///
/// <para>
/// 判定はその項目の通知がオンの間だけ動かす。オフにしたら控えを捨て、オンにした時点から見始める。
/// HP低下の控えはマップが替わったら捨てる(ログアウトとキャプチャの停止では名簿が空になり、判定の側で全員の控えが消える)。
/// HP低下はプレイヤーの通知なので、NPC(助っ人)と判定された人は渡さない。
/// 出現の2つは、オンにした・名前を変えたときに今の一覧を「既にいる相手」として控え直す。起動した直後に見えた相手には出す。
/// </para>
/// </summary>
public sealed class NotificationMonitor
{
    /// <summary>予兆技を確かめる間隔。タイムラインの更新と同じ間隔。</summary>
    private static readonly TimeSpan TelegraphedSkillPollInterval = TimeSpan.FromMilliseconds(250);

    private readonly HealthLowNotificationTracker _healthLowTracker = new();
    private readonly AppearanceNotificationTracker _playerAppearedTracker = new(NotificationKind.PlayerAppeared);
    private readonly AppearanceNotificationTracker _entityAppearedTracker = new(NotificationKind.EntityAppeared);
    private readonly List<PlayerRosterEntry> _nearbyPlayers = [];
    private readonly DispatcherTimer _telegraphedSkillTimer;
    private TelegraphedSkillNotificationTracker? _telegraphedSkillTracker;

    private PlayerRosterSnapshot? _lastRoster;
    private IReadOnlyList<NearbyEntityEntry> _lastNearbyEntities = [];
    private long _healthLowMapGeneration = -1;
    private bool _isHealthLowEnabled;

    /// <summary>HP低下の判定に使っている「HP低下判定」と「通知条件」の番号。選び直したら控えを捨てて見始め直す。</summary>
    private (int ThresholdIndex, int ConditionIndex) _healthLowCriteria;

    /// <summary>プレイヤー出現で見ている名前(オフか名前が空なら null)。変わったら控え直す。</summary>
    private string? _playerAppearedWatchName;

    /// <summary>エンティティ出現で見ている名前(オフか名前が空なら null)。変わったら控え直す。</summary>
    private string? _entityAppearedWatchName;

    public NotificationMonitor()
    {
        _telegraphedSkillTimer = new DispatcherTimer
        {
            Interval = TelegraphedSkillPollInterval
        };
        _telegraphedSkillTimer.Tick += TelegraphedSkillTimer_Tick;

        // 起動時の設定は控え直さずに見始める(起動した直後に見えた相手にも出す)。
        var notifications = ConfigManager.Instance.GetNotificationsSnapshot();
        _playerAppearedWatchName = GetAppearanceWatchName(notifications.Get(NotificationKind.PlayerAppeared));
        _entityAppearedWatchName = GetAppearanceWatchName(notifications.Get(NotificationKind.EntityAppeared));

        ConfigManager.Instance.NotificationsChanged += ConfigManager_NotificationsChanged;
        ApplySettings();
    }

    /// <summary>マッチングが成立した。画面のスレッドで呼ぶ。</summary>
    public void NotifyMatchFound(Zproto.EMatchType matchType, long matchTypeUuid)
    {
        if (ConfigManager.Instance.GetNotificationItem(NotificationKind.MatchFound).IsEnabled)
        {
            MatchFoundNotifier.Notify(matchType, matchTypeUuid);
        }
    }

    /// <summary>名簿が届いた。画面のスレッドで呼ぶ。</summary>
    public void ObserveRoster(PlayerRosterSnapshot roster)
    {
        _lastRoster = roster;
        ObserveHealthLow();
        ObservePlayerAppearance();
    }

    /// <summary>周りのエンティティの一覧(エンティティリストの元)が届いた。画面のスレッドで呼ぶ。</summary>
    public void ObserveNearbyEntities(IReadOnlyList<NearbyEntityEntry> entities)
    {
        _lastNearbyEntities = entities;
        ObserveEntityAppearance();
    }

    private void ConfigManager_NotificationsChanged(object? sender, EventArgs e)
    {
        ApplySettings();
    }

    private void ApplySettings()
    {
        var notifications = ConfigManager.Instance.GetNotificationsSnapshot();

        var isTelegraphedSkillEnabled = notifications.Get(NotificationKind.TelegraphedSkill).IsEnabled;
        if (isTelegraphedSkillEnabled && _telegraphedSkillTracker is null)
        {
            _telegraphedSkillTracker = new TelegraphedSkillNotificationTracker();
            _telegraphedSkillTimer.Start();
        }
        else if (!isTelegraphedSkillEnabled && _telegraphedSkillTracker is not null)
        {
            _telegraphedSkillTimer.Stop();
            _telegraphedSkillTracker = null;
        }

        var healthLow = notifications.Get(NotificationKind.HealthLow);
        _isHealthLowEnabled = healthLow.IsEnabled;
        var healthLowCriteria = (healthLow.HealthLowThresholdIndex, healthLow.HealthLowConditionIndex);
        if (healthLowCriteria != _healthLowCriteria)
        {
            // 前の割合で決めた構えと控えを新しい割合に持ち越さない(選び直した時点で既に割合以下の人には出さない)。
            _healthLowTracker.Reset();
            _healthLowCriteria = healthLowCriteria;
        }

        // 対象を変えたときも、次の名簿を待たずに今の名簿で絞り直す。
        ObserveHealthLow();

        var playerAppearedWatchName = GetAppearanceWatchName(notifications.Get(NotificationKind.PlayerAppeared));
        if (playerAppearedWatchName != _playerAppearedWatchName)
        {
            _playerAppearedWatchName = playerAppearedWatchName;
            _playerAppearedTracker.Rebaseline();
            ObservePlayerAppearance();
        }

        var entityAppearedWatchName = GetAppearanceWatchName(notifications.Get(NotificationKind.EntityAppeared));
        if (entityAppearedWatchName != _entityAppearedWatchName)
        {
            _entityAppearedWatchName = entityAppearedWatchName;
            _entityAppearedTracker.Rebaseline();
            ObserveEntityAppearance();
        }
    }

    /// <summary>出現の項目で見ている名前。オフか名前が空なら null(判定を動かさない)。</summary>
    private static string? GetAppearanceWatchName(NotificationItemConfig item)
    {
        var name = item.TargetName.Trim();
        return item.IsEnabled && name.Length > 0 ? name : null;
    }

    /// <summary>名簿のうち、周りに見えているほかのプレイヤー(自分と履歴の行は除く)を渡す。</summary>
    private void ObservePlayerAppearance()
    {
        if (_playerAppearedWatchName is null || _lastRoster is not { } roster)
        {
            return;
        }

        _nearbyPlayers.Clear();
        foreach (var entry in roster.Entries)
        {
            if (entry.IsNearby && !entry.IsSelf && !entry.IsFromRecord)
            {
                _nearbyPlayers.Add(entry);
            }
        }

        var nameDisplayMode = PlayerRosterPresentationStore.Instance.NameDisplayMode;
        _playerAppearedTracker.Observe(
            _nearbyPlayers,
            entry => entry.CharacterId,
            entry => entry.Name,
            entry => PlayerInfoFormatFormatter.GetDisplayName(
                entry.Name,
                entry.CharacterId,
                entry.IsSelf,
                entry.IsNpc,
                entry.ProfessionId,
                nameDisplayMode));
    }

    /// <summary>周りのエンティティを、表示言語の名前(内部IDの注記なし)で渡す。名前の無い実体は一致しない。</summary>
    private void ObserveEntityAppearance()
    {
        if (_entityAppearedWatchName is null)
        {
            return;
        }

        _entityAppearedTracker.Observe(
            _lastNearbyEntities,
            entity => entity.EntityUuid,
            GetEntityName,
            GetEntityName);

        static string GetEntityName(NearbyEntityEntry entity)
            => CombatDataCatalog.GetEntityNameWithoutInternalId(entity.EntityType, entity.EntityId);
    }

    private void ObserveHealthLow()
    {
        if (!_isHealthLowEnabled)
        {
            _healthLowTracker.Reset();
            _healthLowMapGeneration = -1;
            return;
        }

        if (_lastRoster is not { } roster)
        {
            return;
        }

        if (_healthLowMapGeneration != roster.MapGeneration)
        {
            _healthLowTracker.Reset();
            _healthLowMapGeneration = roster.MapGeneration;
        }

        var mode = (PartyDisplayMode)ConfigManager.Instance.GetNotificationItem(NotificationKind.HealthLow).TargetModeIndex;
        var party = PartyStateStore.Instance.Current;
        var targets = roster.Entries
            .Where(entry => !entry.IsNpc && party.ShouldInclude(entry.CharacterId, entry.IsSelf, mode))
            .ToArray();
        _healthLowTracker.Observe(targets, PlayerRosterPresentationStore.Instance.NameDisplayMode);
    }

    private void TelegraphedSkillTimer_Tick(object? sender, EventArgs e)
    {
        _telegraphedSkillTracker?.Poll();
    }
}
