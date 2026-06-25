using System.Windows;

namespace StarResonanceDps.PluginSdk;

public interface IStarResonancePlugin
{
    void Initialize(IPluginContext context);

    FrameworkElement CreateContent();

    void Shutdown();
}
