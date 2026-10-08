using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using StarResonanceDps.App.Config;
using StarResonanceDps.App.Localization;
using StarResonanceDps.App.Services;
using StarResonanceDps.App.Views;
using StarResonanceDps.Core.Logging;
using StarResonanceDps.Core.CombatRuntime;
using StarResonanceDps.Core.Services;

namespace StarResonanceDps.App;

public partial class App : Application
{
    private const string SingleInstanceMutexName = @"Local\StarResonanceDps.SingleInstance";
    private const int ShowWindowRestore = 9;

    private static Mutex? _singleInstanceMutex;

    protected override void OnStartup(StartupEventArgs e)
    {
        if (!TryAcquireSingleInstance())
        {
            BringExistingInstanceToFront();
            Shutdown(0);
            return;
        }

        ManagerTraceOutput.Configure();

        if (!TryEnsureWritableDataDirectory())
        {
            return;
        }

        var configManager = ConfigManager.Instance;

        // アダプターを選び直したときの保存は Core からは行えない(Core にファイルの口が無い)。
        // 合図だけ受け取って、こちらで AppSettings.json へ書く。
        NetworkAdapterSession.Instance.CaptureSettingsPersistRequested +=
            (_, _) => configManager.PersistCaptureSettingsFromRuntime();

        var settings = configManager.GetSettingsSnapshot();
        LocalizationManager.Instance.ApplyLanguageIndex(settings.LanguageIndex);
        ThemeManager.Instance.ApplyGlobalTheme(settings);
        base.OnStartup(e);
        CombatRuntimeHost.Instance.Initialize();
        SkillCooldownTracker.Instance.Initialize();

        // メイン窓はここで作る(App.xaml に StartupUri を置かない)。StartupUri だと、上の枝で終わるときも
        // OnStartup の後にメイン窓が作られて表示される。
        var mainWindow = new MainWindow();
        MainWindow = mainWindow;
        mainWindow.Show();
    }

    /// <summary>
    /// 設定・履歴・ログは実行フォルダの Data に書く。書けなければ動かせないので、知らせて終わる
    /// (ログもそのフォルダの中なので書けない)。窓の言語とテーマは、設定を読むだけ読んで当てる。
    /// </summary>
    private bool TryEnsureWritableDataDirectory()
    {
        try
        {
            CombatRuntimePaths.EnsureWritableDataDirectory();
            return true;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            // 窓を閉じたときに WPF が終了コード0で終わらせないよう、終わりは自分で決める。
            ShutdownMode = ShutdownMode.OnExplicitShutdown;
            var settings = ConfigManager.Instance.GetSettingsSnapshot();
            LocalizationManager.Instance.ApplyLanguageIndex(settings.LanguageIndex);
            ThemeManager.Instance.ApplyGlobalTheme(settings);
            DataFolderNotWritableMessage.Show(null, CombatRuntimePaths.DataDirectory);
            Shutdown(1);
            return false;
        }
    }

    protected override void OnExit(ExitEventArgs e)
    {
        GlobalHotkeyService.Instance.Dispose();
        NotificationService.Shutdown();
        SkillCooldownTracker.Instance.Shutdown();
        CombatRuntimeHost.Instance.Shutdown();
        PluginManager.Instance.ShutdownAll();
        ReleaseSingleInstance();
        base.OnExit(e);
    }

    private static bool TryAcquireSingleInstance()
    {
        _singleInstanceMutex = new Mutex(true, SingleInstanceMutexName, out var createdNew);
        if (createdNew)
        {
            return true;
        }

        _singleInstanceMutex.Dispose();
        _singleInstanceMutex = null;
        return false;
    }

    private static void ReleaseSingleInstance()
    {
        if (_singleInstanceMutex is null)
        {
            return;
        }

        _singleInstanceMutex.ReleaseMutex();
        _singleInstanceMutex.Dispose();
        _singleInstanceMutex = null;
    }

    private static void BringExistingInstanceToFront()
    {
        using var currentProcess = Process.GetCurrentProcess();
        foreach (var process in Process.GetProcessesByName(currentProcess.ProcessName))
        {
            using (process)
            {
                if (process.Id == currentProcess.Id)
                {
                    continue;
                }

                var handle = process.MainWindowHandle;
                if (handle == IntPtr.Zero)
                {
                    process.Refresh();
                    handle = process.MainWindowHandle;
                }

                if (handle == IntPtr.Zero)
                {
                    continue;
                }

                ShowWindow(handle, ShowWindowRestore);
                SetForegroundWindow(handle);
                return;
            }
        }
    }

    [DllImport("user32.dll")]
    private static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

    [DllImport("user32.dll")]
    private static extern bool SetForegroundWindow(IntPtr hWnd);
}

