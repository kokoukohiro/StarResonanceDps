using System.Windows;
using System.Windows.Controls;
using StarResonanceDps.PluginSdk;

[assembly: PluginRegistration(
    typeof(StarResonanceDps.Plugins.ModuleTool.ModuleToolPlugin),
    "kokoukohiro.module-tool",
    PluginSdkVersion.Current)]
[assembly: PluginDisplayName("ja-JP", "モジュールツール")]
[assembly: PluginDisplayName("ko-KR", "모듈 도구")]
[assembly: PluginDisplayName("zh-CN", "模块工具")]
[assembly: PluginDisplayName("en-US", "Module Tool")]

namespace StarResonanceDps.Plugins.ModuleTool;

public sealed class ModuleToolPlugin : IStarResonancePlugin
{
    private IPluginContext? _context;

    public void Initialize(IPluginContext context)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
        _context.Logger.Info("Initialized.");
    }

    public FrameworkElement CreateContent()
    {
        if (_context is null)
        {
            throw new InvalidOperationException("The plugin has not been initialized.");
        }

        // The shared PluginWindow owns all chrome. Feature-specific UI will be
        // developed inside this DLL in later work.
        return new Grid();
    }

    public void Shutdown()
    {
        _context?.Logger.Info("Shutdown.");
        _context = null;
    }
}
