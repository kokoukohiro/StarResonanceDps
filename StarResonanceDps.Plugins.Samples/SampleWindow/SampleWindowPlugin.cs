using System.Windows;
using StarResonanceDps.PluginSdk;

// The app reads these attributes to list the plugin. A build error tells you when a required one is missing.
[assembly: PluginRegistration(typeof(SampleWindow.SampleWindowPlugin), PluginSdkVersion.Current)]
[assembly: PluginDisplayName("en-US", "Sample: Window")]
[assembly: PluginDisplayName("ja-JP", "サンプル: 窓")]
[assembly: PluginGenre("en-US", "Sample")]
[assembly: PluginGenre("ja-JP", "サンプル")]
[assembly: PluginAuthor("kokoukohiro")]
[assembly: PluginVersion("1.0.0")]
[assembly: PluginIcon("Assets/icon.ico")]

namespace SampleWindow;

/// <summary>
/// A plugin that opens a window from the plugin list.
/// The window lists the app's look (<see cref="PluginResourceKeys"/>) that a plugin can use.
/// </summary>
public sealed class SampleWindowPlugin : IStarResonancePlugin, IPluginWindowProvider
{
    public void Initialize(IPluginContext context)
    {
    }

    public FrameworkElement CreateContent()
    {
        return new ResourceCatalogView();
    }

    public PluginWindowOptions GetWindowOptions()
    {
        return new PluginWindowOptions(width: 720, height: 640, minWidth: 480, minHeight: 320);
    }

    public void Shutdown()
    {
    }
}
