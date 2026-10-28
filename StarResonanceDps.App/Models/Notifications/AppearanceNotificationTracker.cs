using StarResonanceDps.App.Config;
using StarResonanceDps.App.Services;

namespace StarResonanceDps.App.Models.Notifications;

/// <summary>
/// 通知「プレイヤー出現」「エンティティ出現」の判定。見張り(<see cref="NotificationMonitor"/>)が、周りに見えている相手の一覧を届くたびに渡す。
/// ウィジェットを開いていなくても動く。
///
/// <para>
/// 前の一覧に無かった相手のうち、名前が通知タブの対象の名前と一致するものが現れたら出す。視界を出入りすると、入るたびに出す。
/// 同じ一覧の中で同じ名前の相手がいくつ現れても1回(同じ文が並ぶだけになるため)。
/// 名前は前後の空白を除いて、大文字小文字を区別せずに完全一致で比べる(部分一致だと別の名前まで当たる)。
/// オンにした・名前を変えたときは <see cref="Rebaseline"/> で、その時点で既にいる相手には出さない。
/// </para>
/// </summary>
public sealed class AppearanceNotificationTracker(NotificationKind kind)
{
    private HashSet<long> _presentKeys = [];
    private HashSet<long> _nextPresentKeys = [];
    private readonly HashSet<string> _notifiedNames = new(StringComparer.Ordinal);
    private bool _isBaselinePending;

    /// <summary>次の一覧を、出さずに「既にいる相手」として控える。</summary>
    public void Rebaseline()
    {
        _isBaselinePending = true;
    }

    /// <param name="items">いま周りに見えている相手。</param>
    /// <param name="getKey">相手を見分ける番号(プレイヤーはキャラクターID、エンティティは実体の UUID)。</param>
    /// <param name="getMatchName">対象の名前と比べる名前。名前が無ければ空。</param>
    /// <param name="getDisplayName">通知の文に入れる名前。一致したときだけ引く。</param>
    public void Observe<T>(
        IReadOnlyList<T> items,
        Func<T, long> getKey,
        Func<T, string> getMatchName,
        Func<T, string> getDisplayName)
    {
        var targetName = ConfigManager.Instance.GetNotificationItem(kind).TargetName.Trim();
        _nextPresentKeys.Clear();
        _notifiedNames.Clear();
        foreach (var item in items)
        {
            var key = getKey(item);
            _nextPresentKeys.Add(key);
            if (_isBaselinePending
                || targetName.Length == 0
                || _presentKeys.Contains(key)
                || !string.Equals(getMatchName(item).Trim(), targetName, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var displayName = getDisplayName(item);
            if (_notifiedNames.Add(displayName))
            {
                NotificationService.Instance.Notify(
                    kind,
                    NotificationTextFormatter.FormatAppeared(kind, NotificationTextFormatter.ResolveFormat(kind), displayName));
            }
        }

        (_presentKeys, _nextPresentKeys) = (_nextPresentKeys, _presentKeys);
        _isBaselinePending = false;
    }
}
