using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using StarResonanceDps.PluginSdk;

namespace SampleGameData;

/// <summary>
/// Shows every value of <see cref="IPluginGameDataService"/> as text.
/// Buffs and encounter totals have no change event, so the view reads them again once a second while the window is open.
/// The view does not keep the encounter objects (<see cref="IPluginEncounterRecord"/>); it gets them again each second,
/// because a kept object keeps its encounter in memory.
/// The app disposes the view after the window closes, because it implements <see cref="IDisposable"/>.
/// </summary>
internal sealed class GameDataView : UserControl, IDisposable
{
    private const int MaxListedRows = 20;

    private readonly IPluginContext _context;
    private readonly IPluginGameDataService _data;
    private readonly TextBlock _text = new() { Margin = new Thickness(24, 8, 24, 24), TextWrapping = TextWrapping.Wrap };
    private readonly DispatcherTimer _timer = new() { Interval = TimeSpan.FromSeconds(1) };
    private int _sessionChanges;
    private int _playerChanges;
    private int _entityChanges;
    private int _moduleChanges;
    private int _encountersStarted;
    private int _encountersClosed;
    private int _encountersSaved;
    private int _shownEncounterChanges;
    private int _aggregationSettingsChanges;
    private int _mapsEntered;
    private int _logouts;
    private string _lastEncounterResult = "(none)";
    private string _lastSaved = "(none)";
    private HashSet<long> _listedCharacterIds = [];
    private long _lastLeftCharacterId;
    private long _recordsEncounterKey;
    private long _recordsSelfId;
    private int _selfActivationCount;
    private PluginSkillActivation? _lastSelfActivation;
    private int _takenHitCount;
    private int _combatLogCount;

    public GameDataView(IPluginContext context)
    {
        _context = context;
        _data = context.GameData;

        _text.SetResourceReference(TextBlock.ForegroundProperty, PluginResourceKeys.Brushes.TextPrimary);
        var scrollViewer = new ScrollViewer { Content = _text, VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
        scrollViewer.SetResourceReference(StyleProperty, PluginResourceKeys.Scrolling.ScrollViewer);
        Content = scrollViewer;

        _data.SessionChanged += Data_SessionChanged;
        _data.PlayersChanged += Data_PlayersChanged;
        _data.EntitiesChanged += Data_EntitiesChanged;
        _data.SelfModulesChanged += Data_SelfModulesChanged;
        _data.EncounterStarted += Data_EncounterStarted;
        _data.EncounterClosed += Data_EncounterClosed;
        _data.EncounterSaved += Data_EncounterSaved;
        _data.ShownEncounterChanged += Data_ShownEncounterChanged;
        _data.AggregationSettingsChanged += Data_AggregationSettingsChanged;
        _data.MapEntered += Data_MapEntered;
        _data.LoggedOut += Data_LoggedOut;

        _timer.Tick += Timer_Tick;
        _timer.Start();
        Refresh();
    }

    public void Dispose()
    {
        _timer.Stop();
        _timer.Tick -= Timer_Tick;

        _data.SessionChanged -= Data_SessionChanged;
        _data.PlayersChanged -= Data_PlayersChanged;
        _data.EntitiesChanged -= Data_EntitiesChanged;
        _data.SelfModulesChanged -= Data_SelfModulesChanged;
        _data.EncounterStarted -= Data_EncounterStarted;
        _data.EncounterClosed -= Data_EncounterClosed;
        _data.EncounterSaved -= Data_EncounterSaved;
        _data.ShownEncounterChanged -= Data_ShownEncounterChanged;
        _data.AggregationSettingsChanged -= Data_AggregationSettingsChanged;
        _data.MapEntered -= Data_MapEntered;
        _data.LoggedOut -= Data_LoggedOut;
    }

    private void Data_SessionChanged(object? sender, EventArgs e) => _sessionChanges++;

    private void Data_PlayersChanged(object? sender, EventArgs e)
    {
        _playerChanges++;

        // Remember the last player who left the list, to read them with GetPlayer.
        var listed = _data.Players.Select(player => player.CharacterId).ToHashSet();
        foreach (var characterId in _listedCharacterIds)
        {
            if (!listed.Contains(characterId))
            {
                _lastLeftCharacterId = characterId;
            }
        }

        _listedCharacterIds = listed;
    }

    private void Data_EntitiesChanged(object? sender, EventArgs e) => _entityChanges++;

    private void Data_SelfModulesChanged(object? sender, EventArgs e) => _moduleChanges++;

    private void Data_EncounterStarted(object? sender, EventArgs e) => _encountersStarted++;

    private void Data_MapEntered(object? sender, EventArgs e) => _mapsEntered++;

    private void Data_LoggedOut(object? sender, EventArgs e) => _logouts++;

    private void Data_ShownEncounterChanged(object? sender, EventArgs e) => _shownEncounterChanges++;

    private void Data_AggregationSettingsChanged(object? sender, EventArgs e) => _aggregationSettingsChanges++;

    /// <summary>
    /// The meters show a closed encounter only for a while, and the saved encounter can be read again only after
    /// <see cref="IPluginGameDataService.EncounterSaved"/> (<see cref="IPluginHistoryService.LoadAsync"/>),
    /// so keep what you need from the result here.
    /// </summary>
    private void Data_EncounterClosed(object? sender, PluginEncounterResult result)
    {
        _encountersClosed++;
        _lastEncounterResult = $"key {result.Totals.EncounterKey}, {result.Totals.Elapsed:mm\\:ss}, {result.Totals.Players.Count} players";
    }

    /// <summary>The encounter key matches the one of <see cref="Data_EncounterClosed"/>.</summary>
    private void Data_EncounterSaved(object? sender, PluginEncounterSaved saved)
    {
        _encountersSaved++;
        _lastSaved = $"key {saved.EncounterKey} -> history ID {saved.HistoryId}";
    }

    private void Timer_Tick(object? sender, EventArgs e)
    {
        Refresh();
    }

    private void Refresh()
    {
        var builder = new StringBuilder();
        AppendSession(builder);
        AppendPlayers(builder);
        AppendSelfBuild(builder);
        AppendEntities(builder);
        AppendEncounter(builder);
        AppendShownEncounter(builder);
        AppendLiveEncounter(builder);
        AppendAggregationSettings(builder);
        AppendModules(builder);
        AppendEvents(builder);
        _text.Text = builder.ToString();
    }

    private void AppendSession(StringBuilder builder)
    {
        var session = _data.Session;
        var sceneName = session.SceneId is { } sceneId ? _context.Names.GetSceneName(sceneId) : null;
        builder.AppendLine("[Session]");
        builder.AppendLine($"Server: {_data.CurrentServer}");
        builder.AppendLine($"Self character ID: {session.SelfCharacterId}");
        builder.AppendLine($"Scene: {session.SceneId} {sceneName}");
        builder.AppendLine($"Season: {session.SeasonId}");
        builder.AppendLine($"Dungeon state: {session.DungeonState}");
        builder.AppendLine($"Started mid-game: {session.IsMidLaunch}");
        builder.AppendLine($"Your target: {session.SelfTargetEntityUuid?.ToString() ?? "(none)"}");
        var lastFlowChange = session.DungeonFlowChanges.Count > 0 ? session.DungeonFlowChanges[^1] : null;
        builder.AppendLine($"Dungeon flow: {session.DungeonFlowState?.ToString() ?? "(none)"}  changes {session.DungeonFlowChanges.Count}  last at {lastFlowChange?.ArrivalUtc.ToLocalTime():HH:mm:ss}");
        builder.AppendLine();
    }

    private void AppendSelfBuild(StringBuilder builder)
    {
        builder.AppendLine("[Your build]");
        if (_data.Session.SelfCharacterId is { } selfId && _data.GetPlayerBuild(selfId) is { } build)
        {
            // null means "not received"; an empty list was received empty.
            static string Count<T>(IReadOnlyCollection<T>? items) => items?.Count.ToString() ?? "not received";
            builder.AppendLine($"Root buffs {Count(build.RootBuffIds)}  talent nodes {Count(build.TalentNodeIds)}  replaced skills {Count(build.LastUsedReplaceableSkills)}");
            builder.AppendLine($"Equipment {(build.Equipment is { } equipment ? (equipment.IsReadable ? equipment.Items.Count.ToString() : "unreadable") : "not received")}  skills {Count(build.SkillLevels)}  factors {Count(build.Factors)}  module effects {Count(build.ModuleEffects)}");
        }
        else
        {
            builder.AppendLine("(unknown)");
        }

        if (_lastLeftCharacterId != 0)
        {
            var left = _data.GetPlayer(_lastLeftCharacterId);
            var leftBuild = _data.GetPlayerBuild(_lastLeftCharacterId);
            builder.AppendLine($"Last player who left the list: {left?.Name ?? "(none)"}  root buffs {leftBuild?.RootBuffIds?.Count.ToString() ?? "-"}");
        }

        builder.AppendLine();
    }

    private void AppendPlayers(StringBuilder builder)
    {
        var players = _data.Players;
        builder.AppendLine($"[Players: {players.Count}]");
        foreach (var player in players.Take(MaxListedRows))
        {
            var spec = player.SpecId is { } specId ? _context.Names.GetClassSpecName(specId) : player.SpecState.ToString();
            builder.AppendLine($"{player.Name}  {spec}  HP {player.CurrentHp}/{player.MaxHp}  score {player.AbilityScore}  buffs {_data.GetPlayerBuffs(player.CharacterId).Count}");
        }

        var self = players.FirstOrDefault(player => player.IsSelf);
        if (self is not null)
        {
            builder.AppendLine("Your buffs:");
            foreach (var buff in _data.GetPlayerBuffs(self.CharacterId).Take(MaxListedRows))
            {
                var seconds = buff.EndUtc is { } end ? $"{Math.Max((end - DateTime.UtcNow).TotalSeconds, 0):0}s" : "-";
                builder.AppendLine($"  {_context.Names.GetBuffName(buff.BuffId) ?? buff.BuffId.ToString()}  x{buff.Layer}  {seconds}");
            }
        }

        builder.AppendLine();
    }

    private void AppendEntities(StringBuilder builder)
    {
        var entities = _data.Entities;
        builder.AppendLine($"[Entities: {entities.Count}]");
        foreach (var entity in entities.Take(MaxListedRows))
        {
            var name = entity.MonsterId is { } monsterId ? _context.Names.GetMonsterName(monsterId) : null;
            var cast = entity.Cast is { } current ? $"  casting {_context.Names.GetSkillName(current.SkillId)}" : string.Empty;
            builder.AppendLine($"{name ?? "?"}  Lv {entity.Level}  HP {entity.CurrentHp}/{entity.MaxHp}  shield {entity.CurrentShield}  combat {entity.IsInCombat}  hate {entity.HateRanking.Count}  buffs {_data.GetEntityBuffs(entity.EntityUuid).Count}{cast}");
        }

        builder.AppendLine();
    }

    private void AppendEncounter(StringBuilder builder)
    {
        var encounter = _data.GetEncounter();
        builder.AppendLine($"[Encounter: {encounter.Elapsed:mm\\:ss}, history {encounter.IsHistory}, closed {encounter.IsClosed}]");
        builder.AppendLine($"Encounter key {encounter.EncounterKey}, history ID {FormatHistoryId(encounter.HistoryId)}, last combat event {FormatTime(encounter.LastCombatEventUtc)}, aggregation stopped {encounter.IsAggregationStopped}");
        foreach (var player in encounter.Players.OrderByDescending(player => player.Damage.Total).Take(MaxListedRows))
        {
            var topSkill = _data.GetSkillTotals(player.CharacterId, PluginCombatKind.Damage).FirstOrDefault();
            builder.AppendLine($"{player.CharacterId}  damage {player.Damage.Total} ({player.Damage.PerSecond:0}/s)  healing {player.Healing.Total}  taken {player.DamageTaken.Total}  top skill {topSkill?.Name}");
        }

        builder.AppendLine();
    }

    private void AppendShownEncounter(StringBuilder builder)
    {
        builder.AppendLine("[Shown encounter]");
        var shown = _data.GetShownEncounter();
        if (shown is null)
        {
            builder.AppendLine("(none)");
            builder.AppendLine();
            return;
        }

        var info = shown.Info;
        var sceneName = info.SceneId is { } sceneId ? _context.Names.GetSceneName(sceneId, info.DungeonDifficulty) : null;
        builder.AppendLine($"Encounter key {shown.EncounterKey}, history ID {FormatHistoryId(shown.HistoryId)}, from history {shown.IsHistory}, created {FormatTime(info.CreatedUtc)}");
        builder.AppendLine($"Scene: {info.SceneId} {sceneName}, channel {info.ChannelLine}, server {info.Server}, season {info.SeasonId}");
        builder.AppendLine($"Wipe {info.IsWipe}, auto pause {info.AutoPauseSeconds}s, benchmark {info.BenchmarkSeconds}s, first target only {info.BenchmarkFirstTargetOnly?.ToString() ?? "(not saved)"}");
        AppendShownEncounterRecords(builder, shown);
        builder.AppendLine();
    }

    /// <summary>
    /// Reads the records of the shown encounter from where the previous read stopped: the counts are kept while the encounter
    /// (and the local player) stays the same, and start again from 0 when it changes, for example when a history encounter is opened.
    /// </summary>
    private void AppendShownEncounterRecords(StringBuilder builder, IPluginEncounterRecord shown)
    {
        var selfId = _data.Session.SelfCharacterId ?? 0;
        if (shown.EncounterKey != _recordsEncounterKey || selfId != _recordsSelfId)
        {
            _recordsEncounterKey = shown.EncounterKey;
            _recordsSelfId = selfId;
            _selfActivationCount = 0;
            _lastSelfActivation = null;
            _takenHitCount = 0;
            _combatLogCount = 0;
        }

        if (selfId != 0)
        {
            var activations = shown.GetSkillActivations(selfId, _selfActivationCount);
            _selfActivationCount += activations.Count;
            if (activations.Count > 0)
            {
                _lastSelfActivation = activations[^1];
            }
        }

        _takenHitCount += shown.GetTakenHits(_takenHitCount).Count;
        _combatLogCount += shown.GetCombatLog(_combatLogCount).Count;

        var lastSkill = _lastSelfActivation is { } last
            ? $"{_context.Names.GetSkillActivationName(last.SkillId) ?? last.SkillId.ToString()} ({last.Frame})"
            : "(none)";
        builder.AppendLine($"Your skills {_selfActivationCount}, last {lastSkill}  damage taken records {_takenHitCount}  Timeline records {_combatLogCount}");

        var perSecond = selfId != 0 ? shown.GetPerSecondTotals(selfId, PluginCombatKind.Damage) : null;
        builder.AppendLine(perSecond is null
            ? "Your damage per second: (none)"
            : $"Your damage per second: {perSecond.Totals.Count} seconds, last {(perSecond.Totals.Count > 0 ? perSecond.Totals[^1] : 0)}");
    }

    /// <summary>
    /// The live encounter differs from the shown one while a history encounter is open
    /// and while the meters keep showing the previous encounter.
    /// </summary>
    private void AppendLiveEncounter(StringBuilder builder)
    {
        builder.AppendLine("[Live encounter]");
        var live = _data.GetLiveEncounter();
        if (live is null)
        {
            builder.AppendLine("(none)");
            builder.AppendLine();
            return;
        }

        var totals = live.GetTotals();
        var now = DateTime.UtcNow;
        builder.AppendLine($"Encounter key {totals.EncounterKey}, started {FormatTime(totals.StartUtc)}, last combat event {FormatTime(totals.LastCombatEventUtc)}, closed {totals.IsClosed}, aggregation stopped {totals.IsAggregationStopped}");
        builder.AppendLine($"Combat time now: {live.ToCombatOffset(now)?.ToString(@"mm\:ss") ?? "(not started)"}");
        builder.AppendLine($"Benchmark: {GetBenchmarkState(live.Info, totals, now)}, first target {totals.BenchmarkFirstTargetEntityUuid?.ToString() ?? "(none)"}");
        builder.AppendLine();
    }

    /// <summary>A benchmark ends when its duration has passed; the encounter does not close then.</summary>
    private static string GetBenchmarkState(PluginEncounterInfo info, PluginEncounterSnapshot totals, DateTime utcNow)
    {
        if (info.BenchmarkSeconds == 0)
        {
            return "off";
        }

        if (totals.StartUtc is not { } start)
        {
            return "waiting for the first event";
        }

        return totals.IsClosed || utcNow >= start.AddSeconds(info.BenchmarkSeconds) ? "finished" : "running";
    }

    private void AppendAggregationSettings(StringBuilder builder)
    {
        var settings = _data.AggregationSettings;
        builder.AppendLine("[Aggregation settings]");
        builder.AppendLine($"Stop in non-combat areas {settings.StopInNonCombatAreas}, split on progress {settings.SplitOnProgress}");
        builder.AppendLine($"Keep result until next event {settings.KeepResultUntilNextEvent}, return to live on next event {settings.ReturnToLiveOnNextEvent}");
        builder.AppendLine($"Auto pause {settings.AutoPauseSeconds}s, benchmark {settings.BenchmarkSeconds}s, first target only {settings.BenchmarkFirstTargetOnly}");
        builder.AppendLine($"Max saved encounters {settings.MaxSavedEncounters}");
        builder.AppendLine();
    }

    private void AppendModules(StringBuilder builder)
    {
        var modules = _data.SelfModules;
        builder.AppendLine("[Modules]");
        builder.AppendLine(modules is null
            ? "Not received yet"
            : $"{modules.Modules.Count} owned, {modules.EquippedUuidBySlot.Count} equipped");
        builder.AppendLine();
    }

    private void AppendEvents(StringBuilder builder)
    {
        builder.AppendLine("[Events since the window opened]");
        builder.AppendLine($"Session {_sessionChanges}, players {_playerChanges}, entities {_entityChanges}, modules {_moduleChanges}");
        builder.AppendLine($"Encounters started {_encountersStarted}, closed {_encountersClosed}, last result {_lastEncounterResult}");
        builder.AppendLine($"Encounters saved {_encountersSaved}, last saved {_lastSaved}");
        builder.AppendLine($"Shown encounter changes {_shownEncounterChanges}, aggregation settings changes {_aggregationSettingsChanges}");
        builder.AppendLine($"Maps entered {_mapsEntered}, logouts {_logouts}");
    }

    private static string FormatTime(DateTime? utc) => utc?.ToLocalTime().ToString("HH:mm:ss") ?? "-";

    private static string FormatHistoryId(ulong? historyId) => historyId?.ToString() ?? "(not saved)";
}
