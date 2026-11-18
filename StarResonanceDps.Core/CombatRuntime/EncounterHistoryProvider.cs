// This file is part of StarResonanceDps. See LICENSING.md for the license, additional permissions and additional terms.

using Serilog;
using StarResonanceDps.Core.CombatRuntime.Database;

namespace StarResonanceDps.Core.CombatRuntime;

/// <summary>保存済みエンカウンター1件ぶんの見出し。一覧に出すぶんだけ持つ。</summary>
/// <param name="SceneName">
/// 記録した時点の <c>GetSceneName</c> の出力。**言語も内部ID注記も当時のまま固定**なので、
/// 表示には使わない。<paramref name="SceneId"/> から引き直せなかったときの受け皿。
/// </param>
/// <param name="BenchmarkSeconds">
/// 計測の記録なら計測時間(秒、計測を始めたときの設定)、そうでなければ 0。
/// 出所は <c>EncounterExData.BenchmarkTime</c> で、書き込みは <c>EnterDungeon</c> の
/// <c>if (IsBenchmarkActive)</c> の中だけ。計測を終える側(停止・ログアウト・マップ移動)は
/// 印を先に下ろしてから次のエンカウンターを作るので、<b>計測の回にしか付かない。</b>
/// 待機中の回は記録が無く保存されないので、残るのは計測が始まった回だけ。
/// </param>
/// <param name="DungeonDifficulty">
/// ダンジョン同期で届いた難易度(マスターの段階)。出所は <c>EncounterExData.DungeonDifficulty</c>。
/// シーン名を引き直すときに難易度名を選ぶのに使う。
/// </param>
/// <param name="ChannelLine">
/// チャンネル番号。出所は <c>EncounterExData.ChannelLine</c>。チャンネルの無い場所の回は 0。
/// </param>
public sealed record EncounterHistoryEntry(
    ulong EncounterId,
    DateTime StartTime,
    uint SceneId,
    string SceneName,
    bool IsWipe,
    int BenchmarkSeconds,
    int DungeonDifficulty,
    uint ChannelLine);

/// <summary>履歴の1件を選んだ結果(<see cref="EncounterHistoryProvider.SelectAsync"/>)。</summary>
public enum HistorySelectResult
{
    /// <summary>読めた。選択の反映はパケットのスレッドへ積んだ(反映されると <see cref="EncounterHistoryProvider.SelectionChanged"/> が上がる)。</summary>
    Opened,

    /// <summary>DB にその回が無い(保存数の上限で消えたなど)。選択は変えない。</summary>
    NotFound,

    /// <summary>読み込みの間に別の回かライブが選ばれた(またはアプリを閉じている)。読んだ結果は捨てた。</summary>
    Superseded,

    /// <summary>読み込みで例外が起きた(ログに書いた)。選択は変えない。</summary>
    Failed
}

/// <summary>選択を変えない読み込みの結果(<see cref="EncounterHistoryProvider.LoadSummariesDetachedAsync"/>・<see cref="EncounterHistoryProvider.LoadDetachedAsync"/>)。</summary>
public enum HistoryReadStatus
{
    /// <summary>読めた。</summary>
    Succeeded,

    /// <summary>DB にその回が無い。</summary>
    NotFound,

    /// <summary>読み込みで例外が起きた(ログに書いた)。</summary>
    Failed,

    /// <summary>アプリを閉じた後なので読まなかった。</summary>
    Unavailable
}

/// <param name="Status">読めたか。</param>
/// <param name="Value">読めたときの値。それ以外は null。</param>
public sealed record HistoryRead<T>(HistoryReadStatus Status, T? Value);

/// <summary>保存した回のプラグインの値の書き込みの結果(<see cref="EncounterHistoryProvider.SetSavedPluginDataAsync"/>)。</summary>
public enum SavedPluginDataStatus
{
    /// <summary>書いた(長さ0なら消した)。</summary>
    Stored,

    /// <summary>DB にその回が無い。</summary>
    NotFound,

    /// <summary>そのプラグインのキーの数が上限を越えるので書かなかった。</summary>
    TooManyKeys,

    /// <summary>そのプラグインの値の長さの合計が上限を越えるので書かなかった。</summary>
    TooLarge,

    /// <summary>書き込みで例外が起きた(ログに書いた)。</summary>
    Failed,

    /// <summary>アプリを閉じた後なので書かなかった。</summary>
    Unavailable
}

/// <summary>
/// 集計タブが読む、保存済みエンカウンターの一覧と選択。
///
/// <para>
/// 選択の実体は <see cref="AppState.OpenedHistoricalEncounter"/> ただ1つ。
/// <c>MeterSnapshotProvider.ResolveActiveEncounter</c> が先頭でこれを見るので、
/// <b>ここへ入れるだけでメーター・スキル詳細・グラフ・集計のすべてが追従する</b>
/// (入口はどれも同じ関数を通る)。ウィジェットごとの切り替えは持たない。
/// </para>
///
/// <para>
/// <b>選択を書くのはパケットのスレッドだけ。</b> 履歴の回は裏のスレッドで読み、反映(選択・顔ぶれの作り直し・合図)を
/// <c>MessageManager.RunOnPacketThread</c> で積む。自動でライブへ戻す処理とログアウトもパケットのスレッドで走る。
/// 選ぶたびに要求の番号を進め、反映するのは最新の要求だけ(読み込みは途中で止められないので、古い結果は捨てる)。
/// </para>
///
/// <para>
/// 設定「履歴表示を自動解除」の自動の戻しは、プラグインが保持(<see cref="KeepOpen"/>)している間は行わない。
/// 選択を変えない読み書き(プラグインが回を読む・保存した回に値を書く)も、選択の読み込みと同じく裏のスレッドで走らせ、閉じるときに終わりを待つ。
/// </para>
/// </summary>
public static class EncounterHistoryProvider
{
    private static readonly object RequestGate = new();

    /// <summary>最新の選択の要求の番号。集計タブの操作(回を選ぶ・ライブへ戻す)とログアウトで進める。</summary>
    private static long _latestRequest;

    /// <summary>アプリを閉じている(<see cref="ShutdownLoads"/> の後)。新しい読み込みを受けず、反映もしない。</summary>
    private static bool _isShutDown;

    /// <summary>走っている読み込みと、選択を変えない読み書き。閉じるときに終わりを待つ(閉じた接続に当てない)。</summary>
    private static readonly List<Task> PendingLoads = [];

    /// <summary>最新の要求の回を読めなかった(<see cref="HasLoadFailed"/>)。</summary>
    private static bool _hasLoadFailed;

    /// <summary>自動の戻しを止めている保持の数(<see cref="KeepOpen"/>)。</summary>
    private static int _keepOpenCount;

    /// <summary>アプリを閉じている(<see cref="ShutdownLoads"/> の後)。新しい読み書きを受けない。</summary>
    public static bool IsShutDown
    {
        get
        {
            lock (RequestGate)
            {
                return _isShutDown;
            }
        }
    }

    /// <summary>
    /// 最新の要求の回を読めなかった(例外か、DB にその回が無い)。選択は変わらず、窓は前の表示のまま。
    /// 次に選び直したとき(回を開けた、ライブへ戻した)に下りる。ウィジェットの状態の印が読む。
    /// </summary>
    public static bool HasLoadFailed
    {
        get
        {
            lock (RequestGate)
            {
                return _hasLoadFailed;
            }
        }
    }

    /// <summary><see cref="HasLoadFailed"/> が変わったとき。読み込みのスレッドかパケットのスレッドから来る。</summary>
    public static event Action? LoadFailedChanged;

    /// <summary>いま履歴を開いているならその ID。ライブを見ているなら <c>null</c>。</summary>
    public static ulong? SelectedEncounterId => AppState.OpenedHistoricalEncounter?.EncounterId;

    /// <summary>
    /// 選択が変わったときに上がる。<b>自動でライブへ戻したときも上がる</b>ので、
    /// 一覧を出している側はこれを購読して「表示中」を出し直すこと。
    /// パケットのスレッドから来る(キャプチャが止まっていれば、選んだ操作のスレッドか読み込みのスレッド)ので、UI へは渡し直す。
    /// </summary>
    public static event Action? SelectionChanged;

    /// <summary>
    /// ライブ側でイベントが起きたので履歴表示を解除する。
    /// **設定が OFF か、履歴を開いていなければ何もしない。**
    ///
    /// <para>
    /// 呼ぶのは <c>Encounter.AddDamage</c> / <c>AddHealing</c>(戦闘)と
    /// <c>EncounterManager.EnterDungeon</c> の末尾(エンカウンターの作り直し)。
    /// 計測・リセット・マップ移動・「進行で自動リセット」の区切りは全部 <c>EnterDungeon</c> を通るので、
    /// <b>ボタン側に専用の解除を書かない。</b>
    /// </para>
    ///
    /// <para>
    /// <b>押し込み式にする理由。</b> <c>MeterSnapshotProvider.ResolveActiveEncounter</c> から
    /// 引きに行くと、あれを呼ぶのはメーター・スキル詳細・詳細・グラフのウィジェット4種の
    /// 更新だけなので、<b>1枚も開いていないと一度も走らず集計タブの「表示中」が残る。</b>
    /// </para>
    ///
    /// <para>
    /// プラグインが保持(<see cref="KeepOpen"/>)している間も何もしない。保持が全部外れても、すぐには戻さず次の出来事で戻す。
    /// 保持の数は、出来事のたびにロックを取らないよう、上の条件を通った後で見る。
    /// </para>
    /// </summary>
    public static void NotifyLiveEncounterEvent()
    {
        if (!CombatRuntimeSettings.ClearHistorySelectionOnNextEvent
            || AppState.OpenedHistoricalEncounter is null)
        {
            return;
        }

        lock (RequestGate)
        {
            if (_keepOpenCount > 0)
            {
                return;
            }
        }

        // 要求の番号は進めない。読み込み中の回があれば、それは読み終えてから開く(押した操作は残す)。
        ApplyLive();
    }

    /// <summary>
    /// 保存済みエンカウンターを新しい順に返す。
    ///
    /// <para>
    /// 戦闘データの無いエンカウンターはそもそも保存されない
    /// (<c>EncounterManager</c> が <c>HasStatsBeenRecorded</c> で弾く)ので、
    /// ここで空行を除く必要はない。
    /// </para>
    /// </summary>
    public static IReadOnlyList<EncounterHistoryEntry> GetEntries()
    {
        var summaries = DB.LoadEncounterSummaries();
        var entries = new List<EncounterHistoryEntry>(summaries.Count);
        foreach (var encounter in summaries)
        {
            entries.Add(new EncounterHistoryEntry(
                encounter.EncounterId,
                encounter.StartTime,
                encounter.SceneId,
                encounter.SceneName ?? string.Empty,
                encounter.IsWipe,
                // LoadEncounterSummaries が全行の ExData を復元済みなので追加の読み込みは無い。
                encounter.ExData.BenchmarkTime,
                encounter.ExData.DungeonDifficulty,
                encounter.ExData.ChannelLine));
        }

        return entries;
    }

    /// <summary>
    /// 履歴の1件を開く。**開いている間、全ウィジェットが新しいデータを表示しなくなる。**
    /// DB は裏のスレッドで読み(画面を止めない)、読めたら反映をパケットのスレッドへ積む。
    /// 読めなければ選択を変えない(結果で分かる)。返す Task は読み込みと反映の予約まで。
    /// </summary>
    public static Task<HistorySelectResult> SelectAsync(ulong encounterId)
    {
        return SelectAsync(encounterId, out _);
    }

    /// <summary>
    /// <see cref="SelectAsync(ulong)"/> と同じで、出した要求の番号も返す(取り下げに使う、<see cref="WithdrawSelection"/>)。
    /// アプリを閉じている間は要求を出さず、番号は 0。
    /// </summary>
    public static Task<HistorySelectResult> SelectAsync(ulong encounterId, out long request)
    {
        Task<HistorySelectResult> load;
        lock (RequestGate)
        {
            if (_isShutDown)
            {
                request = 0;
                return Task.FromResult(HistorySelectResult.Superseded);
            }

            var issued = ++_latestRequest;
            request = issued;
            load = Task.Run(() => LoadAndApply(encounterId, issued));
            PendingLoads.Add(load);
        }

        load.ContinueWith(
            finished =>
            {
                lock (RequestGate)
                {
                    PendingLoads.Remove(finished);
                }
            },
            TaskScheduler.Default);
        return load;
    }

    private static HistorySelectResult LoadAndApply(ulong encounterId, long request)
    {
        Encounter? encounter;
        try
        {
            encounter = DB.LoadEncounter(encounterId);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Encounter {EncounterId} could not be loaded from history", encounterId);
            SetLoadFailedIfLatest(request);
            return HistorySelectResult.Failed;
        }

        if (encounter is null)
        {
            Log.Warning("Encounter {EncounterId} could not be opened from history", encounterId);
            SetLoadFailedIfLatest(request);
            return HistorySelectResult.NotFound;
        }

        if (!IsLatestRequest(request))
        {
            return HistorySelectResult.Superseded;
        }

        MessageManager.RunOnPacketThread(() =>
        {
            // 待ち行列に並んでいる間に別の回かライブが選ばれていたら反映しない。
            if (!IsLatestRequest(request))
            {
                return;
            }

            AppState.OpenedHistoricalEncounter = encounter;
            ClearLoadFailed();
            PlayerRosterProjection.RebuildRoster();
            SelectionChanged?.Invoke();
        });
        return HistorySelectResult.Opened;
    }

    /// <summary>読めなかった。古い要求(後から別の回かライブが選ばれた)の失敗は印にしない。</summary>
    private static void SetLoadFailedIfLatest(long request)
    {
        lock (RequestGate)
        {
            if (_isShutDown || request != _latestRequest || _hasLoadFailed)
            {
                return;
            }

            _hasLoadFailed = true;
        }

        LoadFailedChanged?.Invoke();
    }

    private static void ClearLoadFailed()
    {
        lock (RequestGate)
        {
            if (!_hasLoadFailed)
            {
                return;
            }

            _hasLoadFailed = false;
        }

        LoadFailedChanged?.Invoke();
    }

    private static bool IsLatestRequest(long request)
    {
        lock (RequestGate)
        {
            return !_isShutDown && request == _latestRequest;
        }
    }

    /// <summary>
    /// 出した選択の要求(<see cref="SelectAsync(ulong, out long)"/>)を取り下げる。その要求がまだ最新なら番号だけ進め、
    /// 読み込み中・反映待ちの回を開かない(表示は変えない。<see cref="SelectLive"/> と違いライブへ戻さない)。
    /// 後から別の回かライブが選ばれていれば何もしない。反映した後なら、開いた回はそのまま。止めたプラグインの要求に使う。
    /// </summary>
    public static void WithdrawSelection(long request)
    {
        lock (RequestGate)
        {
            if (request != 0 && request == _latestRequest)
            {
                _latestRequest++;
            }
        }
    }

    /// <summary>現在の戦闘へ戻す(集計タブの操作)。読み込み中の選択は捨てる。反映はパケットのスレッドで。</summary>
    public static void SelectLive()
    {
        lock (RequestGate)
        {
            _latestRequest++;
        }

        MessageManager.RunOnPacketThread(ApplyLive);
    }

    /// <summary>
    /// <see cref="SelectLive"/> と同じで、戻し終えたら終わる Task を返す(プラグインの口)。戻しで例外が起きたら、ログに書いて Task をその例外で終える
    /// (呼んだスレッドで走るときも、例外は Task で返す)。キャプチャが止まっていれば呼んだスレッドで戻す。
    /// </summary>
    public static Task SelectLiveAsync()
    {
        lock (RequestGate)
        {
            _latestRequest++;
        }

        var completion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        MessageManager.RunOnPacketThread(() =>
        {
            try
            {
                ApplyLive();
                completion.SetResult();
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Returning to the live encounter failed");
                completion.SetException(ex);
            }
        });
        return completion.Task;
    }

    /// <summary>
    /// 設定「履歴表示を自動解除」の自動の戻し(<see cref="NotifyLiveEncounterEvent"/>)を、返した物を Dispose するまで止める(プラグインの口)。
    /// 呼ぶたびに別の物を返し、Dispose の2回目は何もしない。集計タブ・<see cref="SelectLive"/>・ログアウトの戻しは止めない。
    /// </summary>
    public static IDisposable KeepOpen()
    {
        lock (RequestGate)
        {
            _keepOpenCount++;
        }

        return new KeepOpenHandle();
    }

    private sealed class KeepOpenHandle : IDisposable
    {
        private int _isDisposed;

        public void Dispose()
        {
            if (Interlocked.Exchange(ref _isDisposed, 1) != 0)
            {
                return;
            }

            lock (RequestGate)
            {
                _keepOpenCount--;
            }
        }
    }

    /// <summary>
    /// 保存した回の要約の一覧(新しい順)を、選択を変えずに読む(プラグインの口)。読み込みは裏のスレッドで、閉じるときに終わりを待つ。
    /// 読めなければログに書いて <see cref="HistoryReadStatus.Failed"/>、アプリを閉じた後は <see cref="HistoryReadStatus.Unavailable"/>。
    /// </summary>
    public static Task<HistoryRead<IReadOnlyList<Encounter>>> LoadSummariesDetachedAsync()
    {
        return RunDetached(
            () =>
            {
                try
                {
                    return new HistoryRead<IReadOnlyList<Encounter>>(HistoryReadStatus.Succeeded, DB.LoadEncounterSummaries());
                }
                catch (Exception ex)
                {
                    Log.Error(ex, "Encounter history could not be listed for a plugin");
                    return new HistoryRead<IReadOnlyList<Encounter>>(HistoryReadStatus.Failed, null);
                }
            },
            new HistoryRead<IReadOnlyList<Encounter>>(HistoryReadStatus.Unavailable, null));
    }

    /// <summary>
    /// 保存した回を1件、選択を変えずに読む(プラグインの口)。読み込みは裏のスレッドで、閉じるときに終わりを待つ。
    /// その番号の回が無ければ <see cref="HistoryReadStatus.NotFound"/>、読めなければログに書いて <see cref="HistoryReadStatus.Failed"/>。
    /// </summary>
    public static Task<HistoryRead<Encounter>> LoadDetachedAsync(ulong encounterId)
    {
        return RunDetached(
            () =>
            {
                try
                {
                    return DB.LoadEncounter(encounterId) is { } encounter
                        ? new HistoryRead<Encounter>(HistoryReadStatus.Succeeded, encounter)
                        : new HistoryRead<Encounter>(HistoryReadStatus.NotFound, null);
                }
                catch (Exception ex)
                {
                    Log.Error(ex, "Encounter {EncounterId} could not be loaded from history for a plugin", encounterId);
                    return new HistoryRead<Encounter>(HistoryReadStatus.Failed, null);
                }
            },
            new HistoryRead<Encounter>(HistoryReadStatus.Unavailable, null));
    }

    /// <summary>
    /// 保存した回のプラグインの値を1キー置き換える(プラグインの口、長さ0は消す)。書き込みは裏のスレッドで、閉じるときに終わりを待つ。
    /// 書けたら、本体が持っている同じ回の写し(開いている履歴の回・保持している前の回)にも入れる。プラグインが前に読んだ回の写しには入らない。
    /// 書けなければログに書いて <see cref="SavedPluginDataStatus.Failed"/>。
    /// </summary>
    public static Task<SavedPluginDataStatus> SetSavedPluginDataAsync(
        ulong encounterId,
        string pluginKey,
        string dataKey,
        byte[] data,
        int maxKeys,
        int maxTotalBytes)
    {
        ArgumentNullException.ThrowIfNull(pluginKey);
        ArgumentNullException.ThrowIfNull(dataKey);
        ArgumentNullException.ThrowIfNull(data);

        return RunDetached(
            () =>
            {
                SavedPluginDataResult result;
                try
                {
                    result = DB.ReplaceSavedPluginData(encounterId, pluginKey, dataKey, data, maxKeys, maxTotalBytes);
                }
                catch (Exception ex)
                {
                    Log.Error(ex, "Plugin data {PluginKey}/{DataKey} could not be written to encounter {EncounterId}", pluginKey, dataKey, encounterId);
                    return SavedPluginDataStatus.Failed;
                }

                if (result == SavedPluginDataResult.Stored)
                {
                    ReplaceHeldSavedPluginData(encounterId, pluginKey, dataKey, data);
                }

                return result switch
                {
                    SavedPluginDataResult.Stored => SavedPluginDataStatus.Stored,
                    SavedPluginDataResult.NotFound => SavedPluginDataStatus.NotFound,
                    SavedPluginDataResult.TooManyKeys => SavedPluginDataStatus.TooManyKeys,
                    SavedPluginDataResult.TooLarge => SavedPluginDataStatus.TooLarge,
                    _ => throw new ArgumentOutOfRangeException(nameof(result), result, null)
                };
            },
            SavedPluginDataStatus.Unavailable);
    }

    /// <summary>本体が持っている、保存した同じ回の写し(開いている履歴の回・保持している前の回)へ、DB に書いた値を入れる。</summary>
    private static void ReplaceHeldSavedPluginData(ulong encounterId, string pluginKey, string dataKey, byte[] data)
    {
        Encounter?[] held = [AppState.OpenedHistoricalEncounter, EncounterManager.Current?.PastEncounterForMeter];
        foreach (var encounter in held.Distinct())
        {
            if (encounter is { IsPersisted: true } && encounter.EncounterId == encounterId)
            {
                encounter.PluginData.ReplaceSaved(pluginKey, dataKey, data);
            }
        }
    }

    /// <summary>選択を変えない読み書きを裏のスレッドで走らせ、閉じるときに終わりを待つ一覧へ入れる。アプリを閉じた後は走らせず <paramref name="unavailable"/>。</summary>
    private static Task<T> RunDetached<T>(Func<T> work, T unavailable)
    {
        Task<T> task;
        lock (RequestGate)
        {
            if (_isShutDown)
            {
                return Task.FromResult(unavailable);
            }

            task = Task.Run(work);
            PendingLoads.Add(task);
        }

        task.ContinueWith(
            finished =>
            {
                lock (RequestGate)
                {
                    PendingLoads.Remove(finished);
                }
            },
            TaskScheduler.Default);
        return task;
    }

    /// <summary>ログアウト(起動直後へ戻す)。読み込み中の選択は捨て、開いていれば現在の戦闘へ戻す。パケットのスレッドから呼ぶ。</summary>
    internal static void ResetSelectionForLogout()
    {
        lock (RequestGate)
        {
            _latestRequest++;
        }

        if (AppState.OpenedHistoricalEncounter is not null)
        {
            ApplyLive();
        }
    }

    private static void ApplyLive()
    {
        AppState.OpenedHistoricalEncounter = null;
        ClearLoadFailed();
        PlayerRosterProjection.RebuildRoster();
        SelectionChanged?.Invoke();
    }

    /// <summary>
    /// アプリを閉じる。新しい読み込みと読み書きを受けなくし、走っているものの終わりを待つ(読み終えた結果は反映しない)。
    /// DB を閉じる前に呼ぶ。
    /// </summary>
    public static void ShutdownLoads()
    {
        Task[] pending;
        lock (RequestGate)
        {
            _isShutDown = true;
            pending = PendingLoads.ToArray();
        }

        Task.WaitAll(pending);
    }
}
