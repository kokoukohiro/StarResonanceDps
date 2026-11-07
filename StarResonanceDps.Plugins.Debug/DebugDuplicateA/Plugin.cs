using System.Windows;
using StarResonanceDps.Plugins.Debug;
using StarResonanceDps.PluginSdk;

[assembly: PluginRegistration(typeof(DebugDuplicateA.Plugin), "kokoukohiro.debug-duplicate", PluginSdkVersion.Current)]
[assembly: PluginDisplayName("ja-JP", "デバッグ: 同じID(A)")]
[assembly: PluginGenre("ja-JP", "デバッグ")]
[assembly: PluginAuthor("kokoukohiro")]
[assembly: PluginVersion("1.0.0")]

namespace DebugDuplicateA;

public sealed class Plugin : IStarResonancePlugin, IPluginWindowProvider
{
    public void Initialize(IPluginContext context)
    {
    }

    public FrameworkElement CreateContent() => DebugParts.CreateWindowContent("同じIDのプラグインのうち、先に読まれた方です。");

    public void Shutdown()
    {
    }
}
