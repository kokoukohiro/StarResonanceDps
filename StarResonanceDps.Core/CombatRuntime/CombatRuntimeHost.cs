using Serilog;
using StarResonanceDps.Core.CombatRuntime.DataTypes;
using StarResonanceDps.Core.CombatRuntime.Database;
using StarResonanceDps.Core.Logging;

namespace StarResonanceDps.Core.CombatRuntime;

public sealed class CombatRuntimeHost
{
    private static readonly Lazy<CombatRuntimeHost> LazyInstance = new(() => new CombatRuntimeHost());
    private readonly object _sync = new();
    private bool _isInitialized;

    private CombatRuntimeHost()
    {
    }

    public static CombatRuntimeHost Instance => LazyInstance.Value;

    public bool IsInitialized
    {
        get
        {
            lock (_sync)
            {
                return _isInitialized;
            }
        }
    }

    public void Initialize()
    {
        lock (_sync)
        {
            if (_isInitialized)
            {
                return;
            }

            Utils.MigratePersistedRuntimeFiles();
            Settings.Load();
            ConfigureLogging();
            DB.Init();
            AppState.LoadDataTables();
            Settings.Instance.Apply();

            if (string.IsNullOrEmpty(MessageManager.NetCaptureDeviceName))
            {
                var bestDefaultDevice = MessageManager.TryFindBestNetworkDevice();
                if (bestDefaultDevice is not null)
                {
                    MessageManager.NetCaptureDeviceName = bestDefaultDevice.Name;
                    Settings.Instance.NetCaptureDeviceName = bestDefaultDevice.Name;
                }
            }

            if (DB.CheckIfMigrationsNeeded())
            {
                DB.CheckAndRunMigrations();
            }

            if (!string.IsNullOrEmpty(MessageManager.NetCaptureDeviceName))
            {
                MessageManager.InitializeCapturing();
            }

            _isInitialized = true;
        }
    }

    public void Shutdown()
    {
        lock (_sync)
        {
            if (!_isInitialized)
            {
                return;
            }

            MessageManager.StopCapturing();
            if (EncounterManager.Current is not null)
            {
                EncounterManager.ShutdownManager();
            }

            DB.CloseAndSave();
            Settings.Save();

            var writingTimeout = System.Diagnostics.Stopwatch.StartNew();
            while (EntityCache.Instance.IsWritingFile)
            {
                Thread.Sleep(10);
                if (writingTimeout.Elapsed.TotalSeconds >= 6)
                {
                    Log.Warning("EntityCache writing exceeded the shutdown timeout.");
                    break;
                }
            }

            if (Settings.Instance.UseDatabaseForEncounterHistory
                && Settings.Instance.DatabaseRetentionPolicyDays > 0)
            {
                DB.ClearOldEncounters(Settings.Instance.DatabaseRetentionPolicyDays);
            }

            _isInitialized = false;
        }
    }

    private static void ConfigureLogging()
    {
        var loggerConfiguration = new LoggerConfiguration()
            .MinimumLevel.Verbose()
            .Enrich.FromLogContext()
            .WriteTo.Sink(new ManagerLogSink(PacketDiagnosticLogStore.Instance));

        if (Settings.Instance.LogToFile)
        {
            loggerConfiguration = loggerConfiguration.WriteTo.File(
                Path.Combine(Utils.DATA_DIR_NAME, "combat-runtime.log"));
        }

        Log.Logger = loggerConfiguration.CreateLogger();
    }
}
