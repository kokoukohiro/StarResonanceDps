using System.Windows;
using StarResonanceDps.PluginSdk;

[assembly: PluginRegistration(typeof(SampleGameData.SampleGameDataPlugin), PluginSdkVersion.Current)]
[assembly: PluginDisplayName("en-US", "Sample: Game data")]
[assembly: PluginDisplayName("ja-JP", "サンプル: ゲームのデータ")]
[assembly: PluginGenre("en-US", "Sample")]
[assembly: PluginGenre("ja-JP", "サンプル")]
[assembly: PluginAuthor("kokoukohiro")]
[assembly: PluginVersion("1.0.0")]

namespace SampleGameData;

/// <summary>A plugin that shows the game state the app received (<see cref="IPluginGameDataService"/>).</summary>
public sealed class SampleGameDataPlugin : IStarResonancePlugin, IPluginWindowProvider
{
    private IPluginContext? _context;

    public void Initialize(IPluginContext context)
    {
        _context = context;
    }

    public FrameworkElement CreateContent()
    {
        return new GameDataView(_context ?? throw new InvalidOperationException("The plugin has not been initialized."));
    }

    public void Shutdown()
    {
        _context = null;
    }
}
