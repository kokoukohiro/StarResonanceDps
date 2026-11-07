using System.Windows;
using StarResonanceDps.PluginSdk;

[assembly: PluginRegistration(typeof(DebugThrowOpen.Plugin), "kokoukohiro.debug-throw-open", PluginSdkVersion.Current)]
[assembly: PluginDisplayName("ja-JP", "デバッグ: 窓を開くときに例外")]
[assembly: PluginGenre("ja-JP", "デバッグ")]
[assembly: PluginAuthor("kokoukohiro")]
[assembly: PluginVersion("1.0.0")]

namespace DebugThrowOpen;

public sealed class Plugin : IStarResonancePlugin, IPluginWindowProvider
{
    public void Initialize(IPluginContext context)
    {
    }

    public FrameworkElement CreateContent()
    {
        throw new InvalidOperationException("Debug exception from CreateContent.");
    }

    public void Shutdown()
    {
    }
}
