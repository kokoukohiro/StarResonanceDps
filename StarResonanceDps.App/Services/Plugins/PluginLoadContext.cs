using System.IO;
using System.Reflection;
using System.Runtime.Loader;
using StarResonanceDps.PluginSdk;

namespace StarResonanceDps.App.Services;

internal sealed class PluginLoadContext : AssemblyLoadContext
{
    private readonly AssemblyDependencyResolver _dependencyResolver;
    private readonly string _pluginSdkAssemblyName;

    public PluginLoadContext(string mainAssemblyPath)
        : base($"Plugin:{Path.GetFileNameWithoutExtension(mainAssemblyPath)}", isCollectible: false)
    {
        _dependencyResolver = new AssemblyDependencyResolver(mainAssemblyPath);
        _pluginSdkAssemblyName = typeof(IStarResonancePlugin).Assembly.GetName().Name
            ?? throw new InvalidOperationException("The plugin SDK assembly name is unavailable.");
    }

    protected override Assembly? Load(AssemblyName assemblyName)
    {
        // SDK types must be shared with the host. Loading a copied SDK DLL would make
        // IStarResonancePlugin type checks fail even when names appear identical.
        if (string.Equals(assemblyName.Name, _pluginSdkAssemblyName, StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        var assemblyPath = _dependencyResolver.ResolveAssemblyToPath(assemblyName);
        return string.IsNullOrWhiteSpace(assemblyPath)
            ? null
            : LoadFromAssemblyPath(assemblyPath);
    }

    protected override IntPtr LoadUnmanagedDll(string unmanagedDllName)
    {
        var libraryPath = _dependencyResolver.ResolveUnmanagedDllToPath(unmanagedDllName);
        return string.IsNullOrWhiteSpace(libraryPath)
            ? IntPtr.Zero
            : LoadUnmanagedDllFromPath(libraryPath);
    }
}
