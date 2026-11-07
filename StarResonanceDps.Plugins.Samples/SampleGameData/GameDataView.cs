using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using StarResonanceDps.PluginSdk;

namespace SampleGameData;

/// <summary>
/// Shows every value of <see cref="IPluginGameDataService"/> as text.
/// Buffs and encounter totals have no change event, so the view reads them again once a second while the window is open.
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
    private int _mapsEntered;
    private int _logouts;
    private string _lastEncounterResult = "(none)";

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
        _data.MapEntered -= Data_MapEntered;
        _data.LoggedOut -= Data_LoggedOut;
    }

    private void Data_SessionChanged(object? sender, EventArgs e) => _sessionChanges++;

    private void Data_PlayersChanged(object? sender, EventArgs e) => _playerChanges++;

    private void Data_EntitiesChanged(object? sender, EventArgs e) => _entityChanges++;

    private void Data_SelfModulesChanged(object? sender, EventArgs e) => _moduleChanges++;

    private void Data_EncounterStarted(object? sender, EventArgs e) => _encountersStarted++;

    private void Data_MapEntered(object? sender, EventArgs e) => _mapsEntered++;

    private void Data_LoggedOut(object? sender, EventArgs e) => _logouts++;

    /// <summary>A closed encounter cannot be read later, so keep what you need from the result here.</summary>
    private void Data_EncounterClosed(object? sender, PluginEncounterResult result)
    {
        _encountersClosed++;
        _lastEncounterResult = $"{result.Totals.Elapsed:mm\\:ss}, {result.Totals.Players.Count} players";
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
        AppendEntities(builder);
        AppendEncounter(builder);
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
            builder.AppendLine($"{name ?? "?"}  HP {entity.CurrentHp}/{entity.MaxHp}  combat {entity.IsInCombat}  hate {entity.HateRanking.Count}  buffs {_data.GetEntityBuffs(entity.EntityUuid).Count}{cast}");
        }

        builder.AppendLine();
    }

    private void AppendEncounter(StringBuilder builder)
    {
        var encounter = _data.GetEncounter();
        builder.AppendLine($"[Encounter: {encounter.Elapsed:mm\\:ss}, history {encounter.IsHistory}, closed {encounter.IsClosed}]");
        foreach (var player in encounter.Players.OrderByDescending(player => player.Damage.Total).Take(MaxListedRows))
        {
            var topSkill = _data.GetSkillTotals(player.CharacterId, PluginCombatKind.Damage).FirstOrDefault();
            builder.AppendLine($"{player.CharacterId}  damage {player.Damage.Total} ({player.Damage.PerSecond:0}/s)  healing {player.Healing.Total}  taken {player.DamageTaken.Total}  top skill {topSkill?.Name}");
        }

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
        builder.AppendLine($"Maps entered {_mapsEntered}, logouts {_logouts}");
    }
}
