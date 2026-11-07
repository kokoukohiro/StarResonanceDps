using System.Windows;
using StarResonanceDps.Plugins.Debug;
using StarResonanceDps.PluginSdk;

[assembly: PluginRegistration(typeof(DebugDuplicateB.Plugin), "kokoukohiro.debug-duplicate", PluginSdkVersion.Current)]
[assembly: PluginDisplayName("ja-JP", "デバッグ: 同じID(B)")]
[assembly: PluginGenre("ja-JP", "デバッグ")]
[assembly: PluginAuthor("kokoukohiro")]
[assembly: PluginVersion("1.0.0")]

namespace DebugDuplicateB;

public sealed class Plugin : IStarResonancePlugin, IPluginWindowProvider
{
    public void Initialize(IPluginContext context)
    {
    }

    public FrameworkElement CreateContent() => DebugParts.CreateWindowContent("ここは開けないはずです。");

    public void Shutdown()
    {
    }
}
