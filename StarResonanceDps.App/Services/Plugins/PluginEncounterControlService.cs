// This file is part of StarResonanceDps. See LICENSING.md for the license, additional permissions and additional terms.

using StarResonanceDps.Core.CombatRuntime;
using StarResonanceDps.PluginSdk;

namespace StarResonanceDps.App.Services;

/// <summary>
/// プラグイン1つに渡す、回の区切りと戦闘の時計を変える口。要求は本体のスレッド(<see cref="PluginEncounterRuleRegistry.RunOnHostThread"/>)で走らせ、
/// <c>Task</c> は続きを非同期にして完了する(プラグインの続きを本体のスレッドで動かさない)。
/// 止めたら(<see cref="Release"/>)規則を外し、まだ走っていない要求を <c>Unavailable</c> で終える。
/// </summary>
internal sealed class PluginEncounterControlService : IPluginEncounterControlService
{
    private const int MaxConditionSeconds = 86400;

    private readonly PluginCallGuard _guard;
    private readonly string _pluginKey;
    private readonly PluginInfo _plugin;
    private readonly object _gate = new();

    /// <summary>まだ走っていない要求と、止めたときに <c>Unavailable</c> で終える処理。</summary>
    private readonly Dictionary<object, Action> _pendingRequests = [];

    private PluginEncounterRules? _rules;
    private IReadOnlyList<PluginEncounterRuleItem> _lastPending = Array.Empty<PluginEncounterRuleItem>();
    private PluginEncounterOverrides _lastApplied;
    private bool _isReleased;

    public PluginEncounterControlService(PluginCallGuard guard, string pluginKey, PluginInfo plugin)
    {
        _guard = guard ?? throw new ArgumentNullException(nameof(guard));
        _pluginKey = pluginKey ?? throw new ArgumentNullException(nameof(pluginKey));
        _plugin = plugin ?? throw new ArgumentNullException(nameof(plugin));
        _lastApplied = PluginEncounterRuleRegistry.Instance.AppliedOverrides;
        PluginEncounterRuleRegistry.Instance.Changed += Registry_Changed;
    }

    public event EventHandler? RulesChanged;

    public PluginEncounterRules? Rules
    {
        get
        {
            lock (_gate)
            {
                return _rules;
            }
        }
    }

    public IReadOnlyList<PluginEncounterRuleItem> PendingItems => PluginEncounterRuleRegistry.Instance.GetPendingItems(_pluginKey);

    public PluginEncounterOverrides AppliedOverrides => PluginEncounterRuleRegistry.Instance.AppliedOverrides;

    public Task<PluginEncounterSplitResult> SplitAsync(bool keepPreviousInMeter = false, CancellationToken cancellationToken = default)
    {
        return Run(
            () =>
            {
                var split = EncounterManager.SplitOnRequest(keepPreviousInMeter, out var closedHadRecords);
                return new PluginEncounterSplitResult(
                    split ? PluginEncounterSplitOutcome.Split : PluginEncounterSplitOutcome.SkippedBenchmark,
                    closedHadRecords);
            },
            new PluginEncounterSplitResult(PluginEncounterSplitOutcome.Unavailable, false),
            cancellationToken);
    }

    public Task<PluginEncounterRulesResult> SetRulesAsync(PluginEncounterRules? rules, CancellationToken cancellationToken = default)
    {
        if (rules != null)
        {
            Validate(rules);
        }

        return Run(
            () =>
            {
                lock (_gate)
                {
                    _rules = rules;
                }

                var pending = PluginEncounterRuleRegistry.Instance.Set(_pluginKey, _plugin, rules);
                return new PluginEncounterRulesResult(
                    pending.Count == 0 ? PluginEncounterRulesStatus.Applied : PluginEncounterRulesStatus.Pending,
                    pending);
            },
            new PluginEncounterRulesResult(PluginEncounterRulesStatus.Unavailable, Array.Empty<PluginEncounterRuleItem>()),
            cancellationToken);
    }

    /// <summary>
    /// 止める・終える・アンインストールで呼ぶ。規則を外し、まだ走っていない要求を <c>Unavailable</c> で終える。
    /// </summary>
    public void Release()
    {
        Action[] unavailable;
        bool hadRules;
        lock (_gate)
        {
            if (_isReleased)
            {
                return;
            }

            _isReleased = true;
            unavailable = _pendingRequests.Values.ToArray();
            _pendingRequests.Clear();
            hadRules = _rules != null;
            _rules = null;
        }

        PluginEncounterRuleRegistry.Instance.Changed -= Registry_Changed;
        foreach (var complete in unavailable)
        {
            complete();
        }

        if (hadRules)
        {
            PluginEncounterRuleRegistry.RunOnHostThread(() => PluginEncounterRuleRegistry.Instance.Set(_pluginKey, _plugin, null));
        }
    }

    /// <summary>範囲と、ダンジョンの時計と合わない組を確かめる(呼んだスレッドで投げる)。</summary>
    private static void Validate(PluginEncounterRules rules)
    {
        if (rules.IdleSplitSeconds is < 0 or > MaxConditionSeconds)
        {
            throw new ArgumentOutOfRangeException(nameof(rules), rules.IdleSplitSeconds, "IdleSplitSeconds must be 0 or from 1 to 86400.");
        }

        if (rules.IntervalSplitSeconds is < 0 or > MaxConditionSeconds)
        {
            throw new ArgumentOutOfRangeException(nameof(rules), rules.IntervalSplitSeconds, "IntervalSplitSeconds must be 0 or from 1 to 86400.");
        }

        foreach (var mode in new[] { rules.DungeonStartSplit, rules.BossRoomSplit, rules.WipeSplit })
        {
            if (!Enum.IsDefined(mode))
            {
                throw new ArgumentOutOfRangeException(nameof(rules), mode, "An automatic split value is not defined.");
            }
        }

        if (!Enum.IsDefined(rules.DungeonClock))
        {
            throw new ArgumentOutOfRangeException(nameof(rules), rules.DungeonClock, "The dungeon clock value is not defined.");
        }

        if (rules.DungeonClock != PluginDungeonClock.None
            && (rules.DungeonStartSplit == PluginAutoSplitMode.DoNotSplit
                || rules.BossRoomSplit == PluginAutoSplitMode.Split
                || rules.WipeSplit == PluginAutoSplitMode.Split))
        {
            throw new ArgumentException(
                "A dungeon clock splits at the start of a dungeon and does not split at boss rooms or wipes inside it.",
                nameof(rules));
        }
    }

    /// <summary>要求を本体のスレッドで走らせる。止めた後・走る前に止められたら <paramref name="unavailable"/>、取り消されたら取り消しで終える。</summary>
    private Task<T> Run<T>(Func<T> work, T unavailable, CancellationToken cancellationToken)
    {
        if (cancellationToken.IsCancellationRequested)
        {
            return Task.FromCanceled<T>(cancellationToken);
        }

        var completion = new TaskCompletionSource<T>(TaskCreationOptions.RunContinuationsAsynchronously);
        var key = new object();
        lock (_gate)
        {
            if (_isReleased)
            {
                return Task.FromResult(unavailable);
            }

            _pendingRequests[key] = () => completion.TrySetResult(unavailable);
        }

        PluginEncounterRuleRegistry.RunOnHostThread(() =>
        {
            lock (_gate)
            {
                // 止めたときに Unavailable で終えてある。
                if (!_pendingRequests.Remove(key))
                {
                    return;
                }
            }

            if (cancellationToken.IsCancellationRequested)
            {
                completion.TrySetCanceled(cancellationToken);
                return;
            }

            try
            {
                completion.TrySetResult(work());
            }
            catch (Exception ex)
            {
                // プラグインにも失敗を見せ、本体のスレッドの例外の扱い(ログ)にも渡す。
                completion.TrySetException(ex);
                throw;
            }
        });

        return completion.Task;
    }

    /// <summary>合わせた結果が変わったら、このプラグインの保留か当てている上書きが変わったときだけ知らせる(画面のスレッド)。</summary>
    private void Registry_Changed(object? sender, EventArgs e)
    {
        var pending = PendingItems;
        var applied = AppliedOverrides;
        lock (_gate)
        {
            if (_isReleased || (pending.SequenceEqual(_lastPending) && applied == _lastApplied))
            {
                return;
            }

            _lastPending = pending;
            _lastApplied = applied;
        }

        _guard.TryInvoke("Encounter rules notification", () => RulesChanged?.Invoke(this, EventArgs.Empty));
    }
}
