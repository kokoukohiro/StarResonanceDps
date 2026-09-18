using Serilog;
using StarResonanceDps.Core.CombatRuntime.Database;

namespace StarResonanceDps.Core.CombatRuntime;

/// <summary>保存済みエンカウンター1件ぶんの見出し。一覧に出すぶんだけ持つ。</summary>
/// <param name="SceneName">
/// 記録した時点の <c>GetSceneName</c> の出力。**言語も内部ID注記も当時のまま固定**なので、
/// 表示には使わない。<paramref name="SceneId"/> から引き直せなかったときの受け皿。
/// </param>
/// <param name="BenchmarkSeconds">
/// 3分計測の記録なら計測秒数、そうでなければ 0。
/// 出所は <c>EncounterExData.BenchmarkTime</c> で、書き込みは <c>EnterDungeon</c> の
/// <c>if (AppState.IsBenchmarkMode)</c> の中だけ。<c>TryStopBenchmark</c> は
/// <c>IsBenchmarkMode = false</c> を先に立ててから次のエンカウンターを作るので、
/// <b>計測本体の1件にしか付かない。</b>
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

/// <summary>
/// 集計タブが読む、保存済みエンカウンターの一覧と選択。
///
/// <para>
/// 選択の実体は <see cref="AppState.OpenedHistoricalEncounter"/> ただ1つ。
/// <c>MeterSnapshotProvider.ResolveActiveEncounter</c> が先頭でこれを見るので、
/// <b>ここへ入れるだけでメーター・スキル詳細・グラフ・集計のすべてが追従する</b>
/// (入口6か所すべてが同じ関数を通る)。ウィジェットごとの切り替えは持たない。
/// </para>
/// </summary>
public static class EncounterHistoryProvider
{
    /// <summary>いま履歴を開いているならその ID。ライブを見ているなら <c>null</c>。</summary>
    public static ulong? SelectedEncounterId => AppState.OpenedHistoricalEncounter?.EncounterId;

    /// <summary>
    /// 選択が変わったときに上がる。<b>自動でライブへ戻したときも上がる</b>ので、
    /// 一覧を出している側はこれを購読して「表示中」を出し直すこと。
    /// パケット処理スレッドから来ることがあるので、UI へは渡し直す。
    /// </summary>
    public static event Action? SelectionChanged;

    /// <summary>
    /// ライブ側でイベントが起きたので履歴表示を解除する。
    /// **設定が OFF か、履歴を開いていなければ何もしない。**
    ///
    /// <para>
    /// 呼ぶのは <c>Encounter.AddDamage</c> / <c>AddHealing</c>(戦闘)と
    /// <c>EncounterManager.EnterDungeon</c> の末尾(エンカウンターの作り直し)。
    /// 3分計測・リセット・マップ移動・フェーズ分割は全部 <c>EnterDungeon</c> を通るので、
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

        SelectLive();
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
    /// 読めなければ選択を変えずに false を返す(ライブのまま)。
    /// </summary>
    public static bool TrySelect(ulong encounterId)
    {
        var encounter = DB.LoadEncounter(encounterId);
        if (encounter is null)
        {
            Log.Warning("Encounter {EncounterId} could not be opened from history", encounterId);
            return false;
        }

        AppState.OpenedHistoricalEncounter = encounter;
        PlayerRosterProjection.RebuildRoster();
        SelectionChanged?.Invoke();
        return true;
    }

    /// <summary>現在の戦闘へ戻す。</summary>
    public static void SelectLive()
    {
        AppState.OpenedHistoricalEncounter = null;
        PlayerRosterProjection.RebuildRoster();
        SelectionChanged?.Invoke();
    }
}
