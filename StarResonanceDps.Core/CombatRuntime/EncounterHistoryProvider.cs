using Serilog;
using StarResonanceDps.Core.CombatRuntime.Database;

namespace StarResonanceDps.Core.CombatRuntime;

/// <summary>保存済みエンカウンター1件ぶんの見出し。一覧に出すぶんだけ持つ。</summary>
/// <param name="SceneName">
/// 記録した時点の <c>GetSceneName</c> の出力。**言語も内部ID注記も当時のまま固定**なので、
/// 表示には使わない。<paramref name="SceneId"/> から引き直せなかったときの受け皿。
/// </param>
/// <param name="SceneSubName">
/// 記録した時点の <c>"Phase {n}"</c>。**英語のまま固定**なので表示には使わない。
/// <paramref name="PhaseNumber"/> が 0 のときだけの受け皿。
/// </param>
/// <param name="PhaseNumber">
/// フェーズ区切り(NewObjective)で分割された何本目か。分割していなければ 0。
/// 出所は <c>EncounterExData.EncounterPhase</c> で、<c>SceneSubName</c> の2つの書き手が
/// <b>必ず対で書く</b>ので同じ数字。文字列をパースせずにここから組み立てる。
/// </param>
/// <param name="BenchmarkSeconds">
/// 3分計測の記録なら計測秒数、そうでなければ 0。
/// 出所は <c>EncounterExData.BenchmarkTime</c> で、書き込みは <c>EnterDungeon</c> の
/// <c>if (AppState.IsBenchmarkMode)</c> の中だけ。<c>TryStopBenchmark</c> は
/// <c>IsBenchmarkMode = false</c> を先に立ててから次のエンカウンターを作るので、
/// <b>計測本体の1件にしか付かない</b>(実測: DB13件のうち計測の1件だけ 180、残りは 0)。
/// </param>
public sealed record EncounterHistoryEntry(
    ulong EncounterId,
    DateTime StartTime,
    uint SceneId,
    string SceneName,
    string? SceneSubName,
    bool IsWipe,
    int PhaseNumber,
    int BenchmarkSeconds);

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
                string.IsNullOrWhiteSpace(encounter.SceneSubName) ? null : encounter.SceneSubName,
                encounter.IsWipe,
                // LoadEncounterSummaries が全行の ExData を復元済みなので追加の読み込みは無い。
                encounter.ExData.EncounterPhase,
                encounter.ExData.BenchmarkTime));
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
        return true;
    }

    /// <summary>現在の戦闘へ戻す。</summary>
    public static void SelectLive()
    {
        AppState.OpenedHistoricalEncounter = null;
        PlayerRosterProjection.RebuildRoster();
    }
}
