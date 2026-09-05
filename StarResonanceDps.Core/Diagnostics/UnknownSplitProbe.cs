using System.Globalization;

namespace StarResonanceDps.Core.Diagnostics;

/// <summary>
/// 畳み先が絞り込めなかったバフを残す常設の計測。
///
/// <para>
/// 発生源の畳み込みは「同じバフの生きている実体が複数あり、それぞれの親が違う」ときに
/// 決め切れなくなる。そこで片方を選ぶと表示は正常に見えるのに中身が当て推量になり、
/// 誤りが見えなくなる。<see cref="CombatRuntime.SkillSourceResolver"/> は畳まずに
/// 生のバフIDで出し、その回をここへ記録する。
/// </para>
///
/// <para>
/// 出てきたIDは「実体を見ても決まらないバフ」なので、
/// <c>SkillSourceResolver.TableResolvedBuffIds</c> へ足す候補になる。
/// 既知の <c>20301</c> / <c>55355</c> はテーブル確定済みでここには出ない。
/// </para>
///
/// <para>
/// <b>これは調査用の一時計測ではない。</b> 常設で、プローブ撤去の対象外。
/// リリース前に掃除する。
/// </para>
///
/// <para>出力先は AppContext.BaseDirectory/Logs/<b>Resident</b>/UnknownSplitProbe_(起動時刻).txt。</para>
/// </summary>
public static class UnknownSplitProbe
{
    private const string LogNamePrefix = "UnknownSplitProbe";

    /// <summary>同じバフを何度も書かない。1回の起動につき最初の観測だけ残す。</summary>
    private const int MaxRecordsPerBuff = 3;

    private static readonly object Sync = new();
    private static readonly Dictionary<int, int> SeenCounts = [];

    /// <summary>畳み先が絞り込めなかった回。</summary>
    public static void CaptureSplit(
        int buffId,
        long casterUuid,
        IReadOnlyList<Services.BuffSourceIndex.LiveBuff> candidates)
    {
        lock (Sync)
        {
            SeenCounts.TryGetValue(buffId, out var count);
            if (count >= MaxRecordsPerBuff)
            {
                SeenCounts[buffId] = count + 1;
                return;
            }

            SeenCounts[buffId] = count + 1;
        }

        var detail = string.Join(
            " | ",
            candidates.Select(candidate => string.Create(
                CultureInfo.InvariantCulture,
                $"buffUuid={candidate.BuffUuid} on={candidate.HolderUuid} ftype={candidate.FightSourceType} src={candidate.SourceConfigId} 付与から={(DateTime.Now - candidate.AddTime).TotalMilliseconds:F0}ms")));

        Write(string.Create(
            CultureInfo.InvariantCulture,
            $"SPLIT baseId={buffId} 術者={casterUuid} 実体数={candidates.Count}  {detail}"));
    }

    private static void Write(string message)
    {
        try
        {
            var line = DateTimeOffset.Now.ToString("HH:mm:ss.fff", CultureInfo.InvariantCulture)
                + "  " + message;
            var directory = DiagnosticSession.ResidentLogDirectory;
            // アプリ起動ごとに別ファイル。追記だとどの回の記録か切り分けられない。
            var path = Path.Combine(directory, $"{LogNamePrefix}_{DiagnosticSession.Stamp}.txt");

            lock (Sync)
            {
                Directory.CreateDirectory(directory);
                DiagnosticSession.EnsureHeader(path);
                File.AppendAllText(path, line + Environment.NewLine, System.Text.Encoding.UTF8);
            }
        }
        catch
        {
            // 診断のみ。本来の処理へは伝播させない。
        }
    }
}
