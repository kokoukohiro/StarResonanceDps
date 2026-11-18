using System.Windows;
using StarResonanceDps.PluginSdk;

[assembly: PluginRegistration(typeof(SampleEncounterControl.SampleEncounterControlPlugin), PluginSdkVersion.Current)]
[assembly: PluginDisplayName("en-US", "Sample: Encounter Control")]
[assembly: PluginDisplayName("ja-JP", "サンプル: 戦闘の区切り")]
[assembly: PluginGenre("en-US", "Sample")]
[assembly: PluginGenre("ja-JP", "サンプル")]
[assembly: PluginAuthor("kokoukohiro")]
[assembly: PluginVersion("1.0.0")]

namespace SampleEncounterControl;

/// <summary>
/// A plugin that changes how the app splits encounters (<see cref="IPluginEncounterControlService"/>):
/// a button that splits the current encounter, and split rules and a dungeon clock kept in the plugin's own settings file.
/// The app does not save the rules, so the plugin registers them again every time it starts.
/// </summary>
public sealed class SampleEncounterControlPlugin : IStarResonancePlugin, IPluginWindowProvider
{
    private const string SettingsFileName = "encounter_rules.json";

    private IPluginContext? _context;

    internal EncounterRulesSettings Settings { get; private set; } = new();

    public void Initialize(IPluginContext context)
    {
        _context = context;
        Settings = context.Settings.Load<EncounterRulesSettings>(SettingsFileName);
        _ = RegisterRulesAsync();
    }

    public FrameworkElement CreateContent()
    {
        var context = _context ?? throw new InvalidOperationException("The plugin has not been initialized.");
        return new EncounterControlView(context, this);
    }

    public void Shutdown()
    {
        // The app removes the rules of a stopped plugin, so there is nothing to undo here.
        _context = null;
    }

    /// <summary>Saves the settings and registers the rules made from them.</summary>
    internal Task<PluginEncounterRulesResult?> SaveAndRegisterAsync(EncounterRulesSettings settings)
    {
        Settings = settings;
        _context?.Settings.Save(SettingsFileName, settings);
        return RegisterRulesAsync();
    }

    /// <summary>
    /// Registers the rules. <see cref="IPluginEncounterControlService.SetRulesAsync"/> throws when a value is out of range,
    /// which can happen with an edited settings file, so the failure is logged instead of being lost in an unobserved task.
    /// </summary>
    private async Task<PluginEncounterRulesResult?> RegisterRulesAsync()
    {
        var context = _context;
        if (context is null)
        {
            return null;
        }

        try
        {
            var result = await context.EncounterControl.SetRulesAsync(Settings.ToRules());
            context.Logger.Info($"Rules registered: {result.Status}, pending: {string.Join(", ", result.PendingItems)}");
            return result;
        }
        catch (ArgumentException ex)
        {
            context.Logger.Error("The rules in the settings file are invalid.", ex);
            return null;
        }
    }
}

/// <summary>The plugin's settings file. The default values register no rules.</summary>
public sealed class EncounterRulesSettings
{
    public int IdleSplitSeconds { get; set; }

    public int IntervalSplitSeconds { get; set; }

    public bool SplitAtDungeonStart { get; set; }

    public PluginDungeonClock DungeonClock { get; set; }

    internal PluginEncounterRules? ToRules()
    {
        var rules = new PluginEncounterRules
        {
            IdleSplitSeconds = IdleSplitSeconds,
            IntervalSplitSeconds = IntervalSplitSeconds,
            DungeonStartSplit = SplitAtDungeonStart ? PluginAutoSplitMode.Split : PluginAutoSplitMode.HostSetting,
            DungeonClock = DungeonClock,
        };

        // null removes the rules; registering the default values would do the same, but keeps an empty registration.
        return rules == new PluginEncounterRules() ? null : rules;
    }
}
