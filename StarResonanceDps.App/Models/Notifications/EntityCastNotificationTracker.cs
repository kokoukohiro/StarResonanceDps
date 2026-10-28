using StarResonanceDps.App.Config;
using StarResonanceDps.App.Services;
using StarResonanceDps.Core.Models;

namespace StarResonanceDps.App.Models.Notifications;

/// <summary>詠唱1つ。周りの敵(エンティティリストの実体)がしている詠唱。</summary>
/// <param name="EntityName">詠唱している敵の名前。</param>
public readonly record struct EntityCastObservation(long EntityUuid, NearbyEntityCast Cast, string EntityName);

/// <summary>
/// 通知「エンティティ詠唱」の判定。周りの敵の一覧が届くたびに、詠唱している敵(<see cref="NearbyEntityEntry.Cast"/> のあるもの)を渡す。
/// ウィジェットを開いていなくても動く(<see cref="NotificationMonitor"/>)。
///
/// <para>
/// 敵ごとに前の一覧の詠唱と比べ、新しく始まった詠唱の全体の秒が通知条件の秒以上なら、その場で1回出す。
/// 詠唱はエンティティリストの詠唱ゲージと同じ値で、周りに現れたときにすでに詠唱していた敵は値を持たないので出さない。
/// 同じ一覧の中で同じ名前の敵がいくつ詠唱を始めても1回(同じ文が並ぶだけになるため。エンティティ出現と同じ)。
/// オンにした・通知条件を選び直したときは <see cref="Rebaseline"/> で、その時点で詠唱中の敵には出さない。
/// </para>
/// </summary>
public sealed class EntityCastNotificationTracker
{
    /// <summary>敵ごとに最後に見た詠唱。鍵は敵の UUID。最後の一覧で詠唱していなかった敵は持たない。</summary>
    private Dictionary<long, NearbyEntityCast> _castsByEntityUuid = [];
    private Dictionary<long, NearbyEntityCast> _nextCastsByEntityUuid = [];
    private readonly HashSet<string> _notifiedNames = new(StringComparer.Ordinal);

    /// <summary>今の詠唱を、出さずに「既に見た詠唱」として控え直す。オフのときは空で呼ぶ。</summary>
    public void Rebaseline(IReadOnlyList<EntityCastObservation> casts)
    {
        _castsByEntityUuid.Clear();
        foreach (var cast in casts)
        {
            _castsByEntityUuid[cast.EntityUuid] = cast.Cast;
        }
    }

    public void Observe(IReadOnlyList<EntityCastObservation> casts)
    {
        var minimumSeconds = GetMinimumSeconds();
        _nextCastsByEntityUuid.Clear();
        _notifiedNames.Clear();
        foreach (var cast in casts)
        {
            _nextCastsByEntityUuid[cast.EntityUuid] = cast.Cast;
            if ((_castsByEntityUuid.TryGetValue(cast.EntityUuid, out var previous) && previous == cast.Cast)
                || cast.Cast.TotalSeconds < minimumSeconds
                || !_notifiedNames.Add(cast.EntityName))
            {
                continue;
            }

            NotificationService.Instance.Notify(
                NotificationKind.EntityCast,
                NotificationTextFormatter.FormatEntityCast(
                    NotificationTextFormatter.ResolveFormat(NotificationKind.EntityCast),
                    cast.EntityName));
        }

        (_castsByEntityUuid, _nextCastsByEntityUuid) = (_nextCastsByEntityUuid, _castsByEntityUuid);
    }

    /// <summary>通知する詠唱の全体の秒の下限。通知タブの「通知条件」。0 は無条件。</summary>
    private static int GetMinimumSeconds()
    {
        var index = ConfigManager.Instance.GetNotificationItem(NotificationKind.EntityCast).EntityCastConditionIndex;
        return AppConfigDefaults.EntityCastMinimumSeconds[index];
    }
}
