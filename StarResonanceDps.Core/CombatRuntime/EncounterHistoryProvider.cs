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
public sealed record EncounterHistoryEntry(
    ulong EncounterId,
    DateTime StartTime,
    uint SceneId,
    string SceneName,
    bool IsWipe,
    int BenchmarkSeconds,
    int DungeonDifficulty);

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
/// </summary>
public static class EncounterHistoryProvider
{
    private static readonly object RequestGate = new();

    /// <summary>最新の選択の要求の番号。集計タブの操作(回を選ぶ・ライブへ戻す)とログアウトで進める。</summary>
    private static long _latestRequest;

    /// <summary>アプリを閉じている(<see cref="ShutdownLoads"/> の後)。新しい読み込みを受けず、反映もしない。</summary>
    private static bool _isShutDown;

    /// <summary>走っている読み込み。閉じるときに終わりを待つ(閉じた接続に当てない)。</summary>
    private static readonly List<Task> PendingLoads = [];

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
    /// </summary>
    public static void NotifyLiveEncounterEvent()
    {
        if (!CombatRuntimeSettings.ClearHistorySelectionOnNextEvent
            || AppState.OpenedHistoricalEncounter is null)
        {
            return;
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
                encounter.ExData.DungeonDifficulty));
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
        Task<HistorySelectResult> load;
        lock (RequestGate)
        {
            if (_isShutDown)
            {
                return Task.FromResult(HistorySelectResult.Superseded);
            }

            var request = ++_latestRequest;
            load = Task.Run(() => LoadAndApply(encounterId, request));
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
            return HistorySelectResult.Failed;
        }

        if (encounter is null)
        {
            Log.Warning("Encounter {EncounterId} could not be opened from history", encounterId);
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
            PlayerRosterProjection.RebuildRoster();
            SelectionChanged?.Invoke();
        });
        return HistorySelectResult.Opened;
    }

    private static bool IsLatestRequest(long request)
    {
        lock (RequestGate)
        {
            return !_isShutDown && request == _latestRequest;
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
        PlayerRosterProjection.RebuildRoster();
        SelectionChanged?.Invoke();
    }

    /// <summary>
    /// アプリを閉じる。新しい読み込みを受けなくし、走っている読み込みの終わりを待つ(読み終えた結果は反映しない)。
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
