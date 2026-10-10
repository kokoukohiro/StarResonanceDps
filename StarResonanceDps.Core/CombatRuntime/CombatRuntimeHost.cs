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

            // 設定は App 側が持っている。Core にファイルを読む口は無いので、
            // 呼ぶ前に CombatRuntimeSettings.Apply を通してもらう必要がある。
            // 忘れると既定値(キャプチャ自動・分割あり・保持なし)で黙って動いてしまうため、
            // ここで落とす。
            if (!CombatRuntimeSettings.HasBeenApplied)
            {
                throw new InvalidOperationException(
                    "CombatRuntimeSettings.Apply を呼んでから CombatRuntimeHost.Initialize を呼ぶこと。");
            }

            Directory.CreateDirectory(CombatRuntimePaths.DataDirectory);
            ConfigureLogging();
            DB.Init();
            AppState.LoadDataTables();

            if (string.IsNullOrEmpty(MessageManager.NetCaptureDeviceName))
            {
                var bestDefaultDevice = MessageManager.TryFindBestNetworkDevice();
                if (bestDefaultDevice is not null)
                {
                    MessageManager.NetCaptureDeviceName = bestDefaultDevice.Name;
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

            // 履歴の読み込みが閉じた接続に当たらないよう、先に止めて待つ。
            EncounterHistoryProvider.ShutdownLoads();
            MessageManager.StopCapturing();
            if (EncounterManager.Current is not null)
            {
                EncounterManager.ShutdownManager();
            }

            DB.CloseAndSave();

            // 0 は無限。掃除を回さない。
            if (CombatRuntimeSettings.DatabaseMaxEncounterCount > 0)
            {
                DB.TrimEncountersToLimit(CombatRuntimeSettings.DatabaseMaxEncounterCount, vacuum: true);
            }

            _isInitialized = false;
        }
    }

    private static void ConfigureLogging()
    {
        var loggerConfiguration = new LoggerConfiguration()
            .MinimumLevel.Verbose()
            .Enrich.FromLogContext()
            .WriteTo.Sink(new ManagerLogSink(PacketDiagnosticLogStore.Instance))
            .WriteTo.File(CombatRuntimePaths.LogFilePath);

        Log.Logger = loggerConfiguration.CreateLogger();
    }
}
