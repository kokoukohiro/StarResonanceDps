using System.IO;
using System.Windows;
using StarResonanceDps.PluginSdk;
using StarResonanceDps.Plugins.ModuleTool.Services;
using StarResonanceDps.Plugins.ModuleTool.ViewModels;
using StarResonanceDps.Plugins.ModuleTool.Views;

[assembly: PluginRegistration(
    typeof(StarResonanceDps.Plugins.ModuleTool.ModuleToolPlugin),
    PluginSdkVersion.Current)]
[assembly: PluginDisplayName("ja-JP", "ただのモジュールツール")]
[assembly: PluginGenre("ja-JP", "モジュールツール")]
[assembly: PluginGenre("ko-KR", "모듈 도구")]
[assembly: PluginGenre("zh-CN", "模组工具")]
[assembly: PluginGenre("en-US", "Module Tool")]
[assembly: PluginAuthor("kokoukohiro")]
[assembly: PluginVersion("1.0.0")]
[assembly: PluginIcon("Assets/icon.ico")]

namespace StarResonanceDps.Plugins.ModuleTool;

public sealed class ModuleToolPlugin : IStarResonancePlugin, IPluginWindowProvider
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
        MoveSettingsFromPluginsFolder(_context, ModuleToolViewModel.ConfigFileName);
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

    /// <summary>
    /// 設定ファイルの置き場所は Plugins の直下からプラグインのフォルダへ移った。古い場所にあって今の場所に無ければ移す。
    /// 移せなければ古いファイルを残してログに書き、初期値で動く。
    /// </summary>
    private static void MoveSettingsFromPluginsFolder(IPluginContext context, string fileName)
    {
        var path = context.Settings.GetFilePath(fileName);
        if (File.Exists(path) || Path.GetDirectoryName(context.Settings.SettingsDirectory) is not { } pluginsFolder)
        {
            return;
        }

        var oldPath = Path.Combine(pluginsFolder, fileName);
        if (!File.Exists(oldPath))
        {
            return;
        }

        try
        {
            File.Move(oldPath, path);
            context.Logger.Info($"Moved the settings file from '{oldPath}' to '{path}'.");
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            context.Logger.Error($"Failed to move the settings file from '{oldPath}' to '{path}'; the old file is left in place.", exception);
        }
    }
}
