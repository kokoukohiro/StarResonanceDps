using System.IO;
using System.Windows;
using StarResonanceDps.PluginSdk;
using StarResonanceDps.Plugins.KeybindTool.Services;
using StarResonanceDps.Plugins.KeybindTool.ViewModels;
using StarResonanceDps.Plugins.KeybindTool.Views;

[assembly: PluginRegistration(
    typeof(StarResonanceDps.Plugins.KeybindTool.KeybindToolPlugin),
    "kokoukohiro.keybind-tool",
    PluginSdkVersion.Current)]
[assembly: PluginDisplayName("ja-JP", "BPSR：パッド勢を救いたい")]
[assembly: PluginGenre("ja-JP", "キーバインドツール")]
[assembly: PluginGenre("ko-KR", "키 바인드 도구")]
[assembly: PluginGenre("zh-CN", "按键绑定工具")]
[assembly: PluginGenre("en-US", "Keybind Tool")]
[assembly: PluginAuthor("kokoukohiro")]
[assembly: PluginVersion("2.0.0")]
[assembly: PluginIcon("Assets/icon.ico")]

namespace StarResonanceDps.Plugins.KeybindTool;

public sealed class KeybindToolPlugin : IStarResonancePlugin, IPluginWindowProvider
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
        MoveSettingsFromPluginsFolder(_context, KeybindCatalog.ButtonLayoutFileName);
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
