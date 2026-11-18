using System.Text;
using System.Windows;
using System.Windows.Controls;
using StarResonanceDps.PluginSdk;

namespace SampleHistory;

/// <summary>
/// Lists the app's saved encounters, reads one without opening it, and opens one in the app.
/// The view keeps no encounter object: it reads what it shows and lets the object go, because a kept object keeps its encounter in memory.
/// The app disposes the view after the window closes, because it implements <see cref="IDisposable"/>.
/// </summary>
internal sealed class HistoryView : UserControl, IDisposable
{
    private const int MaxListedRows = 20;

    private readonly IPluginContext _context;
    private readonly IPluginHistoryService _history;
    private readonly IDisposable _keepHistoryOpen;
    private readonly ComboBox _entries = new() { Width = 360, Margin = new Thickness(0, 0, 8, 8) };
    private readonly TextBlock _status = CreateText();
    private readonly TextBlock _operation = CreateText();
    private readonly TextBlock _details = CreateText();

    public HistoryView(IPluginContext context)
    {
        _context = context;
        _history = context.History;

        // The history encounter opened from this window stays shown while the window is open,
        // even with the app's setting that returns to the live encounter at the next live event.
        _keepHistoryOpen = _history.KeepHistoryOpen();

        _entries.SetResourceReference(StyleProperty, PluginResourceKeys.Inputs.SelectBox);
        var commands = new WrapPanel();
        commands.Children.Add(_entries);
        commands.Children.Add(CreateButton("Refresh list", RefreshButton_Click));
        commands.Children.Add(CreateButton("Read", ReadButton_Click));
        commands.Children.Add(CreateButton("Open in the app", OpenButton_Click));
        commands.Children.Add(CreateButton("Return to live", ReturnButton_Click));

        _operation.Margin = new Thickness(0, 8, 0, 0);
        var scrollViewer = new ScrollViewer
        {
            Content = _details,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            Margin = new Thickness(0, 16, 0, 0)
        };
        scrollViewer.SetResourceReference(StyleProperty, PluginResourceKeys.Scrolling.ScrollViewer);

        var root = new DockPanel { Margin = new Thickness(24, 8, 24, 24) };
        foreach (var top in new FrameworkElement[] { commands, _status, _operation })
        {
            DockPanel.SetDock(top, Dock.Top);
            root.Children.Add(top);
        }

        root.Children.Add(scrollViewer);
        Content = root;

        _history.OpenFailedChanged += History_OpenFailedChanged;
        _context.GameData.ShownEncounterChanged += GameData_ShownEncounterChanged;
        UpdateStatus();
    }

    public void Dispose()
    {
        _keepHistoryOpen.Dispose();
        _history.OpenFailedChanged -= History_OpenFailedChanged;
        _context.GameData.ShownEncounterChanged -= GameData_ShownEncounterChanged;
    }

    private static Button CreateButton(string text, RoutedEventHandler click)
    {
        var button = new Button { Content = text, Width = 140, Margin = new Thickness(0, 0, 8, 8), VerticalAlignment = VerticalAlignment.Center };
        button.SetResourceReference(StyleProperty, PluginResourceKeys.Buttons.Text);
        button.Click += click;
        return button;
    }

    private static TextBlock CreateText()
    {
        var text = new TextBlock { TextWrapping = TextWrapping.Wrap };
        text.SetResourceReference(TextBlock.ForegroundProperty, PluginResourceKeys.Brushes.TextPrimary);
        return text;
    }

    private void History_OpenFailedChanged(object? sender, EventArgs e) => UpdateStatus();

    private void GameData_ShownEncounterChanged(object? sender, EventArgs e) => UpdateStatus();

    private void UpdateStatus()
    {
        var shown = _context.GameData.GetShownEncounter();
        var shownText = shown is null
            ? "no encounter"
            : $"{(shown.IsHistory ? "an encounter from the history" : "an encounter of this session")}, history ID {shown.HistoryId?.ToString() ?? "(not saved)"}";
        var failure = _history.HasOpenFailed ? Environment.NewLine + "The app could not read the encounter it was last asked to open." : string.Empty;
        _status.Text = $"The app shows {shownText}.{failure}";
    }

    private async void RefreshButton_Click(object sender, RoutedEventArgs e)
    {
        var result = await _history.GetEntriesAsync();
        if (result.Status != PluginHistoryStatus.Succeeded)
        {
            _operation.Text = $"The history could not be listed: {result.Status}";
            return;
        }

        _entries.ItemsSource = result.Entries.Select(entry => new EntryItem(entry, FormatEntry(entry))).ToArray();
        _entries.SelectedIndex = result.Entries.Count > 0 ? 0 : -1;
        _operation.Text = $"{result.Entries.Count} saved encounters.";
    }

    private async void ReadButton_Click(object sender, RoutedEventArgs e)
    {
        if (_entries.SelectedItem is not EntryItem item)
        {
            return;
        }

        var result = await _history.LoadAsync(item.Entry.HistoryId);
        if (result.Encounter is not { } encounter)
        {
            _operation.Text = $"History ID {item.Entry.HistoryId} could not be read: {result.Status}";
            return;
        }

        _operation.Text = $"Read history ID {item.Entry.HistoryId} without opening it.";
        _details.Text = Describe(encounter);
    }

    private async void OpenButton_Click(object sender, RoutedEventArgs e)
    {
        if (_entries.SelectedItem is not EntryItem item)
        {
            return;
        }

        var result = await _history.OpenAsync(item.Entry.HistoryId);
        _operation.Text = $"Opening history ID {item.Entry.HistoryId}: {result}";
    }

    private async void ReturnButton_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            await _history.ReturnToLiveAsync();
            _operation.Text = "Returned to the live encounter.";
        }
        catch (Exception exception)
        {
            // The app also writes the exception to its log.
            _operation.Text = $"Could not return to the live encounter: {exception.Message}";
        }
    }

    private string Describe(IPluginEncounterRecord encounter)
    {
        var names = _context.Names;
        var totals = encounter.GetTotals();
        IReadOnlyList<PluginPlayer> players = encounter.GetPlayers() ?? [];
        var builder = new StringBuilder();

        builder.AppendLine($"[History ID {encounter.HistoryId}: {GetSceneName(encounter.Info)}, {totals.Elapsed:mm\\:ss}]");
        var top = totals.Players.MaxBy(player => player.Damage.Total);
        if (top is not null)
        {
            var topName = players.FirstOrDefault(player => player.CharacterId == top.CharacterId)?.Name;
            builder.AppendLine($"Most damage: {topName ?? top.CharacterId.ToString()}  {top.Damage.Total} ({top.Damage.PerSecond:0}/s)");
        }

        builder.AppendLine();

        builder.AppendLine($"[Players: {players.Count}]");
        foreach (var player in players.Take(MaxListedRows))
        {
            var spec = player.SpecId is { } specId ? names.GetClassSpecName(specId) : player.SpecState.ToString();
            var self = player.IsSelf ? "  (you)" : string.Empty;

            // Factors are saved only for the character that was logged in when the encounter was saved.
            var build = encounter.GetPlayerBuild(player.CharacterId);
            var factors = build?.Factors is { } saved ? $"  factors {saved.Count}" : string.Empty;
            builder.AppendLine($"{player.Name}  {spec}  score {player.AbilityScore}  skills {build?.SkillLevels?.Count.ToString() ?? "-"}{factors}{self}");
        }

        builder.AppendLine();

        var monsters = encounter.GetMonsters();
        builder.AppendLine($"[Monsters: {monsters.Count}]");
        foreach (var monster in monsters.Take(MaxListedRows))
        {
            var monsterName = monster.MonsterId is { } monsterId ? names.GetMonsterName(monsterId) : null;
            builder.AppendLine($"{monsterName ?? "?"}  HP {monster.Hp}/{monster.MaxHp}  casts {monster.Casts.Count}");
        }

        builder.AppendLine();

        AppendSelfBuffs(builder, encounter, players);

        builder.AppendLine("[This plugin's data]");
        builder.AppendLine($"Window opened during the encounter: {SampleHistoryPlugin.ReadCount(encounter.GetPluginData(SampleHistoryPlugin.OpenedCountKey))} times");
        builder.AppendLine($"Saved at: {SampleHistoryPlugin.ReadText(encounter.GetPluginData(SampleHistoryPlugin.SavedAtKey)) ?? "(none)"}");
        return builder.ToString();
    }

    private void AppendSelfBuffs(StringBuilder builder, IPluginEncounterRecord encounter, IReadOnlyList<PluginPlayer> players)
    {
        builder.AppendLine("[Your buffs]");
        var self = players.FirstOrDefault(player => player.IsSelf);
        var intervals = self is null ? null : encounter.GetPlayerBuffIntervals(self.CharacterId);
        if (intervals is null)
        {
            builder.AppendLine(self is null ? "The logged-in character is not in this encounter." : "No records.");
            builder.AppendLine();
            return;
        }

        var aroundSeconds = intervals.PresentIntervals.Sum(interval => (interval.EndUtc - interval.StartUtc).TotalSeconds);
        builder.AppendLine($"{intervals.Buffs.Count} buffs, around for {aroundSeconds:0}s");
        var buffs = intervals.Buffs
            .Select(buff => (buff.BuffId, Seconds: buff.Intervals.Sum(interval => (interval.EndUtc - interval.StartUtc).TotalSeconds)))
            .OrderByDescending(buff => buff.Seconds)
            .Take(MaxListedRows);
        foreach (var (buffId, seconds) in buffs)
        {
            builder.AppendLine($"  {_context.Names.GetBuffName(buffId) ?? buffId.ToString()}  {seconds:0}s");
        }

        builder.AppendLine();
    }

    private string FormatEntry(PluginHistoryEntry entry)
    {
        return $"{entry.HistoryId}  {entry.Info.CreatedUtc.ToLocalTime():g}  {GetSceneName(entry.Info)}  {entry.Elapsed:mm\\:ss}";
    }

    private string GetSceneName(PluginEncounterInfo info)
    {
        return info.SceneId is { } sceneId ? _context.Names.GetSceneName(sceneId, info.DungeonDifficulty) ?? sceneId.ToString() : "?";
    }

    /// <summary>An item of the list, which shows <see cref="ToString"/>.</summary>
    private sealed record EntryItem(PluginHistoryEntry Entry, string Text)
    {
        public override string ToString() => Text;
    }
}
