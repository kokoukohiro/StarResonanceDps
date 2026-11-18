using System.Globalization;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Threading;
using StarResonanceDps.PluginSdk;

namespace SampleEncounterControl;

/// <summary>
/// Splits the current encounter, edits the split rules, and shows the clock of the current encounter and the game's dungeon timer once a second.
/// The app disposes the view after the window closes, because it implements <see cref="IDisposable"/>.
/// </summary>
internal sealed class EncounterControlView : UserControl, IDisposable
{
    private readonly IPluginContext _context;
    private readonly SampleEncounterControlPlugin _plugin;
    private readonly TextBox _idleSeconds = CreateNumberBox();
    private readonly TextBox _intervalSeconds = CreateNumberBox();
    private readonly ToggleButton _splitAtDungeonStart = new() { Margin = new Thickness(0, 0, 8, 8), VerticalAlignment = VerticalAlignment.Center };
    private readonly ComboBox _dungeonClock = new() { Width = 200, Margin = new Thickness(0, 0, 8, 8) };
    private readonly TextBlock _splitResult = CreateText();
    private readonly TextBlock _rules = CreateText();
    private readonly TextBlock _clock = CreateText();
    private readonly DispatcherTimer _timer = new() { Interval = TimeSpan.FromSeconds(1) };

    public EncounterControlView(IPluginContext context, SampleEncounterControlPlugin plugin)
    {
        _context = context;
        _plugin = plugin;

        var settings = plugin.Settings;
        _idleSeconds.Text = settings.IdleSplitSeconds.ToString(CultureInfo.InvariantCulture);
        _intervalSeconds.Text = settings.IntervalSplitSeconds.ToString(CultureInfo.InvariantCulture);
        _splitAtDungeonStart.SetResourceReference(StyleProperty, PluginResourceKeys.Inputs.Switch);
        _splitAtDungeonStart.IsChecked = settings.SplitAtDungeonStart;
        _dungeonClock.SetResourceReference(StyleProperty, PluginResourceKeys.Inputs.SelectBox);
        _dungeonClock.ItemsSource = Enum.GetValues<PluginDungeonClock>();
        _dungeonClock.SelectedItem = settings.DungeonClock;

        var split = new WrapPanel();
        split.Children.Add(CreateButton("Split now", SplitButton_Click));
        split.Children.Add(_splitResult);

        var rules = new WrapPanel { Margin = new Thickness(0, 16, 0, 0) };
        rules.Children.Add(CreateLabel("Idle seconds"));
        rules.Children.Add(_idleSeconds);
        rules.Children.Add(CreateLabel("Interval seconds"));
        rules.Children.Add(_intervalSeconds);
        rules.Children.Add(CreateLabel("Split at dungeon start"));
        rules.Children.Add(_splitAtDungeonStart);
        rules.Children.Add(CreateLabel("Dungeon clock"));
        rules.Children.Add(_dungeonClock);
        rules.Children.Add(CreateButton("Apply rules", ApplyButton_Click));

        _clock.Margin = new Thickness(0, 16, 0, 0);
        var root = new StackPanel { Margin = new Thickness(24, 8, 24, 24) };
        root.Children.Add(split);
        root.Children.Add(rules);
        root.Children.Add(_rules);
        root.Children.Add(_clock);
        Content = root;

        _context.EncounterControl.RulesChanged += EncounterControl_RulesChanged;
        _timer.Tick += Timer_Tick;
        _timer.Start();
        UpdateRules();
        UpdateClock();
    }

    public void Dispose()
    {
        _timer.Stop();
        _timer.Tick -= Timer_Tick;
        _context.EncounterControl.RulesChanged -= EncounterControl_RulesChanged;
    }

    private static TextBox CreateNumberBox()
    {
        var box = new TextBox { Width = 80, Margin = new Thickness(0, 0, 16, 8), VerticalAlignment = VerticalAlignment.Center };
        box.SetResourceReference(StyleProperty, PluginResourceKeys.Inputs.PathBox);
        return box;
    }

    private static TextBlock CreateLabel(string text)
    {
        var label = CreateText();
        label.Text = text;
        label.Margin = new Thickness(0, 0, 8, 8);
        label.VerticalAlignment = VerticalAlignment.Center;
        return label;
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
        var text = new TextBlock { TextWrapping = TextWrapping.Wrap, VerticalAlignment = VerticalAlignment.Center };
        text.SetResourceReference(TextBlock.ForegroundProperty, PluginResourceKeys.Brushes.TextPrimary);
        return text;
    }

    private async void SplitButton_Click(object sender, RoutedEventArgs e)
    {
        var result = await _context.EncounterControl.SplitAsync();
        _splitResult.Text = $"{result.Outcome} (the closed encounter had records: {result.ClosedEncounterHadRecords})";
    }

    private async void ApplyButton_Click(object sender, RoutedEventArgs e)
    {
        if (!int.TryParse(_idleSeconds.Text, NumberStyles.None, CultureInfo.InvariantCulture, out var idle)
            || !int.TryParse(_intervalSeconds.Text, NumberStyles.None, CultureInfo.InvariantCulture, out var interval)
            || idle > 86400
            || interval > 86400)
        {
            _rules.Text = "Enter 0, or seconds from 1 to 86400.";
            return;
        }

        await _plugin.SaveAndRegisterAsync(new EncounterRulesSettings
        {
            IdleSplitSeconds = idle,
            IntervalSplitSeconds = interval,
            SplitAtDungeonStart = _splitAtDungeonStart.IsChecked == true,
            DungeonClock = _dungeonClock.SelectedItem is PluginDungeonClock clock ? clock : PluginDungeonClock.None,
        });
        UpdateRules();
    }

    private void EncounterControl_RulesChanged(object? sender, EventArgs e) => UpdateRules();

    private void Timer_Tick(object? sender, EventArgs e) => UpdateClock();

    private void UpdateRules()
    {
        var control = _context.EncounterControl;
        var applied = control.AppliedOverrides;
        _rules.Text = new StringBuilder()
            .AppendLine($"Registered: {(control.Rules is null ? "none" : "yes")}")
            .AppendLine($"Pending items: {(control.PendingItems.Count == 0 ? "none" : string.Join(", ", control.PendingItems))}")
            .Append($"Applied by the app: start {applied.DungeonStartSplit}, boss room {applied.BossRoomSplit}, wipe {applied.WipeSplit}, clock {applied.DungeonClock}")
            .ToString();
    }

    private void UpdateClock()
    {
        var encounter = _context.GameData.GetEncounter();
        var builder = new StringBuilder()
            .AppendLine($"Clock origin: {encounter.ClockOrigin}")
            .AppendLine($"Clock start (UTC): {encounter.StartUtc?.ToString("HH:mm:ss.fff", CultureInfo.InvariantCulture) ?? "-"}")
            .AppendLine($"First combat event (UTC): {encounter.FirstCombatEventUtc?.ToString("HH:mm:ss.fff", CultureInfo.InvariantCulture) ?? "-"}")
            .AppendLine($"Elapsed: {encounter.Elapsed:hh\\:mm\\:ss}");

        if (_context.GameData.Session.DungeonTimer is { } timer)
        {
            builder.Append($"Dungeon timer: start {timer.StartUtc?.ToString("HH:mm:ss.fff", CultureInfo.InvariantCulture) ?? "-"} UTC, ")
                .Append($"limit {timer.LimitSeconds} s, counts down {timer.CountsDown}, ")
                .Append($"stopped {timer.PausedTotalSeconds} s in total, stopped at {timer.PausedAtSeconds?.ToString(CultureInfo.InvariantCulture) ?? "-"}");
        }
        else
        {
            builder.Append("Dungeon timer: none");
        }

        _clock.Text = builder.ToString();
    }
}
