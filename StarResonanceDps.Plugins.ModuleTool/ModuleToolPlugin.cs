using System.Windows;
using StarResonanceDps.PluginSdk;
using StarResonanceDps.Plugins.ModuleTool.Services;
using StarResonanceDps.Plugins.ModuleTool.ViewModels;
using StarResonanceDps.Plugins.ModuleTool.Views;

[assembly: PluginRegistration(
    typeof(StarResonanceDps.Plugins.ModuleTool.ModuleToolPlugin),
    "kokoukohiro.module-tool",
    PluginSdkVersion.Current)]
[assembly: PluginDisplayName("ja-JP", "モジュールツール")]
[assembly: PluginDisplayName("ko-KR", "모듈 도구")]
[assembly: PluginDisplayName("zh-CN", "模组工具")]
[assembly: PluginDisplayName("en-US", "Module Tool")]

namespace StarResonanceDps.Plugins.ModuleTool;

public sealed class ModuleToolPlugin : IStarResonancePlugin, IPluginWindowOptionsProvider
{
    private static readonly PluginWindowOptions WindowOptions = new(
        width: 877,
        height: 640,
        minWidth: 877,
        minHeight: 480);

    private IPluginContext? _context;
    private ModuleCatalog? _catalog;
    private ModuleInventoryHolder? _inventory;

    public PluginWindowOptions GetWindowOptions()
    {
        return WindowOptions;
    }

    public void Initialize(IPluginContext context)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
        _catalog = new ModuleCatalog();
        _inventory = new ModuleInventoryHolder(_context.GameData);
        _context.Logger.Info("Initialized.");
    }

    public FrameworkElement CreateContent()
    {
        if (_context is null || _catalog is null || _inventory is null)
        {
            throw new InvalidOperationException("The plugin has not been initialized.");
        }

        return new ModuleToolView(new ModuleToolViewModel(_context, _catalog, _inventory));
    }

    public void Shutdown()
    {
        _context?.Logger.Info("Shutdown.");
        _inventory?.Dispose();
        _inventory = null;
        _context = null;
        _catalog = null;
    }
}
