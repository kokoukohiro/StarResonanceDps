using System.IO;
using System.Reflection;
using System.Runtime.Loader;

namespace StarResonanceDps.App.Services;

/// <summary>
/// プラグイン1つの読み込み先。部品はプラグインのフォルダから探し(<c>.deps.json</c> があればそれに従う)、
/// SDK だけは本体のものを使う(本体と同じ型にするため)。見つからない部品は既定の読み込み先(本体・.NET・WPF)に任せる。
/// </summary>
internal sealed class PluginLoadContext : AssemblyLoadContext
{
    public const string SdkAssemblyName = "StarResonanceDps.PluginSdk";

    private readonly AssemblyDependencyResolver _resolver;
    private readonly bool _readsFromMemory;

    /// <param name="readsFromMemory">
    /// 真なら捨てられる読み込み先にし、ファイルを中身から読む(ファイルを掴まない)。「＋」で写す前に確かめるときに使う。
    /// </param>
    public PluginLoadContext(string mainAssemblyPath, bool readsFromMemory)
        : base(Path.GetFileNameWithoutExtension(mainAssemblyPath), isCollectible: readsFromMemory)
    {
        _resolver = new AssemblyDependencyResolver(mainAssemblyPath);
        _readsFromMemory = readsFromMemory;
    }

    public Assembly LoadPluginAssembly(string path)
    {
        if (!_readsFromMemory)
        {
            return LoadFromAssemblyPath(path);
        }

        using var stream = new MemoryStream(File.ReadAllBytes(path));
        return LoadFromStream(stream);
    }

    protected override Assembly? Load(AssemblyName assemblyName)
    {
        if (string.Equals(assemblyName.Name, SdkAssemblyName, StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        var path = _resolver.ResolveAssemblyToPath(assemblyName);
        return path is null ? null : LoadPluginAssembly(path);
    }

    protected override IntPtr LoadUnmanagedDll(string unmanagedDllName)
    {
        var path = _resolver.ResolveUnmanagedDllToPath(unmanagedDllName);
        return path is null ? IntPtr.Zero : LoadUnmanagedDllFromPath(path);
    }
}
