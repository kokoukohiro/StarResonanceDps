using System.Windows;
using System.Windows.Controls;
using StarResonanceDps.PluginSdk;

namespace StarResonanceDps.Plugins.ModuleTool;

public sealed class ModuleToolPlugin : IStarResonancePlugin
{
    private IPluginContext? _context;

    public PluginDescriptor Descriptor { get; } = new("kokoukohiro.module-tool", PluginSdkVersion.Current);

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
