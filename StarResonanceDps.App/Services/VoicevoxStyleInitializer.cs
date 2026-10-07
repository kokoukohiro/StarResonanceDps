using System.IO;
using Serilog;
using StarResonanceDps.App.Config;

namespace StarResonanceDps.App.Services;

/// <summary>
/// 読み上げに使う VOICEVOX のスタイルを、通知より前に初期化しておく。
///
/// <para>
/// エンジンは初めて使うスタイルの合成に時間がかかり、合成の打ち切りを越えるとその通知は読み上げられない。
/// 合成の直前に初期化すると通知が遅れるので、アプリの起動時、全体設定の値が変わったとき(保存前プレビューと保存)、
/// VOICEVOX のエディタが起動して <c>runtime-info.json</c> を書いたときに送る。
/// 送るのは、通知方式が読み上げ・読み上げ方式が VOICEVOX・スタイルを選んでいる、の3つがそろうときだけ。
/// </para>
///
/// <para>
/// 返事は待たない(起動も設定画面も止めない)。初期化が通ったスタイルは、次の設定の変更では送らない(音量のスライダーのたびに送らないため)。
/// 失敗したら、次の設定の変更か起動でまた送る。失敗はログに残す(つながらないことは起動時と設定時の確認がメッセージで知らせる)。
/// </para>
///
/// <para>
/// エディタはエンジンが要求を受けられるようになる前に <c>runtime-info.json</c> を書くので、書かれたら
/// 初期化が通るまで <see cref="RetryInterval"/> ごとに送り直す。もう一度書かれたら最初からやり直す。
/// </para>
/// </summary>
public static class VoicevoxStyleInitializer
{
    private const string RuntimeInfoFileName = "runtime-info.json";

    private static readonly TimeSpan RetryInterval = TimeSpan.FromSeconds(1);

    private static readonly object Sync = new();
    private static bool _isStarted;
    private static FileSystemWatcher? _runtimeInfoWatcher;

    /// <summary>最後に初期化が通ったスタイル。</summary>
    private static int? _initializedStyleId;

    /// <summary>送って返事を待っているスタイル。</summary>
    private static int? _pendingStyleId;

    /// <summary><c>runtime-info.json</c> が書かれるたびに進める。送り直しは自分の番号が古くなったら止まる。</summary>
    private static int _runtimeInfoGeneration;

    /// <summary>アプリの起動時に1回呼ぶ。設定の変更と <c>runtime-info.json</c> の書き込みを受け始め、今の設定のスタイルを初期化する。</summary>
    public static void Start()
    {
        lock (Sync)
        {
            if (_isStarted)
            {
                return;
            }

            _isStarted = true;
        }

        ConfigManager.Instance.SettingsChanged += ConfigManager_SettingsChanged;
        ConfigManager.Instance.SettingsPreviewChanged += ConfigManager_SettingsChanged;
        WatchRuntimeInfo();
        InitializeSelectedStyle();
    }

    private static void WatchRuntimeInfo()
    {
        var directory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "voicevox");
        if (!Directory.Exists(directory))
        {
            Log.Information("VOICEVOX folder {Directory} does not exist; not watching {FileName}", directory, RuntimeInfoFileName);
            return;
        }

        try
        {
            var watcher = new FileSystemWatcher(directory, RuntimeInfoFileName)
            {
                NotifyFilter = NotifyFilters.FileName | NotifyFilters.LastWrite | NotifyFilters.CreationTime | NotifyFilters.Size
            };
            watcher.Created += RuntimeInfo_Written;
            watcher.Changed += RuntimeInfo_Written;
            watcher.Renamed += RuntimeInfo_Written;
            watcher.EnableRaisingEvents = true;
            _runtimeInfoWatcher = watcher;
        }
        catch (Exception ex) when (ex is ArgumentException or IOException or PlatformNotSupportedException)
        {
            Log.Warning(ex, "Could not watch VOICEVOX {FileName} in {Directory}", RuntimeInfoFileName, directory);
        }
    }

    private static void ConfigManager_SettingsChanged(object? sender, EventArgs e)
    {
        InitializeSelectedStyle();
    }

    /// <summary>エディタが起動した(エンジンも起動し直した)。覚えている初期化を捨て、通るまで送り直す。</summary>
    private static void RuntimeInfo_Written(object sender, FileSystemEventArgs e)
    {
        int generation;
        lock (Sync)
        {
            _initializedStyleId = null;
            _pendingStyleId = null;
            generation = ++_runtimeInfoGeneration;
        }

        _ = RetryUntilInitializedAsync(generation);
    }

    private static async Task RetryUntilInitializedAsync(int generation)
    {
        var hasLoggedWaiting = false;
        while (true)
        {
            if (GetSelectedStyleId() is not { } styleId)
            {
                return;
            }

            lock (Sync)
            {
                if (generation != _runtimeInfoGeneration || _initializedStyleId == styleId)
                {
                    return;
                }
            }

            var result = await VoicevoxClient.InitializeStyleAsync(styleId).ConfigureAwait(false);
            if (result.Kind == VoicevoxResultKind.Success)
            {
                lock (Sync)
                {
                    if (generation == _runtimeInfoGeneration)
                    {
                        _initializedStyleId = styleId;
                    }
                }

                Log.Information("Initialized VOICEVOX style {StyleId} after VOICEVOX started", styleId);
                return;
            }

            // つながったが応答を読めないなら、送り直しても変わらない。
            if (result.Kind != VoicevoxResultKind.ConnectFailed)
            {
                Log.Warning("Could not initialize VOICEVOX style {StyleId} after VOICEVOX started: {Detail}", styleId, result.Detail);
                return;
            }

            if (!hasLoggedWaiting)
            {
                hasLoggedWaiting = true;
                Log.Information("VOICEVOX started; waiting for the engine to initialize style {StyleId} ({Detail})", styleId, result.Detail);
            }

            await Task.Delay(RetryInterval).ConfigureAwait(false);
        }
    }

    private static void InitializeSelectedStyle()
    {
        if (GetSelectedStyleId() is not { } styleId)
        {
            return;
        }

        lock (Sync)
        {
            if (_initializedStyleId == styleId || _pendingStyleId == styleId)
            {
                return;
            }

            _pendingStyleId = styleId;
        }

        _ = InitializeAsync(styleId);
    }

    /// <summary>通知方式が読み上げ・読み上げ方式が VOICEVOX のときの、選んでいるスタイル。それ以外は null。</summary>
    private static int? GetSelectedStyleId()
    {
        var settings = ConfigManager.Instance.GetSettingsSnapshot();
        return settings.NotificationMethodIndex == AppConfigDefaults.NotificationMethodSpeechIndex
            && settings.SpeechVoiceIndex == AppConfigDefaults.SpeechVoiceVoicevoxIndex
                ? settings.VoicevoxStyleId
                : null;
    }

    private static async Task InitializeAsync(int styleId)
    {
        var result = await VoicevoxClient.InitializeStyleAsync(styleId).ConfigureAwait(false);
        lock (Sync)
        {
            if (_pendingStyleId == styleId)
            {
                _pendingStyleId = null;
            }

            if (result.Kind == VoicevoxResultKind.Success)
            {
                _initializedStyleId = styleId;
            }
        }

        if (result.Kind != VoicevoxResultKind.Success)
        {
            Log.Warning("Could not initialize VOICEVOX style {StyleId}: {Detail}", styleId, result.Detail);
        }
    }
}
