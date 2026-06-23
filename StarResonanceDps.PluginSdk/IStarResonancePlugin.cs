using System.Windows;

namespace StarResonanceDps.PluginSdk;

public interface IStarResonancePlugin
{
    PluginDescriptor Descriptor { get; }

    void Initialize(IPluginContext context);

    FrameworkElement CreateContent();

    void Shutdown();
}
