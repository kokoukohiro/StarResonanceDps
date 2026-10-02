using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Windows;
using StarResonanceDps.App.Config;
using StarResonanceDps.App.Localization;
using StarResonanceDps.App.Services;
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

        // 設定ファイルの改名(appsettings→AppSettings / widgetstate→WidgetSettings)は
        // ConfigManager と WidgetStateManager のどちらに触れるより前に済ませる。
        // どちらも遅延生成の singleton で、最初の参照で旧名のまま読み込んでしまう。
        AppDataPaths.MigrateLegacyFileNames();

        var configManager = ConfigManager.Instance;

        // アダプターを選び直したときの保存は Core からは行えない(Core にファイルの口が無い)。
        // 合図だけ受け取って、こちらで AppSettings.json へ書く。
        NetworkAdapterSession.Instance.CaptureSettingsPersistRequested +=
            (_, _) => configManager.PersistCaptureSettingsFromRuntime();
        var legacyClassColors = configManager.TakeLegacyClassColorSettings();
        if (legacyClassColors is not null)
        {
            WidgetStateManager.Instance.MigrateLegacyPlayerListClassColors(legacyClassColors);
        }

        var settings = configManager.GetSettingsSnapshot();
        LocalizationManager.Instance.ApplyLanguageIndex(settings.LanguageIndex);
        ThemeManager.Instance.ApplyGlobalTheme(settings);
        base.OnStartup(e);
        CombatRuntimeHost.Instance.Initialize();
        SkillCooldownTracker.Instance.Initialize();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        GlobalHotkeyService.Instance.Dispose();
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

