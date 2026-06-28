using System.Windows;
using StarResonanceDps.PluginSdk;
using StarResonanceDps.Plugins.KeybindTool.ViewModels;
using StarResonanceDps.Plugins.KeybindTool.Views;

[assembly: PluginRegistration(
    typeof(StarResonanceDps.Plugins.KeybindTool.KeybindToolPlugin),
    "kokoukohiro.keybind-tool",
    PluginSdkVersion.Current)]
[assembly: PluginDisplayName("ja-JP", "キーバインドツール")]
[assembly: PluginDisplayName("ko-KR", "키 바인드 도구")]
[assembly: PluginDisplayName("zh-CN", "按键绑定工具")]
[assembly: PluginDisplayName("en-US", "Keybind Tool")]

namespace StarResonanceDps.Plugins.KeybindTool;

public sealed class KeybindToolPlugin : IStarResonancePlugin, IPluginWindowOptionsProvider
{
    private static readonly PluginWindowOptions WindowOptions = new(
        width: 582,
        height: 760,
        minWidth: 500,
        minHeight: 45);

    private IPluginContext? _context;

    public PluginWindowOptions GetWindowOptions()
    {
        return WindowOptions;
    }

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

        return new KeybindToolView(new KeybindToolViewModel(_context));
    }

    public void Shutdown()
    {
        _context?.Logger.Info("Shutdown.");
        _context = null;
    }
}
