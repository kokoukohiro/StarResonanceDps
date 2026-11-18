// This file is part of StarResonanceDps. See LICENSING.md for the license, additional permissions and additional terms.

using System.Windows;
using StarResonanceDps.Core.CombatRuntime;
using StarResonanceDps.PluginSdk;

namespace StarResonanceDps.App.Services;

/// <summary>
/// 全部のプラグインの区切りの規則(<see cref="IPluginEncounterControlService.SetRulesAsync"/>)。合わせて Core の <see cref="EncounterSplitRules"/> へ書く。
///
/// <para>
/// 上書きの項目(<see cref="PluginEncounterRuleItem"/>)は、既定値でない値を最初に登録したプラグインが持つ。別の値は保留にし、持ち主が外れたら登録の順に当て直す。
/// ダンジョンの時計を持つプラグインは、ダンジョンの開始の区切りも <c>Split</c> で持ち、他のプラグインのボス部屋・全滅の <c>Split</c> とは並ばない。
/// 無操作・経過の秒は全部のプラグインの分を使う。
/// </para>
///
/// <para>
/// 書き換えは本体のスレッド(<see cref="RunOnHostThread"/>: キャプチャが動いていればパケットのスレッド、動いていなければ画面のスレッド)で行う。
/// 控えはプラグインの数で頭打ちで、プラグインを止めたときに外す。保存しない。
/// </para>
/// </summary>
internal sealed class PluginEncounterRuleRegistry
{
    private static readonly IReadOnlyList<PluginEncounterRuleItem> NoItems = Array.Empty<PluginEncounterRuleItem>();

    private readonly object _gate = new();
    /// <summary>登録の順(登録し直しても位置は変えず、外して登録し直したら後ろ)。</summary>
    private readonly List<Registration> _registrations = [];
    private PluginEncounterOverrides _applied = new(
        PluginAutoSplitMode.HostSetting,
        PluginAutoSplitMode.HostSetting,
        PluginAutoSplitMode.HostSetting,
        PluginDungeonClock.None);
    private IReadOnlyList<PluginInfo> _overridingPlugins = Array.Empty<PluginInfo>();

    public static PluginEncounterRuleRegistry Instance { get; } = new();

    private PluginEncounterRuleRegistry()
    {
    }

    /// <summary>合わせた結果(当てている上書き・保留・上書きしているプラグイン)が変わった後。画面のスレッドで上がる。</summary>
    public event EventHandler? Changed;

    /// <summary>当てている上書き。</summary>
    public PluginEncounterOverrides AppliedOverrides
    {
        get
        {
            lock (_gate)
            {
                return _applied;
            }
        }
    }

    /// <summary>上書きの項目を1つでも持っているプラグイン(登録の順)。全体設定の「進行で自動リセット」の注意書きに出す。</summary>
    public IReadOnlyList<PluginInfo> OverridingPlugins
    {
        get
        {
            lock (_gate)
            {
                return _overridingPlugins;
            }
        }
    }

    /// <summary>このプラグインの規則のうち、他のプラグインが持っていて当たっていない項目。</summary>
    public IReadOnlyList<PluginEncounterRuleItem> GetPendingItems(string pluginKey)
    {
        lock (_gate)
        {
            return Find(pluginKey)?.Pending ?? NoItems;
        }
    }

    /// <summary>
    /// 本体のスレッドで走らせる。キャプチャが動いていればパケットのスレッド(前に届いたメッセージの後)、動いていなければ画面のスレッド。
    /// 手動のリセットと設定の書き込みと同じスレッドにそろえ、回の作り直しと規則の書き換えを1本のスレッドに保つ。
    /// </summary>
    public static void RunOnHostThread(Action action)
    {
        if (MessageManager.netCap?.TryEnqueueCommand(action) == true)
        {
            return;
        }

        var dispatcher = Application.Current?.Dispatcher
            ?? throw new InvalidOperationException("The UI dispatcher is not available.");
        if (dispatcher.CheckAccess())
        {
            action();
        }
        else
        {
            dispatcher.BeginInvoke(action);
        }
    }

    /// <summary>規則を登録する(置き換える)。null で外す。本体のスレッドで呼ぶ。返すのはこのプラグインの保留の項目。</summary>
    public IReadOnlyList<PluginEncounterRuleItem> Set(string pluginKey, PluginInfo plugin, PluginEncounterRules? rules)
    {
        EncounterSplitRuleSet ruleSet;
        IReadOnlyList<PluginEncounterRuleItem> pending;
        lock (_gate)
        {
            var existing = Find(pluginKey);
            if (rules is null)
            {
                if (existing != null)
                {
                    _registrations.Remove(existing);
                }
            }
            else if (existing != null)
            {
                existing.Rules = rules;
            }
            else
            {
                _registrations.Add(new Registration(pluginKey, plugin, rules));
            }

            ruleSet = ComposeLocked();
            pending = Find(pluginKey)?.Pending ?? NoItems;
        }

        EncounterSplitRules.Apply(ruleSet);
        RaiseChanged();
        return pending;
    }

    private Registration? Find(string pluginKey)
    {
        return _registrations.Find(registration => registration.PluginKey == pluginKey);
    }

    /// <summary>登録の順に項目の持ち主を決め、保留を付け直し、Core へ書く1組を作る。</summary>
    private EncounterSplitRuleSet ComposeLocked()
    {
        var held = new Dictionary<PluginEncounterRuleItem, (Registration Owner, int Value)>();
        var conditions = new List<EncounterSplitCondition>();
        foreach (var registration in _registrations)
        {
            var rules = registration.Rules;
            var pending = new List<PluginEncounterRuleItem>();

            var clockAccepted = false;
            if (rules.DungeonClock != PluginDungeonClock.None)
            {
                clockAccepted = TryHold(held, registration, PluginEncounterRuleItem.DungeonClock, (int)rules.DungeonClock, pending);
            }

            var startSplit = rules.DungeonStartSplit != PluginAutoSplitMode.HostSetting
                ? rules.DungeonStartSplit
                : clockAccepted ? PluginAutoSplitMode.Split : PluginAutoSplitMode.HostSetting;
            if (startSplit != PluginAutoSplitMode.HostSetting)
            {
                TryHold(held, registration, PluginEncounterRuleItem.DungeonStartSplit, (int)startSplit, pending);
            }

            if (rules.BossRoomSplit != PluginAutoSplitMode.HostSetting)
            {
                TryHold(held, registration, PluginEncounterRuleItem.BossRoomSplit, (int)rules.BossRoomSplit, pending);
            }

            if (rules.WipeSplit != PluginAutoSplitMode.HostSetting)
            {
                TryHold(held, registration, PluginEncounterRuleItem.WipeSplit, (int)rules.WipeSplit, pending);
            }

            registration.Pending = pending.Count == 0 ? NoItems : pending.ToArray();
            if (rules.IdleSplitSeconds > 0 || rules.IntervalSplitSeconds > 0)
            {
                conditions.Add(new EncounterSplitCondition(rules.IdleSplitSeconds, rules.IntervalSplitSeconds, rules.KeepPreviousInMeterOnConditionSplit));
            }
        }

        _applied = new PluginEncounterOverrides(
            held.TryGetValue(PluginEncounterRuleItem.DungeonStartSplit, out var start) ? (PluginAutoSplitMode)start.Value : PluginAutoSplitMode.HostSetting,
            held.TryGetValue(PluginEncounterRuleItem.BossRoomSplit, out var boss) ? (PluginAutoSplitMode)boss.Value : PluginAutoSplitMode.HostSetting,
            held.TryGetValue(PluginEncounterRuleItem.WipeSplit, out var wipe) ? (PluginAutoSplitMode)wipe.Value : PluginAutoSplitMode.HostSetting,
            held.TryGetValue(PluginEncounterRuleItem.DungeonClock, out var clock) ? (PluginDungeonClock)clock.Value : PluginDungeonClock.None);
        _overridingPlugins = _registrations
            .Where(registration => held.Values.Any(holder => ReferenceEquals(holder.Owner, registration)))
            .Select(registration => registration.Plugin)
            .ToArray();

        return new EncounterSplitRuleSet(
            conditions.ToArray(),
            (EncounterSplitOverride)(int)_applied.DungeonStartSplit,
            (EncounterSplitOverride)(int)_applied.BossRoomSplit,
            (EncounterSplitOverride)(int)_applied.WipeSplit,
            (EncounterClockOrigin)(int)_applied.DungeonClock);
    }

    /// <summary>
    /// 項目を持つ。他のプラグインが違う値で持っているか、ダンジョンの時計と並ばない値なら保留に入れて false。
    /// 同じ値の重なりは持ち主を変えずに通す。
    /// </summary>
    private static bool TryHold(
        Dictionary<PluginEncounterRuleItem, (Registration Owner, int Value)> held,
        Registration registration,
        PluginEncounterRuleItem item,
        int value,
        List<PluginEncounterRuleItem> pending)
    {
        var conflicts = held.TryGetValue(item, out var holder) && holder.Value != value;
        conflicts |= item switch
        {
            PluginEncounterRuleItem.DungeonClock =>
                HeldBy(held, PluginEncounterRuleItem.DungeonStartSplit, (int)PluginAutoSplitMode.DoNotSplit)
                || HeldBy(held, PluginEncounterRuleItem.BossRoomSplit, (int)PluginAutoSplitMode.Split)
                || HeldBy(held, PluginEncounterRuleItem.WipeSplit, (int)PluginAutoSplitMode.Split),
            PluginEncounterRuleItem.DungeonStartSplit =>
                value == (int)PluginAutoSplitMode.DoNotSplit && held.ContainsKey(PluginEncounterRuleItem.DungeonClock),
            PluginEncounterRuleItem.BossRoomSplit or PluginEncounterRuleItem.WipeSplit =>
                value == (int)PluginAutoSplitMode.Split && held.ContainsKey(PluginEncounterRuleItem.DungeonClock),
            _ => false,
        };

        if (conflicts)
        {
            pending.Add(item);
            return false;
        }

        held.TryAdd(item, (registration, value));
        return true;
    }

    private static bool HeldBy(Dictionary<PluginEncounterRuleItem, (Registration Owner, int Value)> held, PluginEncounterRuleItem item, int value)
    {
        return held.TryGetValue(item, out var holder) && holder.Value == value;
    }

    private void RaiseChanged()
    {
        var dispatcher = Application.Current?.Dispatcher;
        if (dispatcher is null)
        {
            return;
        }

        dispatcher.BeginInvoke(() => Changed?.Invoke(this, EventArgs.Empty));
    }

    private sealed class Registration(string pluginKey, PluginInfo plugin, PluginEncounterRules rules)
    {
        public string PluginKey { get; } = pluginKey;

        public PluginInfo Plugin { get; } = plugin;

        public PluginEncounterRules Rules { get; set; } = rules;

        public IReadOnlyList<PluginEncounterRuleItem> Pending { get; set; } = NoItems;
    }
}
