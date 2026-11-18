// This file is part of StarResonanceDps. See LICENSING.md for the license, additional permissions and additional terms.

using StarResonanceDps.PluginSdk;

[assembly: PluginRegistration(typeof(DebugEncounterRulesConflict.Plugin), PluginSdkVersion.Current)]
[assembly: PluginDisplayName("ja-JP", "デバッグ: 区切りの規則の重なり")]
[assembly: PluginAuthor("kokoukohiro")]
[assembly: PluginVersion("1.0.0")]

namespace DebugEncounterRulesConflict;

/// <summary>
/// ボス部屋で区切る規則を登録する。サンプル「戦闘の区切り」がダンジョンの時計を持っていると、この項目は保留(Pending)になり、
/// サンプルを止めると当たって <see cref="IPluginEncounterControlService.RulesChanged"/> が来る。結果はログに書く。
/// </summary>
public sealed class Plugin : IStarResonancePlugin
{
    private IPluginContext? _context;

    public void Initialize(IPluginContext context)
    {
        _context = context;
        context.EncounterControl.RulesChanged += EncounterControl_RulesChanged;
        _ = RegisterAsync(context);
    }

    public void Shutdown()
    {
        if (_context is { } context)
        {
            context.EncounterControl.RulesChanged -= EncounterControl_RulesChanged;
        }

        _context = null;
    }

    private static async Task RegisterAsync(IPluginContext context)
    {
        var result = await context.EncounterControl.SetRulesAsync(new PluginEncounterRules { BossRoomSplit = PluginAutoSplitMode.Split });
        context.Logger.Info($"SetRulesAsync: {result.Status}, pending: {string.Join(", ", result.PendingItems)}");
    }

    private void EncounterControl_RulesChanged(object? sender, EventArgs e)
    {
        if (_context is not { } context)
        {
            return;
        }

        var applied = context.EncounterControl.AppliedOverrides;
        context.Logger.Info(
            $"RulesChanged: pending {string.Join(", ", context.EncounterControl.PendingItems)}, " +
            $"applied start {applied.DungeonStartSplit}, boss room {applied.BossRoomSplit}, wipe {applied.WipeSplit}, clock {applied.DungeonClock}");
    }
}
