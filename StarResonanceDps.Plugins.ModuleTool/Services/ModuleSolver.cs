using StarResonanceDps.Plugins.ModuleTool.Models;

namespace StarResonanceDps.Plugins.ModuleTool.Services;

/// <summary>
/// 枠の数のモジュールの組から、優先する効果の条件を満たす組の戦闘力(ゲームの式)の上位を、全部の組の中から厳密に探す(分枝限定)。
///
/// <para>
/// 効果とリンク数がまったく同じモジュールは入れ替えても戦闘力が変わらないので、1つのまとまりにして組を1つに畳む。
/// 枝の上限は、残りのまとまりから選べるモジュールで効果ごと・総和ごとに取れる最大のリンク数を足した戦闘力
/// (効果ごとに別々に最大を取るので実際の組より大きいか同じ)。上限が今の上位の最後に届かない枝と、
/// 優先する効果の条件に届かない枝を捨てる。
/// </para>
///
/// <para>
/// 最初に選ぶまとまりごとに並列に探す。上位の入れ替えはロックの中で行い、戦闘力が同じ組は表示するモジュールの uuid の並びで順を決めるので、
/// 何度回しても同じ結果になる。
/// </para>
/// </summary>
internal sealed class ModuleSolver
{
    private readonly ModuleScorer _scorer;
    private readonly int _slotCount;
    private readonly int _resultCount;
    private readonly List<ModuleGroup> _groups;
    private readonly int _effectCount;

    // まとまりの添字 g 以降から r 個選ぶときの、効果ごとの最大のリンク数([g][r][効果])と総和の最大([g][r])、選べる数([g])。
    private readonly int[][][] _maxEffectLinks;
    private readonly int[][] _maxTotalLinks;
    private readonly int[] _availableCounts;

    private readonly object _resultLock = new();
    private readonly List<RankedCombination> _results = [];
    private int _threshold = int.MinValue;

    public ModuleSolver(
        ModuleScorer scorer,
        IReadOnlyList<ModuleCandidate> candidates,
        IReadOnlySet<long> equippedUuids,
        int slotCount,
        int resultCount)
    {
        _scorer = scorer;
        _effectCount = scorer.Effects.Count;
        _slotCount = Math.Min(slotCount, candidates.Count);
        _resultCount = resultCount;
        _groups = CreateGroups(candidates, equippedUuids);

        var groupCount = _groups.Count;
        _maxEffectLinks = new int[groupCount + 1][][];
        _maxTotalLinks = new int[groupCount + 1][];
        _availableCounts = new int[groupCount + 1];
        var topEffectLinks = Enumerable.Range(0, _effectCount).Select(_ => new List<int>()).ToArray();
        var topTotalLinks = new List<int>();
        for (var g = groupCount; g >= 0; g--)
        {
            if (g < groupCount)
            {
                var group = _groups[g];
                var copies = Math.Min(group.Members.Count, _slotCount);
                foreach (var (effectIndex, links) in group.Effects)
                {
                    InsertTop(topEffectLinks[effectIndex], links, copies, _slotCount);
                }

                InsertTop(topTotalLinks, group.TotalLinks, copies, _slotCount);
                _availableCounts[g] = _availableCounts[g + 1] + group.Members.Count;
            }

            _maxEffectLinks[g] = new int[_slotCount + 1][];
            _maxTotalLinks[g] = new int[_slotCount + 1];
            for (var r = 0; r <= _slotCount; r++)
            {
                _maxEffectLinks[g][r] = new int[_effectCount];
                for (var effectIndex = 0; effectIndex < _effectCount; effectIndex++)
                {
                    _maxEffectLinks[g][r][effectIndex] = topEffectLinks[effectIndex].Take(r).Sum();
                }

                _maxTotalLinks[g][r] = topTotalLinks.Take(r).Sum();
            }
        }
    }

    /// <summary>組むモジュールの数(枠の数と候補の数の小さいほう)。</summary>
    public int CombinationSize => _slotCount;

    /// <summary>上位を探す。見つからなければ空。中断されたら <see cref="OperationCanceledException"/>。</summary>
    public IReadOnlyList<ModuleCombination> Solve(IProgress<double>? progress, CancellationToken cancellationToken)
    {
        if (_slotCount == 0)
        {
            return [];
        }

        // 最初に選ぶまとまり(と選ぶ数)ごとに分けて並列に探す。上位が早く埋まるよう、リンクの多いまとまりから。
        var starts = new List<(int Group, int Count)>();
        for (var g = 0; g < _groups.Count; g++)
        {
            for (var count = Math.Min(_groups[g].Members.Count, _slotCount); count >= 1; count--)
            {
                starts.Add((g, count));
            }
        }

        var finished = 0;
        var options = new ParallelOptions { CancellationToken = cancellationToken };
        Parallel.ForEach(starts, options, start =>
        {
            var links = new int[_effectCount];
            var bestLinks = new int[_effectCount];
            var counts = new int[_groups.Count];
            var group = _groups[start.Group];
            Add(links, group, start.Count);
            counts[start.Group] = start.Count;
            try
            {
                Search(start.Group + 1, _slotCount - start.Count, links, group.TotalLinks * start.Count, counts, bestLinks, cancellationToken);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                // 中断は Parallel.ForEach が ParallelOptions の取り消しで知らせる。
                return;
            }

            progress?.Report((double)Interlocked.Increment(ref finished) / starts.Count);
        });

        lock (_resultLock)
        {
            return _results
                .Select(result => new ModuleCombination(result.FightValue, result.TotalLinks, result.Modules))
                .ToArray();
        }
    }

    private void Search(
        int g,
        int remaining,
        int[] links,
        int totalLinks,
        int[] counts,
        int[] bestLinks,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (remaining == 0)
        {
            OfferResult(links, totalLinks, counts);
            return;
        }

        if (g >= _groups.Count || _availableCounts[g] < remaining || !CanReach(g, remaining, links, totalLinks, bestLinks))
        {
            return;
        }

        var group = _groups[g];
        for (var count = Math.Min(group.Members.Count, remaining); count >= 0; count--)
        {
            Add(links, group, count);
            counts[g] = count;
            Search(g + 1, remaining - count, links, totalLinks + group.TotalLinks * count, counts, bestLinks, cancellationToken);
            counts[g] = 0;
            Add(links, group, -count);
        }
    }

    /// <summary>残りから取れる最大のリンク数で、条件に届き、戦闘力が今の上位の最後に届くか。<paramref name="bestLinks"/> は使い回しの作業用。</summary>
    private bool CanReach(int g, int remaining, int[] links, int totalLinks, int[] bestLinks)
    {
        var maxEffectLinks = _maxEffectLinks[g][remaining];
        for (var effectIndex = 0; effectIndex < _effectCount; effectIndex++)
        {
            bestLinks[effectIndex] = links[effectIndex] + maxEffectLinks[effectIndex];
        }

        if (!_scorer.MeetsRequirements(bestLinks))
        {
            return false;
        }

        return _scorer.GetFightValue(bestLinks, totalLinks + _maxTotalLinks[g][remaining]) >= Volatile.Read(ref _threshold);
    }

    private void OfferResult(int[] links, int totalLinks, int[] counts)
    {
        if (!_scorer.MeetsRequirements(links))
        {
            return;
        }

        var fightValue = _scorer.GetFightValue(links, totalLinks);
        if (fightValue < Volatile.Read(ref _threshold))
        {
            return;
        }

        var modules = new List<ModuleCandidate>(_slotCount);
        for (var g = 0; g < counts.Length; g++)
        {
            if (counts[g] > 0)
            {
                modules.AddRange(_groups[g].Members.Take(counts[g]));
            }
        }

        var result = new RankedCombination(
            fightValue,
            totalLinks,
            modules.OrderBy(module => module.Uuid).ToArray());

        lock (_resultLock)
        {
            var index = _results.BinarySearch(result, RankedCombination.Order);
            _results.Insert(index < 0 ? ~index : index, result);
            if (_results.Count > _resultCount)
            {
                _results.RemoveAt(_results.Count - 1);
            }

            if (_results.Count == _resultCount)
            {
                Volatile.Write(ref _threshold, _results[^1].FightValue);
            }
        }
    }

    private static void Add(int[] links, ModuleGroup group, int count)
    {
        if (count == 0)
        {
            return;
        }

        foreach (var (effectIndex, effectLinks) in group.Effects)
        {
            links[effectIndex] += effectLinks * count;
        }
    }

    /// <summary>降順の並びに value を copies 個入れ、上から limit 個だけ残す。</summary>
    private static void InsertTop(List<int> top, int value, int copies, int limit)
    {
        for (var copy = 0; copy < copies; copy++)
        {
            var index = top.FindIndex(existing => existing < value);
            if (index < 0)
            {
                if (top.Count >= limit)
                {
                    return;
                }

                top.Add(value);
            }
            else
            {
                top.Insert(index, value);
                if (top.Count > limit)
                {
                    top.RemoveAt(top.Count - 1);
                }
            }
        }
    }

    /// <summary>効果とリンク数が同じモジュールのまとまり。中は装着中を先、次に uuid の小さい順(表示に使う代表の順)。</summary>
    private static List<ModuleGroup> CreateGroups(IReadOnlyList<ModuleCandidate> candidates, IReadOnlySet<long> equippedUuids)
    {
        return candidates
            .GroupBy(candidate => string.Join(
                ",",
                candidate.Effects
                    .OrderBy(effect => effect.EffectIndex)
                    .Select(effect => $"{effect.EffectIndex}:{effect.Links}")))
            .Select(group =>
            {
                var members = group
                    .OrderBy(candidate => equippedUuids.Contains(candidate.Uuid) ? 0 : 1)
                    .ThenBy(candidate => candidate.Uuid)
                    .ToArray();
                var effects = members[0].Effects
                    .GroupBy(effect => effect.EffectIndex)
                    .Select(effect => (effect.Key, effect.Sum(part => part.Links)))
                    .ToArray();
                return new ModuleGroup(group.Key, effects, members[0].TotalLinks, members);
            })
            .OrderByDescending(group => group.TotalLinks)
            .ThenBy(group => group.Key, StringComparer.Ordinal)
            .ToList();
    }

    private sealed record ModuleGroup(
        string Key,
        IReadOnlyList<(int EffectIndex, int Links)> Effects,
        int TotalLinks,
        IReadOnlyList<ModuleCandidate> Members);

    private sealed record RankedCombination(int FightValue, int TotalLinks, IReadOnlyList<ModuleCandidate> Modules)
    {
        /// <summary>戦闘力の高い順。同じなら uuid の並びの小さい順。</summary>
        public static readonly IComparer<RankedCombination> Order = Comparer<RankedCombination>.Create((left, right) =>
        {
            var compare = right.FightValue.CompareTo(left.FightValue);
            if (compare != 0)
            {
                return compare;
            }

            for (var index = 0; index < Math.Min(left.Modules.Count, right.Modules.Count); index++)
            {
                compare = left.Modules[index].Uuid.CompareTo(right.Modules[index].Uuid);
                if (compare != 0)
                {
                    return compare;
                }
            }

            return left.Modules.Count.CompareTo(right.Modules.Count);
        });
    }
}
