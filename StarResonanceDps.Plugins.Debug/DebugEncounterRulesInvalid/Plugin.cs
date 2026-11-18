// This file is part of StarResonanceDps. See LICENSING.md for the license, additional permissions and additional terms.

using StarResonanceDps.PluginSdk;

[assembly: PluginRegistration(typeof(DebugEncounterRulesInvalid.Plugin), PluginSdkVersion.Current)]
[assembly: PluginDisplayName("ja-JP", "デバッグ: ダンジョンの時計と合わない規則")]
[assembly: PluginAuthor("kokoukohiro")]
[assembly: PluginVersion("1.0.0")]

namespace DebugEncounterRulesInvalid;

/// <summary>
/// 初期化で、ダンジョンの時計とダンジョンの開始で区切らない組を登録する。
/// <see cref="IPluginEncounterControlService.SetRulesAsync"/> が呼んだその場で <see cref="ArgumentException"/> を投げ、プラグインが止まる。
/// </summary>
public sealed class Plugin : IStarResonancePlugin
{
    public void Initialize(IPluginContext context)
    {
        _ = context.EncounterControl.SetRulesAsync(new PluginEncounterRules
        {
            DungeonClock = PluginDungeonClock.DungeonStartArrival,
            DungeonStartSplit = PluginAutoSplitMode.DoNotSplit,
        });
    }

    public void Shutdown()
    {
    }
}
