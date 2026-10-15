using StarResonanceDps.Core.CombatRuntime;
using StarResonanceDps.Core.Diagnostics;

namespace StarResonanceDps.Core.Services;

/// <summary>バフ1つの由来。<see cref="Landing"/> は付与元をたどって着いた先(着かなければ無し)、<see cref="Trace"/> はたどった鎖。</summary>
public sealed record BuffOrigin(SourceLanding Landing, string Trace);

/// <summary>
/// 表に名前の無いバフの由来(<see cref="BuffEvent.Origin"/>)を、付与の通知の最後に決める。
/// 名前は <c>CombatDataCatalog.GetBuffDisplayName</c> がこの由来から引く。
///
/// <para>
/// 付与(<c>BuffInfo</c> を伴う通知)を受けたら一覧に入れ、通知1件(周りの差分・自分宛ての差分・出現)の処理を終えたところで
/// まとめてたどる(<see cref="ResolvePending"/>)。同じ通知の中で親のバフが子より後ろに届いても、たどる時点では親が索引に入っている。
/// 結果はそのバフに持たせ、画面の更新ごとにはたどらない。
/// </para>
///
/// <para>
/// たどった後、バフ/デバフリストに出るものは常設の空欄検知(<see cref="BlankBuffNameProbe"/>)へ渡す
/// (由来に着いたものも、着かずに空欄のまま出るものも。ここへ来るのは表に名前の無いバフだけ)。
/// モジュール効果のバフは渡さない(名前・絵・出し方は番号ごとの手修正で決めている)。
/// 名前のあるバフは由来をたどらないが、装備効果(セット効果・武器効果)のバフで出し方の手修正(優先度)が無いものは別の一覧に入れ、
/// 通知の最後に足す規則だけで出るなら空欄検知へ渡す(<see cref="BlankBuffNameProbe.CaptureUnhandledEquipmentEffect"/>)。
/// </para>
///
/// <para>
/// 一覧は通知1件の中だけで使い、たどれば空になる。ログアウト・キャプチャの停止・マップ移動でも空にする(<see cref="Clear"/>)。
/// </para>
/// </summary>
public sealed class BuffOriginResolver
{
    private static readonly Lazy<BuffOriginResolver> LazyInstance = new(() => new BuffOriginResolver());

    private readonly object _sync = new();
    private readonly List<(long HolderUuid, BuffEvent BuffEvent, DateTime ArrivalTime)> _pending = [];

    /// <summary>名前のある装備効果のバフで、出し方の手修正が無いもの。通知の最後に、出るかを見て空欄検知へ渡す。</summary>
    private readonly List<(long HolderUuid, BuffEvent BuffEvent)> _pendingEquipmentEffects = [];

    private BuffOriginResolver()
    {
    }

    public static BuffOriginResolver Instance => LazyInstance.Value;

    /// <summary>
    /// 付与を受けたバフを一覧に入れる。前の由来は消す(付与のたびに付与元が変わりうる)。
    /// 表に名前のあるバフはたどらない(名前に由来を使わない)。そのうち装備効果のバフで出し方の手修正が無いものだけ、
    /// 空欄検知のための別の一覧に入れる。
    /// </summary>
    public void Enqueue(long holderUuid, BuffEvent buffEvent, DateTime arrivalTime)
    {
        buffEvent.SetOrigin(null);
        if (buffEvent.BaseId <= 0)
        {
            return;
        }

        if (!string.IsNullOrEmpty(CombatDataCatalog.GetBuffNameWithoutInternalId(buffEvent.BaseId)))
        {
            if (BuffDisplayRules.IsEquipmentEffect(buffEvent)
                && !CombatDataCatalog.HasBuffPriorityOverride(buffEvent.BaseId))
            {
                lock (_sync)
                {
                    _pendingEquipmentEffects.Add((holderUuid, buffEvent));
                }
            }

            return;
        }

        lock (_sync)
        {
            _pending.Add((holderUuid, buffEvent, arrivalTime));
        }
    }

    /// <summary>一覧のバフの由来をたどる。通知1件の処理の最後に呼ぶ。到着時刻は付与を受けた時刻を使う。</summary>
    public void ResolvePending()
    {
        (long HolderUuid, BuffEvent BuffEvent, DateTime ArrivalTime)[] items;
        (long HolderUuid, BuffEvent BuffEvent)[] equipmentEffects;
        lock (_sync)
        {
            if (_pending.Count == 0 && _pendingEquipmentEffects.Count == 0)
            {
                return;
            }

            items = [.. _pending];
            _pending.Clear();
            equipmentEffects = [.. _pendingEquipmentEffects];
            _pendingEquipmentEffects.Clear();
        }

        foreach (var (holderUuid, buffEvent, arrivalTime) in items)
        {
            var landing = SourceLandingResolver.Instance.ResolveBuffOrigin(
                buffEvent.BaseId,
                buffEvent.FightSourceType,
                buffEvent.SourceConfigId,
                buffEvent.FireUuid,
                arrivalTime,
                out var trace);
            buffEvent.SetOrigin(new BuffOrigin(landing, trace));

            if (EncounterManager.Current.Entities.TryGetValue(holderUuid, out var bearer)
                && BuffDisplayRules.IsDisplayed(buffEvent, bearer)
                && BuffDisplayRules.GetModuleEffectId(buffEvent) == 0)
            {
                BlankBuffNameProbe.Capture(holderUuid, buffEvent, landing, trace);
            }
        }

        // 足す規則だけで出るもの(ゲームの条件で出ず、手修正の優先度も無い)。
        foreach (var (holderUuid, buffEvent) in equipmentEffects)
        {
            if (EncounterManager.Current.Entities.TryGetValue(holderUuid, out var bearer)
                && BuffDisplayRules.IsDisplayed(buffEvent, bearer)
                && !BuffDisplayRules.IsShownByGameRule(buffEvent))
            {
                BlankBuffNameProbe.CaptureUnhandledEquipmentEffect(holderUuid, buffEvent);
            }
        }
    }

    public void Clear()
    {
        lock (_sync)
        {
            _pending.Clear();
            _pendingEquipmentEffects.Clear();
        }
    }
}
