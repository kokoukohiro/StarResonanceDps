using StarResonanceDps.Core.CombatRuntime;

namespace StarResonanceDps.App.Models.Notifications;

/// <summary>通知を判定するバフの持ち主。プレイヤー(CharacterId)か、エンティティリストの実体(UUID)。</summary>
public readonly record struct BuffNotificationTarget(bool IsEntity, long Id)
{
    public static BuffNotificationTarget ForPlayer(long characterId) => new(false, characterId);

    public static BuffNotificationTarget ForEntity(long entityUuid) => new(true, entityUuid);
}

/// <summary>
/// バフの通知の判定(<see cref="BuffNotificationTracker"/>)を、対象とバフごとに1つにまとめる。
/// バフ・デバフカードとバフ・デバフグループが同じ対象の同じバフを見ているとき、窓ごとに判定すると同じ通知が窓の数だけ出るため。
/// 見ている窓が全部手放したら判定を捨てる(次に見始めたときは新しく作る)。画面のスレッドだけで使う。
/// </summary>
public sealed class BuffNotificationRegistry
{
    private const string GroupKeyPrefix = "group:";

    private readonly Dictionary<(BuffNotificationTarget Target, string BuffKey), Entry> _entries = [];

    public static BuffNotificationRegistry Instance { get; } = new();

    /// <summary>
    /// 判定のバフの鍵。料理・薬剤のまとまりはまとまりの鍵、それ以外はカードの鍵(<see cref="PlayerBuffSnapshot.CardKey"/>)。
    /// カードとグループは料理・薬剤をどちらもまとまりで見るので、同じ鍵になる。
    /// </summary>
    public static string CreateBuffKey(BuffGroup group, string cardKey)
    {
        return group != BuffGroup.None ? GroupKeyPrefix + group : cardKey;
    }

    /// <summary>対象とバフの判定を借りる。使い終わったら返り値を Dispose する。</summary>
    public BuffNotificationWatch Watch(BuffNotificationTarget target, string buffKey)
    {
        var key = (target, buffKey);
        if (!_entries.TryGetValue(key, out var entry))
        {
            entry = new Entry();
            _entries.Add(key, entry);
        }

        entry.WatchCount++;
        return new BuffNotificationWatch(this, target, buffKey, entry.Tracker);
    }

    internal void Release(BuffNotificationTarget target, string buffKey)
    {
        var key = (target, buffKey);
        if (!_entries.TryGetValue(key, out var entry))
        {
            return;
        }

        entry.WatchCount--;
        if (entry.WatchCount <= 0)
        {
            _entries.Remove(key);
        }
    }

    private sealed class Entry
    {
        public BuffNotificationTracker Tracker { get; } = new();

        public int WatchCount { get; set; }
    }
}

/// <summary>窓が借りている、1つの対象の1つのバフの判定。</summary>
public sealed class BuffNotificationWatch : IDisposable
{
    private readonly BuffNotificationRegistry _registry;
    private readonly BuffNotificationTracker _tracker;
    private bool _isDisposed;

    internal BuffNotificationWatch(
        BuffNotificationRegistry registry,
        BuffNotificationTarget target,
        string buffKey,
        BuffNotificationTracker tracker)
    {
        _registry = registry;
        Target = target;
        BuffKey = buffKey;
        _tracker = tracker;
    }

    public BuffNotificationTarget Target { get; }

    public string BuffKey { get; }

    /// <param name="snapshot">窓が見つけたバフ。消えていれば null。</param>
    /// <param name="targetName">窓に出している対象の名前。</param>
    public void Observe(PlayerBuffSnapshot? snapshot, string targetName)
    {
        if (!_isDisposed)
        {
            _tracker.Observe(snapshot, targetName);
        }
    }

    public void Dispose()
    {
        if (_isDisposed)
        {
            return;
        }

        _isDisposed = true;
        _registry.Release(Target, BuffKey);
    }
}

/// <summary>
/// 1つの対象の1つのバフだけを見る窓(バフ・デバフカード)の判定の借り方。
/// 対象かバフが替わったら前の判定を返して借り直し、対象が分からなくなったら返す。
/// </summary>
public sealed class BuffNotificationWatcher : IDisposable
{
    private BuffNotificationWatch? _watch;

    public void Observe(BuffNotificationTarget target, string buffKey, PlayerBuffSnapshot? snapshot, string targetName)
    {
        if (_watch is null || _watch.Target != target || !string.Equals(_watch.BuffKey, buffKey, StringComparison.Ordinal))
        {
            _watch?.Dispose();
            _watch = BuffNotificationRegistry.Instance.Watch(target, buffKey);
        }

        _watch.Observe(snapshot, targetName);
    }

    /// <summary>対象かバフが分からなくなった・窓を閉じた。判定を返す(ほかの窓が見ていなければ捨てる)。</summary>
    public void Release()
    {
        _watch?.Dispose();
        _watch = null;
    }

    public void Dispose()
    {
        Release();
    }
}
