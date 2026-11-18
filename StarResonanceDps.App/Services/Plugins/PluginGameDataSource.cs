// This file is part of StarResonanceDps. See LICENSING.md for the license, additional permissions and additional terms.

using System.Threading;
using System.Windows;
using StarResonanceDps.Core.CombatRuntime;
using StarResonanceDps.Core.Models;
using StarResonanceDps.Core.Services;
using StarResonanceDps.PluginSdk;

namespace StarResonanceDps.App.Services;

/// <summary>
/// 本体が通信から控えた値を、プラグインへ渡す形(SDK の型)へ写して持つ。全部のプラグインで1つ。
/// 知らせは画面のスレッドで出し、プラグインへの知らせはプラグインごとの口(<see cref="PluginGameDataService"/>)が出す。
///
/// <para>
/// 通信の値の新しい控えは作らない。名簿・周りの敵・モジュールの写しは、本体の控えの中身が同じなら前回の写しを返し、
/// 本体の控えの知らせで作り直す(ログアウト・キャプチャの停止・マップ移動で本体の控えが消えれば、写しも空になる)。
/// 集計の設定の写しは、本体が設定を書き終えた知らせで作り直す。
/// 名簿・周りの敵・接続とマップ・表示中の回・集計の設定・履歴の読み込みの失敗の知らせは画面の1回の更新につき1回にまとめ、
/// 合図(回の始まりと終わりと保存・マップ移動・ログアウト)はまとめずに起きた順に出す。
/// </para>
/// </summary>
internal sealed class PluginGameDataSource
{
    private readonly object _sync = new();
    private readonly CoalescedUiNotification _selfModulesNotification;
    private readonly CoalescedUiNotification _sessionNotification;
    private readonly CoalescedUiNotification _playersNotification;
    private readonly CoalescedUiNotification _entitiesNotification;
    private readonly CoalescedUiNotification _shownEncounterNotification;
    private readonly CoalescedUiNotification _aggregationSettingsNotification;
    private readonly CoalescedUiNotification _historyOpenFailedNotification;

    private IReadOnlyList<PlayerRosterEntry>? _playersSource;
    private IReadOnlyList<PluginPlayer> _players = Array.Empty<PluginPlayer>();
    private IReadOnlyList<NearbyEntityEntry>? _entitiesSource;
    private IReadOnlyList<PluginEntity> _entities = Array.Empty<PluginEntity>();
    private IReadOnlyList<DungeonFlowChange>? _dungeonFlowSource;
    private IReadOnlyList<PluginDungeonFlowChange> _dungeonFlowChanges = Array.Empty<PluginDungeonFlowChange>();

    // 最後に知らせた接続とマップ・表示中の回の番号・集計の設定・履歴の読み込みの失敗(画面のスレッドだけが読み書きする)。中身が変わったときだけ知らせる。
    private PluginSessionState _notifiedSession;
    private long _notifiedShownEncounterKey;
    private PluginAggregationSettings _notifiedAggregationSettings;
    private bool _notifiedHistoryOpenFailed;

    // 集計の設定の写し。本体が設定を書き終えたスレッド(パケットのスレッド)でその場で作り直す。
    // 知らせの予約が走るまでの間も新しい値を返すため、予約の中では作らない。
    private volatile PluginAggregationSettings _aggregationSettings;

    private PluginGameDataSource()
    {
        _selfModulesNotification = new CoalescedUiNotification(RefreshSelfModules);
        _sessionNotification = new CoalescedUiNotification(RefreshSession);
        _playersNotification = new CoalescedUiNotification(() => PlayersChanged?.Invoke(this, EventArgs.Empty));
        _entitiesNotification = new CoalescedUiNotification(() => EntitiesChanged?.Invoke(this, EventArgs.Empty));
        _shownEncounterNotification = new CoalescedUiNotification(RefreshShownEncounter);
        _aggregationSettingsNotification = new CoalescedUiNotification(RefreshAggregationSettings);
        _historyOpenFailedNotification = new CoalescedUiNotification(RefreshHistoryOpenFailed);

        SelfModules = ToPluginInventory(SelfModuleStore.GetSnapshot());
        _notifiedSession = ReadSession();
        _notifiedShownEncounterKey = MeterSnapshotProvider.GetShownEncounter()?.RuntimeKey ?? 0;
        _aggregationSettings = ReadAggregationSettings();
        _notifiedAggregationSettings = _aggregationSettings;
        _notifiedHistoryOpenFailed = EncounterHistoryProvider.HasLoadFailed;

        SelfModuleStore.Changed += _selfModulesNotification.Queue;
        PlayerRosterPresentationStore.Instance.RosterChanged += (_, _) => _playersNotification.Queue();
        NearbyEntityStore.Instance.EntitiesChanged += (_, _) => _entitiesNotification.Queue();
        GameReceptionStateStore.Changed += _sessionNotification.Queue;
        SeasonStateStore.Changed += _sessionNotification.Queue;
        BattleStateMachine.DungeonStateChanged += _sessionNotification.Queue;
        SelfTargetStore.Instance.Changed += (_, _) => _sessionNotification.Queue();
        DungeonTimerStore.Instance.Changed += (_, _) => _sessionNotification.Queue();
        MessageManager.SceneApplied += MessageManager_SceneApplied;
        MessageManager.ResetToStartupCompleted += MessageManager_ResetToStartupCompleted;
        EncounterManager.EncounterStart += _ => Post(() => EncounterStarted?.Invoke(this, EventArgs.Empty));
        EncounterManager.EncounterClosing += EncounterManager_EncounterClosing;
        EncounterManager.EncounterSaved += EncounterManager_EncounterSaved;

        // 表示中の回が替わりうる所: 履歴を開く・ライブへ戻す、新しい回を作る(保持していなければその回を映す)、保持していた前の回を手放す。
        EncounterHistoryProvider.SelectionChanged += _shownEncounterNotification.Queue;
        EncounterManager.EncounterStart += _ => _shownEncounterNotification.Queue();
        EncounterManager.HeldPastEncounterReleased += _shownEncounterNotification.Queue;
        CombatRuntimeSettings.AggregationSettingsApplied += CombatRuntimeSettings_AggregationSettingsApplied;
        EncounterHistoryProvider.LoadFailedChanged += _historyOpenFailedNotification.Queue;
    }

    public static PluginGameDataSource Instance { get; } = new();

    public PluginGameServer CurrentServer => ToPluginServer(GameServerStore.Current);

    public PluginAggregationSettings AggregationSettings => _aggregationSettings;

    public PluginModuleInventory? SelfModules { get; private set; }

    public PluginSessionState Session => ReadSession();

    public IReadOnlyList<PluginPlayer> Players
    {
        get
        {
            var source = PlayerRosterPresentationStore.Instance.Current.Entries;
            lock (_sync)
            {
                if (!ReferenceEquals(source, _playersSource))
                {
                    _players = source.Select(ToPluginPlayer).ToArray();
                    _playersSource = source;
                }

                return _players;
            }
        }
    }

    public IReadOnlyList<PluginEntity> Entities
    {
        get
        {
            var source = NearbyEntityStore.Instance.Current.Entries;
            lock (_sync)
            {
                if (!ReferenceEquals(source, _entitiesSource))
                {
                    _entities = source.Select(ToPluginEntity).ToArray();
                    _entitiesSource = source;
                }

                return _entities;
            }
        }
    }

    /// <summary>以下の知らせは全部画面のスレッドで上がる。</summary>
    public event EventHandler? SelfModulesChanged;

    public event EventHandler? SessionChanged;

    public event EventHandler? PlayersChanged;

    public event EventHandler? EntitiesChanged;

    public event EventHandler? EncounterStarted;

    public event EventHandler<PluginEncounterResult>? EncounterClosed;

    public event EventHandler<PluginEncounterSaved>? EncounterSaved;

    public event EventHandler? ShownEncounterChanged;

    public event EventHandler? AggregationSettingsChanged;

    /// <summary>履歴の読み込みの失敗(<see cref="EncounterHistoryProvider.HasLoadFailed"/>)が変わった。プラグインの履歴の口(<see cref="PluginHistoryService"/>)が受ける。</summary>
    public event EventHandler? HistoryOpenFailedChanged;

    public event EventHandler? MapEntered;

    public event EventHandler? LoggedOut;

    public IReadOnlyList<PluginBuff> GetPlayerBuffs(long characterId)
    {
        return ToPluginBuffs(MeterSnapshotProvider.GetActivePlayerBuffs(characterId));
    }

    public IReadOnlyList<PluginBuff> GetEntityBuffs(long entityUuid)
    {
        return ToPluginBuffs(MeterSnapshotProvider.GetActiveEntityBuffs(entityUuid));
    }

    /// <summary>本体のプレイヤーの窓と同じ行(<see cref="PlayerRosterPresentationStore.GetPlayer"/>、名前は伏せた後)。</summary>
    public PluginPlayer? GetPlayer(long characterId)
    {
        return PlayerRosterPresentationStore.Instance.GetPlayer(characterId) is { } row
            ? ToPluginPlayer(row)
            : null;
    }

    /// <summary>
    /// 本体のプレイヤーの窓と同じ行と、プレイヤーリストの技の枠と同じ技の一覧から作る。呼ぶたびに作り、控えない。
    /// 因子とモジュール効果は、自分なら自分の控え、履歴にだけいる行なら行に保存した値(<see cref="PlayerRosterEntry.RecordedSelf"/>)、他の人は null。
    /// </summary>
    public PluginPlayerBuild? GetPlayerBuild(long characterId)
    {
        if (PlayerRosterPresentationStore.Instance.GetPlayer(characterId) is not { } row)
        {
            return null;
        }

        IReadOnlyList<SelfSeasonFactor>? factors = null;
        IReadOnlyList<SelfModuleEffect>? moduleEffects = null;
        if (row.IsSelf)
        {
            var seasonId = SeasonStateStore.CurrentSeasonId;
            factors = seasonId > 0 ? SelfSeasonCultivateStore.GetActiveFactors(seasonId) : null;
            moduleEffects = SelfModuleStore.GetEquippedEffectLinks();
        }
        else if (row.IsFromRecord)
        {
            factors = row.RecordedSelf?.Factors;
            moduleEffects = row.RecordedSelf?.ModuleEffects;
        }

        return ToPluginBuild(row, MeterSnapshotProvider.GetPlayerSkillLevels(characterId), factors, moduleEffects);
    }

    public PluginEncounterSnapshot GetEncounter()
    {
        return ToPluginEncounter(MeterSnapshotProvider.GetEncounterTotals());
    }

    public IReadOnlyList<PluginSkillTotals> GetSkillTotals(long characterId, PluginCombatKind kind)
    {
        return ToPluginSkills(MeterSnapshotProvider.GetPlayerSkillTable(ToMeterKind(kind), characterId));
    }

    private void RefreshSelfModules()
    {
        SelfModules = ToPluginInventory(SelfModuleStore.GetSnapshot());
        SelfModulesChanged?.Invoke(this, EventArgs.Empty);
    }

    private void RefreshSession()
    {
        var session = ReadSession();
        if (session == _notifiedSession)
        {
            return;
        }

        _notifiedSession = session;
        SessionChanged?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>履歴の回は開くたびに読み直して別の回(別の番号)になるので、同じ回を開き直しても知らせる。</summary>
    private void RefreshShownEncounter()
    {
        var shownEncounterKey = MeterSnapshotProvider.GetShownEncounter()?.RuntimeKey ?? 0;
        if (shownEncounterKey == _notifiedShownEncounterKey)
        {
            return;
        }

        _notifiedShownEncounterKey = shownEncounterKey;
        ShownEncounterChanged?.Invoke(this, EventArgs.Empty);
    }

    private void RefreshAggregationSettings()
    {
        var settings = _aggregationSettings;
        if (settings == _notifiedAggregationSettings)
        {
            return;
        }

        _notifiedAggregationSettings = settings;
        AggregationSettingsChanged?.Invoke(this, EventArgs.Empty);
    }

    private void RefreshHistoryOpenFailed()
    {
        var hasFailed = EncounterHistoryProvider.HasLoadFailed;
        if (hasFailed == _notifiedHistoryOpenFailed)
        {
            return;
        }

        _notifiedHistoryOpenFailed = hasFailed;
        HistoryOpenFailedChanged?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>パケットのスレッドから来る(キャプチャが動いていなければ設定を保存したスレッド)。値が変わらなくても来るので、知らせるかは予約の中で比べる。</summary>
    private void CombatRuntimeSettings_AggregationSettingsApplied()
    {
        _aggregationSettings = ReadAggregationSettings();
        _aggregationSettingsNotification.Queue();
    }

    private void MessageManager_SceneApplied(bool isNewMap)
    {
        _sessionNotification.Queue();
        if (isNewMap)
        {
            Post(() => MapEntered?.Invoke(this, EventArgs.Empty));
        }
    }

    private void MessageManager_ResetToStartupCompleted()
    {
        _sessionNotification.Queue();
        Post(() => LoggedOut?.Invoke(this, EventArgs.Empty));
    }

    /// <summary>
    /// パケットのスレッドから来る。閉じた回にはもう記録が入らないので、結果は画面のスレッドで作る(パケットの処理を待たせない)。
    /// </summary>
    private void EncounterManager_EncounterClosing(Encounter encounter)
    {
        Post(() => EncounterClosed?.Invoke(this, CreateEncounterResult(encounter)));
    }

    /// <summary>
    /// 保存したスレッドから来る(作り直しの保存はパケットのスレッドで、閉じる知らせの後・次の回の始まりの前に予約される)。番号はここで写す。
    /// アプリを閉じるときの最後の回の保存では、予約が走る前にプラグインを知らせから外している。
    /// </summary>
    private void EncounterManager_EncounterSaved(Encounter encounter)
    {
        var saved = new PluginEncounterSaved(encounter.RuntimeKey, encounter.EncounterId);
        Post(() => EncounterSaved?.Invoke(this, saved));
    }

    private static void Post(Action action)
    {
        // 終了の途中で画面が無ければ、知らせる相手も無い。
        Application.Current?.Dispatcher.BeginInvoke(action);
    }

    private PluginSessionState ReadSession()
    {
        var selfCharacterId = AppState.PlayerUID;
        var sceneId = EncounterManager.LevelMapId;
        var seasonId = SeasonStateStore.CurrentSeasonId;
        var dungeonState = BattleStateMachine.IsInOpenWorld()
            ? PluginDungeonState.NotInDungeon
            : BattleStateMachine.IsDungeonFinished()
                ? PluginDungeonState.Finished
                : PluginDungeonState.InDungeon;
        var targetUuid = SelfTargetStore.Instance.TargetUuid;
        var flowChanges = ReadDungeonFlowChanges();
        return new PluginSessionState(
            selfCharacterId != 0 ? selfCharacterId : null,
            sceneId != 0 ? (int)sceneId : null,
            seasonId != 0 ? seasonId : null,
            dungeonState,
            GameReceptionStateStore.Current.IsMidLaunch)
        {
            SelfTargetEntityUuid = targetUuid != 0 ? targetUuid : null,
            DungeonFlowState = flowChanges.Count > 0 ? flowChanges[^1].State : null,
            DungeonFlowChanges = flowChanges,
            DungeonTimer = ToPluginDungeonTimer(DungeonTimerStore.Instance.Current),
        };
    }

    /// <summary>ゲームのダンジョンの時計の写し。開始時刻はこの端末の時計に直す(サーバの時刻をまだ受け取っていなければ null)。</summary>
    private static PluginDungeonTimer? ToPluginDungeonTimer(DungeonTimerSnapshot? timer)
    {
        if (timer is null)
        {
            return null;
        }

        return new PluginDungeonTimer(
            timer.StartServerUnixSeconds,
            DungeonTimerStore.TryToLocalUtc(timer.StartServerUnixSeconds, out var startUtc) ? startUtc : null,
            timer.LimitSeconds,
            timer.CountsDown,
            timer.PausedTotalSeconds,
            timer.PausedAtSeconds);
    }

    /// <summary>
    /// ダンジョンの進行の変化の写し。Core の一覧は書き換えずに差し替えるので、参照が同じなら前回の写しを返す
    /// (接続の状態の値の比べが、中身の同じ一覧で空振りしないように)。持つのは1組だけ。
    /// </summary>
    private IReadOnlyList<PluginDungeonFlowChange> ReadDungeonFlowChanges()
    {
        var source = BattleStateMachine.DungeonFlowChanges;
        lock (_sync)
        {
            if (!ReferenceEquals(source, _dungeonFlowSource))
            {
                _dungeonFlowChanges = source
                    .Select(change => new PluginDungeonFlowChange((PluginDungeonFlowState)(int)change.State, change.ArrivalUtc))
                    .ToArray();
                _dungeonFlowSource = source;
            }

            return _dungeonFlowChanges;
        }
    }

    /// <summary>集計の設定(<see cref="CombatRuntimeSettings"/> の集計の8項目)の写し。</summary>
    private static PluginAggregationSettings ReadAggregationSettings()
    {
        return new PluginAggregationSettings(
            CombatRuntimeSettings.StopAggregationInNonCombatAreas,
            CombatRuntimeSettings.SplitEncountersOnNewPhases,
            CombatRuntimeSettings.KeepPastEncounterInMeterUntilNextDamage,
            CombatRuntimeSettings.ClearHistorySelectionOnNextEvent,
            CombatRuntimeSettings.CombatExitSeconds,
            CombatRuntimeSettings.BenchmarkDurationSeconds,
            CombatRuntimeSettings.BenchmarkFirstTargetOnly,
            CombatRuntimeSettings.DatabaseMaxEncounterCount);
    }

    internal static PluginPlayer ToPluginPlayer(PlayerRosterEntry entry)
    {
        var specState = entry.ClassSpec switch
        {
            PlayerClassSpec.Unknown => PluginSpecState.Unknown,
            PlayerClassSpec.Rank1 => PluginSpecState.NotEquipped,
            _ when PlayerClassSpecResolver.IsTransformation(entry.ClassSpec) => PluginSpecState.Transformation,
            _ => PluginSpecState.Specialized,
        };

        return new PluginPlayer(
            entry.CharacterId,
            entry.Name,
            entry.ProfessionId,
            specState,
            specState == PluginSpecState.Specialized ? entry.SubProfessionId : null,
            entry.Level,
            entry.CombatPower,
            entry.SeasonStrength,
            entry.CurrentHp,
            entry.MaxHp,
            entry.CurrentShield,
            entry.PartyNumber,
            entry.IsSelf,
            entry.IsNpc,
            entry.IsPartyMember,
            entry.IsNearby,
            entry.IsInCombat,
            entry.IsIncapacitated,
            entry.SeasonTalentBuffId != 0 ? entry.SeasonTalentBuffId : null,
            entry.IsSeasonTalentInactive,
            entry.IsFromRecord);
    }

    /// <summary>
    /// 名簿の行と、その人の技の一覧・因子・モジュール効果をビルドの写しにする。一覧は全部写し、本体の一覧を渡さない。
    /// 装備は届いていなければ null、読めない形なら読めないことと空の一覧。
    /// </summary>
    internal static PluginPlayerBuild ToPluginBuild(
        PlayerRosterEntry row,
        IReadOnlyList<Core.CombatRuntime.DataTypes.Skills.SkillLevelInfo>? skillLevels,
        IReadOnlyList<SelfSeasonFactor>? factors,
        IReadOnlyList<SelfModuleEffect>? moduleEffects)
    {
        var equipment = row.EquipmentData?.State switch
        {
            PlayerEquipmentDataState.Available => new PluginEquipment(
                true,
                row.EquipmentData.Items.Select(item => new PluginEquipmentItem(item.Slot, item.EquipmentId)).ToArray()),
            PlayerEquipmentDataState.Invalid => new PluginEquipment(false, Array.Empty<PluginEquipmentItem>()),
            _ => null,
        };

        return new PluginPlayerBuild(
            row.CharacterId,
            row.RootBuffData?.BuffIds.ToArray(),
            row.TalentNodeIds?.ToArray(),
            row.LastReplaceableSkills is { } replaceable ? new Dictionary<int, int>(replaceable) : null,
            equipment,
            skillLevels?.Select(level => new PluginSkillLevel(level.SkillId, level.CurrentLevel, level.Tier)).ToArray(),
            factors?.Select(factor => new PluginSeasonFactor(factor.SlotId, factor.ItemId)).ToArray(),
            moduleEffects?.Select(effect => new PluginModuleEffect(effect.EffectId, effect.LinkCount)).ToArray());
    }

    private static PluginEntity ToPluginEntity(NearbyEntityEntry entry)
    {
        var hateRanking = new PluginHateRank[entry.HateRanking.Count];
        for (var index = 0; index < hateRanking.Length; index++)
        {
            var rank = entry.HateRanking[index];
            hateRanking[index] = new PluginHateRank(rank.CharacterId, rank.HateValue);
        }

        return new PluginEntity(
            entry.EntityUuid,
            entry.EntityId > 0 ? (int)entry.EntityId : null,
            entry.CurrentHp,
            entry.MaxHp,
            entry.IsInCombat,
            entry.Cast is { } cast ? new PluginEntityCast(cast.SkillId, cast.StartedAtUtc, cast.TotalSeconds) : null,
            entry.TargetCharacterId != 0 ? entry.TargetCharacterId : null,
            hateRanking)
        {
            Level = entry.Level,
            CurrentShield = entry.CurrentShield,
        };
    }

    private static IReadOnlyList<PluginBuff> ToPluginBuffs(IReadOnlyList<ActiveBuffSnapshot> buffs)
    {
        return buffs.Count == 0
            ? Array.Empty<PluginBuff>()
            : buffs
                .Select(buff => new PluginBuff(
                    buff.BuffUuid,
                    buff.BuffId,
                    buff.Level,
                    buff.Layer,
                    buff.DurationSeconds,
                    buff.EndUtc,
                    buff.IsRemainingUnknown,
                    buff.CasterEntityUuid,
                    buff.CasterCharacterId))
                .ToArray();
    }

    internal static PluginEncounterSnapshot ToPluginEncounter(EncounterTotalsSnapshot totals)
    {
        return new PluginEncounterSnapshot(
            totals.IsHistory,
            totals.StartUtc,
            totals.Elapsed,
            totals.IsClosed,
            totals.Players
                .Select(player => new PluginEncounterPlayer(
                    player.CharacterId,
                    ToPluginTotals(player.Damage),
                    ToPluginTotals(player.Healing),
                    ToPluginTotals(player.DamageTaken)))
                .ToArray())
        {
            EncounterKey = totals.EncounterKey,
            HistoryId = totals.HistoryId,
            LastCombatEventUtc = totals.LastCombatEventUtc,
            IsAggregationStopped = totals.IsAggregationStopped,
            BenchmarkFirstTargetEntityUuid = totals.BenchmarkFirstTargetUuid,
            ClockOrigin = (PluginClockOrigin)(int)totals.ClockOrigin,
            FirstCombatEventUtc = totals.FirstCombatEventUtc,
        };
    }

    /// <summary>
    /// 回の集計以外の値。回を作った時刻は回が持つローカルの時刻(DB から読んだ値は種類の無い時刻で、ローカルとして直る)を UTC へ直す。
    /// 計測を最初の敵だけで数えたかは DB に載らないので、DB から読んだ回では null。
    /// </summary>
    internal static PluginEncounterInfo ToPluginInfo(Encounter encounter)
    {
        var exData = encounter.ExData;
        return new PluginEncounterInfo(
            encounter.StartTime.ToUniversalTime(),
            encounter.SceneId != 0 ? checked((int)encounter.SceneId) : null,
            exData.DungeonDifficulty,
            exData.ChannelLine != 0 ? checked((int)exData.ChannelLine) : null,
            ToPluginServer(exData.GameServer),
            exData.SeasonId != 0 ? exData.SeasonId : null,
            encounter.IsWipe,
            exData.BenchmarkTime,
            encounter.IsFromHistory ? null : encounter.BenchmarkFirstTargetOnly,
            exData.CombatExitSeconds);
    }

    internal static IReadOnlyList<PluginRecordedMonster> ToPluginMonsters(IReadOnlyList<RecordedMonsterSnapshot> monsters)
    {
        return monsters.Count == 0
            ? Array.Empty<PluginRecordedMonster>()
            : monsters
                .Select(monster => new PluginRecordedMonster(
                    monster.EntityUuid,
                    monster.MonsterId,
                    monster.Hp,
                    monster.MaxHp,
                    monster.Casts.Count == 0
                        ? Array.Empty<PluginRecordedCast>()
                        : monster.Casts
                            .Select(cast => new PluginRecordedCast(
                                cast.SkillId,
                                DateTime.SpecifyKind(cast.Timestamp, DateTimeKind.Utc),
                                cast.IsWarning))
                            .ToArray()))
                .ToArray();
    }

    internal static PluginBuffIntervals? ToPluginBuffIntervals(BuffIntervalsSnapshot? intervals)
    {
        return intervals is null
            ? null
            : new PluginBuffIntervals(
                intervals.Buffs
                    .Select(buff => new PluginBuffInterval(buff.BuffId, buff.IsPermanent, ToPluginIntervals(buff.Intervals)))
                    .ToArray(),
                ToPluginIntervals(intervals.PresentIntervals));
    }

    private static IReadOnlyList<PluginTimeInterval> ToPluginIntervals(IReadOnlyList<UtcInterval> intervals)
    {
        return intervals.Count == 0
            ? Array.Empty<PluginTimeInterval>()
            : intervals
                .Select(interval => new PluginTimeInterval(interval.StartUtc, interval.EndUtc))
                .ToArray();
    }

    private static PluginCombatTotals ToPluginTotals(CombatTotalsSnapshot totals)
    {
        return new PluginCombatTotals(
            totals.Total,
            totals.PerSecond,
            totals.ActivePerSecond,
            totals.Hits,
            totals.CritTotal,
            totals.CritCount,
            totals.LuckyTotal,
            totals.LuckyCount);
    }

    internal static IReadOnlyList<PluginSkillTotals> ToPluginSkills(MetricSkillTableSnapshot table)
    {
        return table.Entries.Count == 0
            ? Array.Empty<PluginSkillTotals>()
            : table.Entries
                .Select(row => new PluginSkillTotals(
                    row.SkillIdText,
                    string.IsNullOrEmpty(row.PlainName) ? null : row.PlainName,
                    row.TotalValue,
                    row.ValuePerSecond,
                    row.ValuePerSecondActive,
                    row.HitCount,
                    row.CritRate,
                    row.Percentage))
                .ToArray();
    }

    /// <summary>
    /// 画面のスレッドで作るので、保存が先に済んでいることも済んでいないこともある。保存の番号は入れず(いつも null)、<see cref="EncounterSaved"/> で渡す。
    /// </summary>
    private static PluginEncounterResult CreateEncounterResult(Encounter encounter)
    {
        var totals = ToPluginEncounter(MeterSnapshotProvider.GetEncounterTotals(encounter)) with { HistoryId = null };
        var skills = totals.Players.ToDictionary(
            player => player.CharacterId,
            player => new PluginPlayerSkills(
                ToPluginSkills(MeterSnapshotProvider.GetPlayerSkillTable(encounter, MeterSnapshotKind.Damage, player.CharacterId)),
                ToPluginSkills(MeterSnapshotProvider.GetPlayerSkillTable(encounter, MeterSnapshotKind.Healing, player.CharacterId))));
        return new PluginEncounterResult(totals, skills);
    }

    internal static MeterSnapshotKind ToMeterKind(PluginCombatKind kind)
    {
        return kind switch
        {
            PluginCombatKind.Damage => MeterSnapshotKind.Damage,
            PluginCombatKind.Healing => MeterSnapshotKind.Healing,
            _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, "Unknown combat kind."),
        };
    }

    private static PluginModuleInventory? ToPluginInventory(SelfModuleInventory? inventory)
    {
        if (inventory is null)
        {
            return null;
        }

        var modules = inventory.Modules
            .Select(module => new PluginModuleItem(
                module.Uuid,
                module.ConfigId,
                module.Quality,
                module.Effects.Select(effect => new PluginModuleEffect(effect.EffectId, effect.LinkCount)).ToArray()))
            .ToArray();
        return new PluginModuleInventory(modules, inventory.EquippedUuidBySlot);
    }

    internal static PluginGameServer ToPluginServer(GameServer server)
    {
        return server switch
        {
            GameServer.Unknown => PluginGameServer.Unknown,
            GameServer.Asia => PluginGameServer.Asia,
            GameServer.China => PluginGameServer.China,
            GameServer.Global => PluginGameServer.Global,
            GameServer.SoutheastAsia => PluginGameServer.SoutheastAsia,
            _ => throw new ArgumentOutOfRangeException(nameof(server), server, "The game server has no plugin equivalent."),
        };
    }

    /// <summary>
    /// ほかのスレッドから来た知らせを、画面のスレッドへの予約1回にまとめる。予約が走った時点の値で知らせる。
    /// </summary>
    private sealed class CoalescedUiNotification
    {
        private readonly Action _notify;
        private int _isQueued;

        public CoalescedUiNotification(Action notify)
        {
            _notify = notify;
        }

        public void Queue()
        {
            if (Interlocked.Exchange(ref _isQueued, 1) == 1)
            {
                return;
            }

            var dispatcher = Application.Current?.Dispatcher;
            if (dispatcher is null)
            {
                // 終了の途中で画面が無い。知らせる相手も無い。
                Interlocked.Exchange(ref _isQueued, 0);
                return;
            }

            dispatcher.BeginInvoke(() =>
            {
                Interlocked.Exchange(ref _isQueued, 0);
                _notify();
            });
        }
    }
}
