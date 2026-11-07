using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.Loader;
using System.Threading;
using System.Windows;
using Microsoft.Win32;
using Serilog;
using StarResonanceDps.App.Config;
using StarResonanceDps.App.Localization;
using StarResonanceDps.App.ViewModels;
using StarResonanceDps.App.Views;
using StarResonanceDps.PluginSdk;

namespace StarResonanceDps.App.Services;

/// <summary>
/// プラグインの読み込み・初期化・止める・終える。
///
/// <para>
/// プラグインは <c>Plugins\&lt;アセンブリ名&gt;\&lt;アセンブリ名&gt;.dll</c> に1つずつ置き、アセンブリ名で見分け、プラグインごとの読み込み先(<see cref="PluginLoadContext"/>)に読む。
/// 一覧はフォルダから読み(<see cref="DiscoverPlugins"/>)、初期化は起動の処理(<see cref="InitializeAll"/>、メイン窓が描かれた後)でまとめて行う。
/// ウィジェットと通知の項目を足すプラグインは、初期化の後に定義を確かめてメイン画面と通知タブへ渡す(<see cref="PluginWidgetRegistry"/>・<see cref="PluginNotificationRegistry"/>)。
/// 決まりに合わなければそのプラグインを止める。
/// </para>
///
/// <para>
/// プラグインのコードは全部 <see cref="PluginCallGuard"/> を通して呼ぶ。例外を出したプラグインはその起動の間だけ止め(読み込んだものは外せない)、次の起動でまた読む。
/// アプリを終わらせた例外(プラグインが作ったスレッドの例外)を出したプラグインは、フォルダに印を書き、印がある間は初期化しない。
/// </para>
///
/// <para>
/// アンインストール(<see cref="Uninstall"/>)は、読み込んだ DLL を動いている間は消せないので、フォルダに印を書いてプラグインを止め、
/// 次の起動で読み込む前にフォルダを消す(<see cref="DiscoverPlugins"/>)。
/// </para>
/// </summary>
public sealed class PluginManager
{
    /// <summary>このプラグインの例外でアプリが終わった印。プラグインのフォルダに置く。</summary>
    private const string TerminatedAppMarkerFileName = "app_terminated.marker";

    /// <summary>アンインストールした印。プラグインのフォルダに置き、次の起動でフォルダごと消す。</summary>
    private const string UninstallMarkerFileName = "uninstall.marker";

    // 前に使ったフォルダと窓の大きさの記録を、ほかのファイル・フォルダの窓と分ける番号。
    private static readonly Guid AddPluginDialogClientGuid = new("1F44B92B-21EC-41A9-831C-1103136B4600");

    private readonly string _pluginsDirectory;
    private readonly Dictionary<string, PluginEntry> _entries = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>足した順(一覧の並び)のプラグイン。例外の持ち主を探す処理(ほかのスレッドから来る)も読むので、足すたびに作り直す。</summary>
    private PluginEntry[] _entrySnapshot = [];

    private bool _hasDiscoveredPlugins;

    private PluginManager()
    {
        _pluginsDirectory = AppDataPaths.PluginsDirectory;
    }

    public static PluginManager Instance { get; } = new();

    /// <summary>
    /// プラグインの一覧の「＋」(<see cref="AddFromFile"/>)からプラグインを足した。起動時の一覧(<see cref="DiscoverPlugins"/>)では出さない。
    /// </summary>
    public event EventHandler<PluginInfo>? PluginRegistered;

    /// <summary>
    /// 一覧の行の状態(<see cref="GetStatus"/>)が変わった。値はプラグインの鍵(<see cref="PluginInfo.Key"/>)。
    /// 初期化の結果・止めた・アンインストールしたときに、画面のスレッドで出す。
    /// </summary>
    public event EventHandler<string>? PluginStatusChanged;

    /// <summary>
    /// Plugins のフォルダを読み、一覧に出すプラグインを返す(入れた順 = フォルダの作成時刻の古い順、同じ時刻は名前の順)。初期化はしない。
    /// アンインストールの印のあるフォルダは、読む前に消す。消せなければ読まずに、削除できなかった行にする(次の起動でまた消す)。
    /// </summary>
    public IReadOnlyList<PluginInfo> DiscoverPlugins()
    {
        if (_hasDiscoveredPlugins)
        {
            return GetDiscoveredPlugins();
        }

        _hasDiscoveredPlugins = true;

        if (!Directory.Exists(_pluginsDirectory))
        {
            return [];
        }

        foreach (var misplacedAssembly in Directory.EnumerateFiles(_pluginsDirectory, "*.dll", SearchOption.TopDirectoryOnly))
        {
            Log.Warning("Ignored a plugin DLL placed directly in the plugins folder; each plugin must be in its own folder: {Path}", misplacedAssembly);
        }

        foreach (var folder in Directory.EnumerateDirectories(_pluginsDirectory)
                     .OrderBy(path => Directory.GetCreationTimeUtc(path))
                     .ThenBy(path => Path.GetFileName(path), StringComparer.OrdinalIgnoreCase))
        {
            if (!File.Exists(Path.Combine(folder, UninstallMarkerFileName)))
            {
                AddEntry(ReadPlugin(folder));
                continue;
            }

            if (!TryDeleteUninstalledFolder(folder))
            {
                var entry = new PluginEntry(Path.GetFileName(folder), folder);
                SetFailure(entry, PluginErrorReason.DeletionFailed, []);
                AddEntry(entry);
            }
        }

        return GetDiscoveredPlugins();
    }

    /// <summary>
    /// 読めたプラグインを全部初期化する(起動の処理で1回)。読めなかった・初期化できなかったプラグインの名前を返す。
    /// 削除できなかったフォルダは返さない(読み込みの失敗ではなく、直し方も違う)。
    /// </summary>
    public IReadOnlyList<string> InitializeAll()
    {
        DiscoverPlugins();

        var entries = Volatile.Read(ref _entrySnapshot);
        foreach (var entry in entries.Where(entry => entry.State == PluginState.Loaded))
        {
            Initialize(entry);
        }

        var culture = LocalizationManager.Instance.CurrentCulture;
        return entries
            .Where(entry => entry.State == PluginState.Failed && entry.ErrorReason != PluginErrorReason.DeletionFailed)
            .Select(entry => entry.Info.GetDisplayName(culture))
            .ToArray();
    }

    /// <summary>一覧の行の状態。<paramref name="pluginKey"/> は <see cref="DiscoverPlugins"/> が返したプラグインの鍵。</summary>
    public PluginStatus GetStatus(string pluginKey)
    {
        var entry = _entries[pluginKey];
        return entry.State switch
        {
            PluginState.PendingDeletion => new PluginStatus(PluginRowState.PendingDeletion, null, [], CanUninstall: false),
            PluginState.Failed or PluginState.Stopped => new PluginStatus(
                PluginRowState.Error,
                entry.ErrorReason ?? throw new InvalidOperationException($"The plugin '{entry.Key}' failed without a reason."),
                entry.MissingFiles,
                CanUninstall: !IsAwaitingDeletion(entry)),
            _ => new PluginStatus(
                entry.Info.HasWindow ? PluginRowState.Openable : PluginRowState.NoWindow,
                null,
                [],
                CanUninstall: true),
        };
    }

    /// <summary>窓を開く。開いていれば前に出す。動いていなければ何もしない(押せる行で動いていないのは、初期化の前の行だけ)。</summary>
    public void Open(PluginListItemViewModel pluginItem)
    {
        ArgumentNullException.ThrowIfNull(pluginItem);

        if (!_entries.TryGetValue(pluginItem.PluginKey, out var entry) || entry.State != PluginState.Running)
        {
            return;
        }

        if (entry.Instance is not IPluginWindowProvider windowProvider
            || PluginWindowManager.Instance.ActivateExisting(entry.Key))
        {
            return;
        }

        var guard = entry.Guard!;
        if (!guard.TryInvoke(
                "Window content creation",
                () => windowProvider.CreateContent() ?? throw new InvalidOperationException("CreateContent returned null."),
                out var content))
        {
            return;
        }

        if (!guard.TryInvoke("Window options", windowProvider.GetWindowOptions, out var windowOptions))
        {
            DisposeContent(entry, content);
            return;
        }

        PluginWindowManager.Instance.Open(pluginItem, content, windowOptions, closedContent => DisposeContent(entry, closedContent));
    }

    /// <summary>
    /// プラグインの一覧の「＋」。DLLを選ばせ、要るファイル(<see cref="PluginDependencyScanner"/>)が全部あれば
    /// <c>Plugins\&lt;アセンブリ名&gt;\</c> へ写して読み込み、初期化して、<see cref="PluginRegistered"/> を出す。
    /// 選んだファイルがプラグインかは写す前に確かめる(<see cref="ReadPluginKey"/>)。プラグインでないファイルを写すと、
    /// 起動のたびに読めないフォルダが Plugins に残るため。
    /// </summary>
    public void AddFromFile()
    {
        var owner = Application.Current?.MainWindow;
        var dialog = new OpenFileDialog
        {
            ClientGuid = AddPluginDialogClientGuid,
            Filter = $"{LocalizationManager.Instance.GetString("PluginAdd_FileFilter")}|*.dll",
            CheckFileExists = true,
            Multiselect = false
        };

        if (UserFolders.Downloads is { } downloads)
        {
            dialog.DefaultDirectory = downloads;
        }

        var selected = owner is null ? dialog.ShowDialog() : dialog.ShowDialog(owner);
        if (selected != true)
        {
            return;
        }

        DiscoverPlugins();

        var sourcePath = Path.GetFullPath(dialog.FileName);
        var sourceName = Path.GetFileNameWithoutExtension(sourcePath);
        if (!string.Equals(Path.GetExtension(sourcePath), ".dll", StringComparison.OrdinalIgnoreCase))
        {
            PluginAddFailureMessage.ShowInvalidFile(owner);
            return;
        }

        PluginDependencyScan scan;
        try
        {
            scan = PluginDependencyScanner.Scan(sourcePath);
        }
        catch (Exception exception)
        {
            LogPluginError(sourceName, "The selected file is not a supported plugin DLL.", exception);
            PluginAddFailureMessage.ShowInvalidFile(owner);
            return;
        }

        if (scan.MissingFiles.Count > 0)
        {
            LogPluginError(sourceName, $"The selected plugin is missing files: {string.Join(", ", scan.MissingFiles)}", null);
            PluginAddFailureMessage.ShowMissingFiles(owner, scan.MissingFiles);
            return;
        }

        if (ReadPluginKey(sourcePath) is not { } pluginKey)
        {
            PluginAddFailureMessage.ShowInvalidFile(owner);
            return;
        }

        var targetFolder = Path.Combine(Path.GetFullPath(_pluginsDirectory), pluginKey);
        _entries.TryGetValue(pluginKey, out var existingEntry);
        if (existingEntry is not null && IsAwaitingDeletion(existingEntry))
        {
            PluginAddFailureMessage.ShowPendingDeletion(owner);
            return;
        }

        if (existingEntry is not null || Directory.Exists(targetFolder))
        {
            PluginAlreadyAddedMessage.Show(owner);
            return;
        }

        try
        {
            foreach (var file in scan.Files)
            {
                var targetPath = Path.Combine(targetFolder, Path.GetRelativePath(scan.Folder, file));
                Directory.CreateDirectory(Path.GetDirectoryName(targetPath)!);
                File.Copy(file, targetPath, overwrite: false);
            }
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            LogPluginError(pluginKey, "Failed to copy the plugin files to the plugins folder.", exception);
            TryDeleteFolder(targetFolder);
            PluginAddFailureMessage.ShowCannotPlaceFile(owner);
            return;
        }

        var addedEntry = ReadPlugin(targetFolder);
        AddEntry(addedEntry);
        if (addedEntry.State == PluginState.Loaded)
        {
            Initialize(addedEntry);
        }

        if (addedEntry.State == PluginState.Failed)
        {
            PluginLoadFailureMessage.Show(owner, [addedEntry.Info.GetDisplayName(LocalizationManager.Instance.CurrentCulture)]);
        }

        PluginRegistered?.Invoke(this, addedEntry.Info);
    }

    /// <summary>
    /// プラグインの一覧のゴミ箱。確かめてから、フォルダにアンインストールの印を書き、動いていれば止めて、削除を待つ状態にし、通知タブとウィジェットのそのプラグインの設定を消す。
    /// フォルダは次の起動で消す(<see cref="DiscoverPlugins"/>)。印を書けなければ止めずに、手で消す直し方の窓を出す。
    /// </summary>
    public void Uninstall(string pluginKey)
    {
        if (!_entries.TryGetValue(pluginKey, out var entry)
            || IsAwaitingDeletion(entry)
            || Application.Current?.MainWindow is not { } owner
            || !PluginUninstallMessage.Confirm(owner, entry.Info.GetDisplayName(LocalizationManager.Instance.CurrentCulture)))
        {
            return;
        }

        try
        {
            File.WriteAllText(Path.Combine(entry.Folder, UninstallMarkerFileName), $"{DateTime.Now:O}{Environment.NewLine}");
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            Log.Error(exception, "[Plugin:{Plugin}] Failed to write the uninstall marker", entry.Key);
            PluginDeleteFailureMessage.Show(owner, entry.Key);
            return;
        }

        Log.Information("[Plugin:{Plugin}] Uninstalled the plugin; its folder is deleted at the next start", entry.Key);
        if (entry.State == PluginState.Running)
        {
            Stop(entry, StopCause.Uninstall);
        }
        else
        {
            entry.State = PluginState.PendingDeletion;
            RaiseStatusChanged(entry);
        }

        // 通知タブから項目を外し、ウィジェットの窓を閉じた(止める処理)後に消す。外すときに通知タブは待っている文章を、窓は位置を保存し、先に消すと作り直されるため。
        ConfigManager.Instance.RemovePluginNotifications(entry.Key);
        WidgetStateManager.Instance.RemovePluginWidgets(entry.Key);
    }

    /// <summary>アプリを終えるときに、動いているプラグインを全部終える。</summary>
    public void ShutdownAll()
    {
        foreach (var entry in _entries.Values.Where(entry => entry.State == PluginState.Running).ToArray())
        {
            entry.State = PluginState.Stopped;
            entry.Runtime!.Release();
            entry.Guard!.TryInvoke("Plugin shutdown", entry.Instance!.Shutdown);
        }
    }

    /// <summary>
    /// 画面のスレッドの受け手の無い例外が、プラグインのコードから出たものなら、そのプラグインを止めて true を返す(アプリは続ける)。
    /// </summary>
    public bool TryStopForUnhandledException(Exception exception)
    {
        if (FindPluginOf(exception) is not { } entry)
        {
            return false;
        }

        StopForUnhandledException(entry, exception);
        return true;
    }

    /// <summary>
    /// ほかのスレッドの受け手の無い例外(待たれなかった <see cref="Task"/>)が、プラグインのコードから出たものなら、
    /// 画面のスレッドでそのプラグインを止めて true を返す(アプリは続ける)。
    /// </summary>
    public bool TryStopForUnobservedException(Exception exception)
    {
        if (FindPluginOf(exception) is not { } entry)
        {
            return false;
        }

        if (Application.Current?.Dispatcher is { } dispatcher)
        {
            dispatcher.BeginInvoke(() => StopForUnhandledException(entry, exception));
        }

        return true;
    }

    /// <summary>
    /// アプリを終わらせる例外(ほかのスレッドの例外)が、プラグインのコードから出たものなら、そのプラグインのフォルダに印を書く。
    /// 次の起動からは、印を消すまでそのプラグインを初期化しない。
    /// </summary>
    public void MarkTerminatedApp(Exception exception)
    {
        if (FindPluginOf(exception) is not { } entry)
        {
            return;
        }

        Log.Error("[Plugin:{Plugin}] The plugin terminated the app; it is not initialized from the next start", entry.Key);
        try
        {
            File.WriteAllText(
                Path.Combine(entry.Folder, TerminatedAppMarkerFileName),
                $"{DateTime.Now:O}{Environment.NewLine}{exception.GetType().FullName}: {exception.Message}{Environment.NewLine}");
        }
        catch (Exception markerException) when (markerException is IOException or UnauthorizedAccessException)
        {
            Log.Error(markerException, "[Plugin:{Plugin}] Failed to write the terminated-app marker", entry.Key);
        }
    }

    /// <summary>読み込んだプラグインの DLL(確認用の一覧が文言を引くのに使う)。読めていなければ null。</summary>
    internal Assembly? FindPluginAssembly(string assemblyName)
    {
        DiscoverPlugins();
        return _entries.TryGetValue(assemblyName, out var entry)
            ? entry.LoadContext?.Assemblies.FirstOrDefault(assembly =>
                string.Equals(assembly.GetName().Name, assemblyName, StringComparison.OrdinalIgnoreCase))
            : null;
    }

    private IReadOnlyList<PluginInfo> GetDiscoveredPlugins()
    {
        return Volatile.Read(ref _entrySnapshot)
            .Select(entry => entry.Info)
            .ToArray();
    }

    private void AddEntry(PluginEntry entry)
    {
        _entries.Add(entry.Key, entry);
        Volatile.Write(ref _entrySnapshot, [.. _entrySnapshot, entry]);
    }

    /// <summary>プラグインのフォルダを読む。要るファイルが足りない・読めない・属性が足りないときは失敗の状態で返す(一覧には出す)。</summary>
    private PluginEntry ReadPlugin(string folder)
    {
        var entry = new PluginEntry(Path.GetFileName(folder), folder);
        try
        {
            if (!File.Exists(entry.MainAssemblyPath))
            {
                Fail(
                    entry,
                    PluginErrorReason.MissingFiles,
                    "The plugin folder has no DLL with the folder's name.",
                    null,
                    [Path.GetFileName(entry.MainAssemblyPath)]);
                return entry;
            }

            var scan = PluginDependencyScanner.Scan(entry.MainAssemblyPath);
            if (scan.MissingFiles.Count > 0)
            {
                Fail(
                    entry,
                    PluginErrorReason.MissingFiles,
                    $"The plugin is missing files: {string.Join(", ", scan.MissingFiles)}",
                    null,
                    scan.MissingFiles);
                return entry;
            }

            var loadContext = new PluginLoadContext(entry.MainAssemblyPath, readsFromMemory: false);
            var assembly = loadContext.LoadPluginAssembly(entry.MainAssemblyPath);

            // WPF の資源の指定(/アセンブリ名;component/...)は、まだ読まれていない部品をこの読み込み先から探せないので、部品を先に読む。
            foreach (var dependencyPath in scan.ManagedAssemblies.Where(path =>
                         !string.Equals(path, scan.ManagedAssemblies[0], StringComparison.OrdinalIgnoreCase)))
            {
                loadContext.LoadFromAssemblyName(AssemblyName.GetAssemblyName(dependencyPath));
            }

            var registration = CreateRegistration(assembly, entry.Key);
            entry.LoadContext = loadContext;
            entry.Info = registration.Info;
            entry.EntryType = registration.EntryType;
            entry.State = PluginState.Loaded;
        }
        catch (Exception exception)
        {
            Fail(entry, PluginErrorReason.Invalid, "Plugin DLL registration failed.", exception);
        }

        return entry;
    }

    private void Initialize(PluginEntry entry)
    {
        InitializeCore(entry);
        RaiseStatusChanged(entry);
    }

    private void InitializeCore(PluginEntry entry)
    {
        if (File.Exists(Path.Combine(entry.Folder, TerminatedAppMarkerFileName)))
        {
            Fail(
                entry,
                PluginErrorReason.TerminatedApp,
                $"The plugin terminated the app in an earlier run; delete {TerminatedAppMarkerFileName} in its folder or reinstall it to load it again.",
                null);
            return;
        }

        entry.State = PluginState.Initializing;
        var guard = new PluginCallGuard((operation, exception) => OnPluginCallFailed(entry, operation, exception));
        if (!guard.TryInvoke(
                "Plugin construction",
                () => (IStarResonancePlugin)Activator.CreateInstance(entry.EntryType!)!,
                out var instance))
        {
            SetFailure(entry, PluginErrorReason.Stopped, []);
            return;
        }

        var logger = new PluginLogger(entry.Key);
        var runtime = new PluginRuntime(
            guard,
            entry.Key,
            PluginPacketHub.Instance.CreateAccount(() => OnPacketBacklog(entry)));
        var context = new PluginHostContext(
            new PluginSettingsStore(entry.Folder, logger),
            new PluginMessageService(),
            runtime.Localization,
            logger,
            runtime.GameData,
            PluginNameService.Instance,
            runtime.Packets,
            runtime.Notifications,
            runtime.Stopping);

        entry.Instance = instance;
        entry.Guard = guard;
        entry.Runtime = runtime;
        if (!guard.TryInvoke("Plugin initialization", () => instance.Initialize(context)))
        {
            StopInitializing(entry, PluginErrorReason.Stopped);
            return;
        }

        if (!TryReadWidgets(entry, instance, out var widgetDefinitions, out var multiWidgetDefinitions))
        {
            return;
        }

        if (instance is IPluginNotificationProvider notificationProvider && !TryRegisterNotifications(entry, notificationProvider))
        {
            return;
        }

        // ウィジェットのカードは動き出した後に足す(カードから窓を開くと中身を作るのにプラグインを呼び、その例外で止められる状態にしておく)。
        entry.State = PluginState.Running;
        PluginWidgetRegistry.Instance.Register(
            entry.Info,
            widgetDefinitions,
            multiWidgetDefinitions,
            guard,
            instance as IPluginWidgetProvider,
            instance as IPluginMultiWidgetProvider);
    }

    /// <summary>
    /// 初期化の後に両方の口のウィジェットの定義を受け取って確かめる(ウィジェットの ID は両方を通して重ならない)。
    /// 定義を返すときの例外と、決まりに合わない定義では、そのプラグインを止めて false を返す。
    /// </summary>
    private static bool TryReadWidgets(
        PluginEntry entry,
        IStarResonancePlugin instance,
        out IReadOnlyList<PluginWidgetDefinition> definitions,
        out IReadOnlyList<PluginWidgetDefinition> multiWindowDefinitions)
    {
        definitions = [];
        multiWindowDefinitions = [];
        var ids = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if (instance is IPluginWidgetProvider provider
            && !TryReadWidgetDefinitions(entry, "Widget definitions", provider.GetWidgetDefinitions, ids, out definitions))
        {
            return false;
        }

        return instance is not IPluginMultiWidgetProvider multiProvider
            || TryReadWidgetDefinitions(entry, "Multi-window widget definitions", multiProvider.GetMultiWidgetDefinitions, ids, out multiWindowDefinitions);
    }

    private static bool TryReadWidgetDefinitions(
        PluginEntry entry,
        string operation,
        Func<IReadOnlyList<PluginWidgetDefinition>> read,
        HashSet<string> ids,
        out IReadOnlyList<PluginWidgetDefinition> definitions)
    {
        definitions = [];
        if (!entry.Guard!.TryInvoke(operation, read, out var returned))
        {
            StopInitializing(entry, PluginErrorReason.Stopped);
            return false;
        }

        if (!PluginWidgetValidator.TryValidate(returned, ids, out var copies, out var error))
        {
            LogPluginError(entry.Key, $"The widget definitions are invalid: {error}", null);
            StopInitializing(entry, PluginErrorReason.InvalidWidgets);
            return false;
        }

        definitions = copies;
        return true;
    }

    /// <summary>
    /// 初期化の後に通知の項目の定義を受け取り、確かめて通知タブへ渡す。定義を返すときの例外と、決まりに合わない定義では、そのプラグインを止めて false を返す。
    /// </summary>
    private static bool TryRegisterNotifications(PluginEntry entry, IPluginNotificationProvider provider)
    {
        if (!entry.Guard!.TryInvoke("Notification definitions", provider.GetNotificationDefinitions, out var definitions))
        {
            StopInitializing(entry, PluginErrorReason.Stopped);
            return false;
        }

        if (!PluginNotificationValidator.TryValidate(definitions, out var copies, out var error))
        {
            LogPluginError(entry.Key, $"The notification definitions are invalid: {error}", null);
            StopInitializing(entry, PluginErrorReason.InvalidNotifications);
            return false;
        }

        PluginNotificationRegistry.Instance.Register(entry.Info, copies, entry.Runtime!.Notifications);
        return true;
    }

    /// <summary>初期化の途中で失敗した(動き出す前)。理由を付け、渡した口を外して、終わりを呼ぶ。</summary>
    private static void StopInitializing(PluginEntry entry, PluginErrorReason reason)
    {
        SetFailure(entry, reason, []);
        entry.Runtime!.Release();
        entry.Guard!.TryInvoke("Plugin shutdown", entry.Instance!.Shutdown);
    }

    /// <summary>
    /// 動いているプラグインを止める(その起動の間)。窓を閉じて中身を後始末し、ウィジェットのカードと窓・通知タブの項目を外し(設定は残す)、止める合図を出し、通信の受け手から外し、終わりを呼ぶ。
    /// 例外と通信の取り残しで止めたときは「停止しました」の窓を出す。アンインストールでは出さず、削除を待つ状態にする。
    /// 画面のスレッドで呼ぶ。
    /// </summary>
    private void Stop(PluginEntry entry, StopCause cause)
    {
        if (entry.State != PluginState.Running)
        {
            return;
        }

        if (cause == StopCause.Uninstall)
        {
            entry.State = PluginState.PendingDeletion;
        }
        else
        {
            entry.State = PluginState.Stopped;
            entry.ErrorReason = cause == StopCause.PacketBacklog ? PluginErrorReason.PacketBacklog : PluginErrorReason.Stopped;
            Log.Warning("[Plugin:{Plugin}] Stopped the plugin until the app restarts", entry.Key);
        }

        PluginWindowManager.Instance.Close(entry.Key);
        PluginWidgetRegistry.Instance.Unregister(entry.Key);
        PluginNotificationRegistry.Instance.Unregister(entry.Key);
        entry.Runtime!.Release();
        entry.Guard!.TryInvoke("Plugin shutdown", entry.Instance!.Shutdown);
        RaiseStatusChanged(entry);

        if (cause == StopCause.Uninstall)
        {
            return;
        }

        // 本体の知らせを配っている途中で窓を待たない。
        var name = entry.Info.GetDisplayName(LocalizationManager.Instance.CurrentCulture);
        if (Application.Current?.Dispatcher is { HasShutdownStarted: false } dispatcher)
        {
            dispatcher.BeginInvoke(() => PluginStoppedMessage.Show(
                Application.Current?.MainWindow,
                name,
                isPacketBacklog: cause == StopCause.PacketBacklog));
        }
    }

    private void StopForUnhandledException(PluginEntry entry, Exception exception)
    {
        Log.Error(exception, "[Plugin:{Plugin}] Unhandled exception in plugin code", entry.Key);
        Stop(entry, StopCause.Exception);
    }

    /// <summary>本体がプラグインを呼んだ処理の例外(<see cref="PluginCallGuard"/>)。動いていれば止める。</summary>
    private void OnPluginCallFailed(PluginEntry entry, string operation, Exception exception)
    {
        Log.Error(exception, "[Plugin:{Plugin}] {Operation} failed", entry.Key, operation);
        Stop(entry, StopCause.Exception);
    }

    /// <summary>取り残しが上限を越えた(パケット処理のスレッド)。すぐに受け取りをやめて溜まった分を捨て、画面のスレッドで止める。</summary>
    private void OnPacketBacklog(PluginEntry entry)
    {
        Log.Error(
            "[Plugin:{Plugin}] The plugin did not read game messages; unread messages exceeded {Limit} bytes",
            entry.Key,
            PluginPacketHub.MaxPendingBytesPerPlugin);
        entry.Runtime?.Packets.Close();
        Application.Current?.Dispatcher.BeginInvoke(() => Stop(entry, StopCause.PacketBacklog));
    }

    private static void DisposeContent(PluginEntry entry, FrameworkElement content)
    {
        if (content is IDisposable disposableContent)
        {
            entry.Guard!.TryInvoke("Window content disposal", disposableContent.Dispose);
        }
    }

    /// <summary>例外の経路(内側の例外を含む)にプラグインの読み込み先の関数があれば、そのプラグイン。</summary>
    private PluginEntry? FindPluginOf(Exception exception)
    {
        var entries = Volatile.Read(ref _entrySnapshot);
        if (entries.Length == 0)
        {
            return null;
        }

        foreach (var current in EnumerateExceptions(exception))
        {
            foreach (var frame in new StackTrace(current, fNeedFileInfo: false).GetFrames())
            {
                if (frame.GetMethod()?.Module.Assembly is not { } assembly
                    || AssemblyLoadContext.GetLoadContext(assembly) is not PluginLoadContext loadContext)
                {
                    continue;
                }

                if (entries.FirstOrDefault(entry => ReferenceEquals(entry.LoadContext, loadContext)) is { } owner)
                {
                    return owner;
                }
            }
        }

        return null;
    }

    private static IEnumerable<Exception> EnumerateExceptions(Exception exception)
    {
        yield return exception;
        if (exception is AggregateException aggregate)
        {
            foreach (var inner in aggregate.InnerExceptions.SelectMany(EnumerateExceptions))
            {
                yield return inner;
            }
        }
        else if (exception.InnerException is { } innerException)
        {
            foreach (var inner in EnumerateExceptions(innerException))
            {
                yield return inner;
            }
        }
    }

    /// <summary>
    /// DLLが起動時と同じ確かめ(<see cref="CreateRegistration"/>)を通るプラグインかを見て、プラグインの鍵(アセンブリ名)を返す。通らなければnull。
    /// ファイルを掴まないよう、中身をメモリから捨てられる読み込み先に読み(部品も選んだDLLのフォルダから)、見終わったら捨てる。
    /// </summary>
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static string? ReadPluginKey(string assemblyPath)
    {
        var loadContext = new PluginLoadContext(assemblyPath, readsFromMemory: true);
        try
        {
            var assembly = loadContext.LoadPluginAssembly(assemblyPath);
            return CreateRegistration(assembly, Path.GetFileNameWithoutExtension(assemblyPath)).Info.Key;
        }
        catch (Exception exception)
        {
            LogPluginError(Path.GetFileNameWithoutExtension(assemblyPath), "The selected file is not a supported plugin DLL.", exception);
            return null;
        }
        finally
        {
            loadContext.Unload();
        }
    }

    /// <param name="fileName">入口の DLL のファイル名(拡張子なし)。アセンブリ名と同じでなければならない(フォルダの名前と WPF の資源の指定に使う)。</param>
    private static PluginRegistration CreateRegistration(Assembly assembly, string fileName)
    {
        var assemblyName = assembly.GetName().Name ?? string.Empty;
        if (!string.Equals(assemblyName, fileName, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException($"The plugin DLL file name '{fileName}' must match its assembly name '{assemblyName}'.");
        }

        if (PluginDependencyScanner.IsHostAssemblyName(assemblyName))
        {
            throw new InvalidOperationException($"The plugin assembly name '{assemblyName}' is used by the app.");
        }

        var registrationAttribute = assembly.GetCustomAttribute<PluginRegistrationAttribute>()
            ?? throw new InvalidOperationException("The plugin registration attribute is missing.");

        if (registrationAttribute.ApiVersion != PluginSdkVersion.Current)
        {
            throw new InvalidOperationException($"Unsupported plugin API version '{registrationAttribute.ApiVersion}'.");
        }

        var entryType = registrationAttribute.EntryType
            ?? throw new InvalidOperationException("The plugin entry type is missing.");

        if (!ReferenceEquals(entryType.Assembly, assembly))
        {
            throw new InvalidOperationException("The plugin entry type must be declared in the plugin DLL itself.");
        }

        if (!entryType.IsClass
            || entryType.IsAbstract
            || !entryType.IsPublic
            || entryType.ContainsGenericParameters)
        {
            throw new InvalidOperationException("The plugin entry type must be a public, non-abstract, non-generic class.");
        }

        if (!typeof(IStarResonancePlugin).IsAssignableFrom(entryType))
        {
            throw new InvalidOperationException($"'{entryType.FullName}' does not implement IStarResonancePlugin.");
        }

        if (entryType.GetConstructor(Type.EmptyTypes) is null)
        {
            throw new InvalidOperationException("The plugin entry type must have a public parameterless constructor.");
        }

        var displayNames = ReadLocalizedTexts(
            assembly.GetCustomAttributes<PluginDisplayNameAttribute>().Select(attribute => (attribute.CultureName, attribute.DisplayName)),
            "display name");
        if (displayNames.Count == 0)
        {
            throw new InvalidOperationException("At least one localized plugin display name is required.");
        }

        var author = assembly.GetCustomAttribute<PluginAuthorAttribute>()?.Author.Trim();
        if (string.IsNullOrEmpty(author))
        {
            throw new InvalidOperationException("The plugin author attribute is missing or empty.");
        }

        var version = assembly.GetCustomAttribute<PluginVersionAttribute>()?.Version.Trim();
        if (!PluginVersionRules.IsValid(version))
        {
            throw new InvalidOperationException("The plugin version attribute is missing or is not three numbers separated by dots.");
        }

        var hasWindow = typeof(IPluginWindowProvider).IsAssignableFrom(entryType);
        IReadOnlyDictionary<string, string>? genres = null;
        if (hasWindow)
        {
            genres = ReadLocalizedTexts(
                assembly.GetCustomAttributes<PluginGenreAttribute>().Select(attribute => (attribute.CultureName, attribute.Genre)),
                "genre");
            if (genres.Count == 0)
            {
                throw new InvalidOperationException("A plugin with a window requires at least one localized genre.");
            }
        }

        var iconPath = assembly.GetCustomAttribute<PluginIconAttribute>()?.ResourcePath.Trim();
        var info = new PluginInfo(
            assemblyName,
            displayNames,
            genres,
            author,
            version,
            string.IsNullOrEmpty(iconPath) ? null : iconPath,
            hasWindow,
            hasWidgets: typeof(IPluginWidgetProvider).IsAssignableFrom(entryType)
                || typeof(IPluginMultiWidgetProvider).IsAssignableFrom(entryType),
            hasNotifications: typeof(IPluginNotificationProvider).IsAssignableFrom(entryType));
        return new PluginRegistration(info, entryType);
    }

    private static IReadOnlyDictionary<string, string> ReadLocalizedTexts(
        IEnumerable<(string CultureName, string Text)> attributes,
        string description)
    {
        return PluginTexts.TryCopy(attributes.Select(attribute => KeyValuePair.Create(attribute.CultureName, attribute.Text)))
            ?? throw new InvalidOperationException($"The plugin contains duplicate {description}s for one culture.");
    }

    private static void Fail(
        PluginEntry entry,
        PluginErrorReason reason,
        string message,
        Exception? exception,
        IReadOnlyList<string>? missingFiles = null)
    {
        SetFailure(entry, reason, missingFiles ?? []);
        LogPluginError(entry.Key, message, exception);
    }

    private static void SetFailure(PluginEntry entry, PluginErrorReason reason, IReadOnlyList<string> missingFiles)
    {
        entry.State = PluginState.Failed;
        entry.ErrorReason = reason;
        entry.MissingFiles = missingFiles;
    }

    /// <summary>アンインストールした(削除を待っている、または削除できず次の起動でまた消す)。</summary>
    private static bool IsAwaitingDeletion(PluginEntry entry)
    {
        return entry.State == PluginState.PendingDeletion || entry.ErrorReason == PluginErrorReason.DeletionFailed;
    }

    private void RaiseStatusChanged(PluginEntry entry)
    {
        PluginStatusChanged?.Invoke(this, entry.Key);
    }

    /// <summary>
    /// アンインストールの印のあるフォルダを消す。印は最後に消す(途中で失敗しても印が残り、次の起動でまた消せる)。
    /// </summary>
    private static bool TryDeleteUninstalledFolder(string folder)
    {
        var markerPath = Path.Combine(folder, UninstallMarkerFileName);
        try
        {
            foreach (var directory in Directory.GetDirectories(folder))
            {
                Directory.Delete(directory, recursive: true);
            }

            foreach (var file in Directory.GetFiles(folder)
                         .Where(path => !string.Equals(Path.GetFileName(path), UninstallMarkerFileName, StringComparison.OrdinalIgnoreCase)))
            {
                File.Delete(file);
            }

            File.Delete(markerPath);
            Directory.Delete(folder);
            Log.Information("Deleted the uninstalled plugin folder: {Folder}", folder);
            return true;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            Log.Error(exception, "Failed to delete the uninstalled plugin folder; it is deleted again at the next start: {Folder}", folder);
            return false;
        }
    }

    private static void LogPluginError(string pluginName, string message, Exception? exception)
    {
        Log.Error(exception, "[Plugin:{Plugin}] {Message}", pluginName, message);
    }

    private static void TryDeleteFolder(string folder)
    {
        try
        {
            if (Directory.Exists(folder))
            {
                Directory.Delete(folder, recursive: true);
            }
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            Log.Error(exception, "Failed to delete the partially copied plugin folder: {Folder}", folder);
        }
    }

    private enum PluginState
    {
        /// <summary>読めた。まだ初期化していない。</summary>
        Loaded,

        Initializing,

        Running,

        /// <summary>読めない・初期化できない。初期化はしない。</summary>
        Failed,

        /// <summary>この起動の間は止めた(例外・取り残し・アプリの終わり)。次の起動でまた読む。</summary>
        Stopped,

        /// <summary>アンインストールした。次の起動で、読む前にフォルダを消す。</summary>
        PendingDeletion,
    }

    private enum StopCause
    {
        Exception,

        PacketBacklog,

        Uninstall,
    }

    private sealed class PluginEntry
    {
        public PluginEntry(string key, string folder)
        {
            Key = key;
            Folder = folder;
            Info = new PluginInfo(
                key,
                displayNames: null,
                genres: null,
                author: null,
                version: null,
                iconPath: null,
                hasWindow: false,
                hasWidgets: false,
                hasNotifications: false);
        }

        public string Key { get; }

        public string Folder { get; }

        public string MainAssemblyPath => Path.Combine(Folder, Key + ".dll");

        public PluginInfo Info { get; set; }

        public PluginState State { get; set; } = PluginState.Failed;

        /// <summary>失敗した・止めた理由。<see cref="PluginState.Failed"/> と <see cref="PluginState.Stopped"/> のときに値がある。</summary>
        public PluginErrorReason? ErrorReason { get; set; }

        /// <summary><see cref="PluginErrorReason.MissingFiles"/> のときの、足りないファイルの名前。</summary>
        public IReadOnlyList<string> MissingFiles { get; set; } = [];

        public PluginLoadContext? LoadContext { get; set; }

        public Type? EntryType { get; set; }

        public IStarResonancePlugin? Instance { get; set; }

        public PluginCallGuard? Guard { get; set; }

        public PluginRuntime? Runtime { get; set; }
    }

    /// <summary>プラグイン1つに渡した、本体の知らせを受ける口と通信の受け取りと通知の口と止める合図。止める・終えるときにまとめて外す。</summary>
    private sealed class PluginRuntime
    {
        private readonly PluginCallGuard _guard;
        private readonly CancellationTokenSource _stopping = new();

        public PluginRuntime(PluginCallGuard guard, string pluginKey, PluginPacketHub.Account packets)
        {
            _guard = guard;
            Packets = packets;
            Localization = new PluginLocalizationService(guard);
            GameData = new PluginGameDataService(guard, PluginGameDataSource.Instance);
            Notifications = new PluginNotificationService(guard, pluginKey);
        }

        public PluginLocalizationService Localization { get; }

        public PluginGameDataService GameData { get; }

        public PluginPacketHub.Account Packets { get; }

        public PluginNotificationService Notifications { get; }

        public CancellationToken Stopping => _stopping.Token;

        public void Release()
        {
            // 取り消しはプラグインが登録した処理をその場で呼ぶ。
            _guard.TryInvoke("Stopping notification", _stopping.Cancel);
            Packets.Close();
            Localization.Detach();
            GameData.Detach();
            Notifications.Detach();
        }
    }

    private sealed record PluginRegistration(PluginInfo Info, Type EntryType);
}
