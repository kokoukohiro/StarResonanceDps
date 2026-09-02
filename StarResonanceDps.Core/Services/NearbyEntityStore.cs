using StarResonanceDps.Core.CombatRuntime;
using StarResonanceDps.Core.Models;

namespace StarResonanceDps.Core.Services;

public sealed class NearbyEntityStore
{
    private static readonly Lazy<NearbyEntityStore> LazyInstance = new(() => new NearbyEntityStore());

    private readonly object _sync = new();
    private readonly Dictionary<long, NearbyEntityEntry> _entries = [];
    private IReadOnlyList<NearbyEntityEntry> _snapshot = Array.AsReadOnly(Array.Empty<NearbyEntityEntry>());
    private string _mapName = string.Empty;
    private uint _mapChannel;
    private long _mapGeneration;

    private NearbyEntityStore()
    {
    }

    public static NearbyEntityStore Instance => LazyInstance.Value;

    public event EventHandler<NearbyEntitiesChangedEventArgs>? EntitiesChanged;

    public NearbyEntitySnapshot Current
    {
        get
        {
            lock (_sync)
            {
                return new NearbyEntitySnapshot(_snapshot, _mapName, _mapChannel, _mapGeneration);
            }
        }
    }

    public void BeginMap()
    {
        PublishIfChanged(
            () =>
            {
                _mapGeneration++;
                _entries.Clear();
            },
            forcePublish: true);
    }

    /// <summary>
    /// マップ名とチャンネル番号。<b>合成した文字列にはしない。</b>
    /// 「ch1」の表記は言語ごとに違う(中国語は「1线」)ので、組み立ては翻訳資源を持つ App 側でやる。
    /// </summary>
    public void UpdateMapName(string? mapName, uint mapChannel)
    {
        var normalizedMapName = mapName ?? string.Empty;
        PublishIfChanged(
            () =>
            {
                _mapName = normalizedMapName;
                _mapChannel = mapChannel;
            },
            forcePublish: false);
    }

    public void UpsertAppeared(NearbyEntityEntry entry)
    {
        Upsert(entry, requireExistingEntry: false);
    }

    public void Refresh(NearbyEntityEntry entry)
    {
        Upsert(entry, requireExistingEntry: true);
    }

    internal bool TryRefresh(
        long entityUuid,
        Func<NearbyEntityEntry, NearbyEntityEntry> update)
    {
        if (entityUuid == 0)
        {
            return false;
        }

        ArgumentNullException.ThrowIfNull(update);

        var refreshed = false;
        PublishIfChanged(
            () =>
            {
                if (_entries.TryGetValue(entityUuid, out var existing))
                {
                    refreshed = true;
                    var updated = update(existing);
                    _entries[entityUuid] = updated with
                    {
                        EntityUuid = entityUuid,
                        Name = updated.Name ?? string.Empty
                    };
                }
            },
            forcePublish: false);

        return refreshed;
    }

    private void Upsert(NearbyEntityEntry entry, bool requireExistingEntry)
    {
        if (entry.EntityUuid == 0)
        {
            return;
        }

        PublishIfChanged(
            () =>
            {
                if (!requireExistingEntry || _entries.ContainsKey(entry.EntityUuid))
                {
                    _entries[entry.EntityUuid] = entry with { Name = entry.Name ?? string.Empty };
                }
            },
            forcePublish: false);
    }

    public void Remove(long entityUuid)
    {
        if (entityUuid == 0)
        {
            return;
        }

        PublishIfChanged(
            () => _entries.Remove(entityUuid),
            forcePublish: false);
    }

    public void UpdateCampRelations(IReadOnlyDictionary<long, EntityCampRelation> relations)
    {
        ArgumentNullException.ThrowIfNull(relations);

        PublishIfChanged(
            () =>
            {
                foreach (var relation in relations)
                {
                    if (_entries.TryGetValue(relation.Key, out var entry)
                        && entry.CampRelation != relation.Value)
                    {
                        _entries[relation.Key] = entry with { CampRelation = relation.Value };
                    }
                }
            },
            forcePublish: false);
    }

    public void Clear()
    {
        PublishIfChanged(
            () =>
            {
                _mapGeneration++;
                _entries.Clear();
                _mapName = string.Empty;
            },
            forcePublish: true);
    }

    private void PublishIfChanged(Action update, bool forcePublish)
    {
        NearbyEntitySnapshot? changedSnapshot = null;

        lock (_sync)
        {
            var previousEntries = _snapshot;
            var previousMapName = _mapName;
            var previousMapChannel = _mapChannel;
            var previousMapGeneration = _mapGeneration;

            update();

            var nextSnapshot = CreateSnapshotNoLock();
            if (!forcePublish
                && previousEntries.SequenceEqual(nextSnapshot)
                && string.Equals(previousMapName, _mapName, StringComparison.Ordinal)
                && previousMapChannel == _mapChannel
                && previousMapGeneration == _mapGeneration)
            {
                return;
            }

            _snapshot = nextSnapshot;
            changedSnapshot = new NearbyEntitySnapshot(_snapshot, _mapName, _mapChannel, _mapGeneration);
        }

        EntitiesChanged?.Invoke(this, new NearbyEntitiesChangedEventArgs(changedSnapshot!));
    }

    private IReadOnlyList<NearbyEntityEntry> CreateSnapshotNoLock()
    {
        return Array.AsReadOnly(_entries.Values
            .OrderBy(GetSortRank)
            .ThenBy(entry => entry.Name, StringComparer.Ordinal)
            .ThenBy(entry => entry.EntityUuid)
            .ToArray());
    }

    private static int GetSortRank(NearbyEntityEntry entry)
    {
        return entry.MonsterType switch
        {
            EMonsterType.Boss => 0,
            EMonsterType.Elite => 1,
            EMonsterType.Monster => 2,
            _ => 3
        };
    }
}

public sealed record NearbyEntitySnapshot(
    IReadOnlyList<NearbyEntityEntry> Entries,
    string MapName,
    uint MapChannel,
    long MapGeneration);

public sealed class NearbyEntitiesChangedEventArgs(NearbyEntitySnapshot snapshot) : EventArgs
{
    public IReadOnlyList<NearbyEntityEntry> Snapshot { get; } = snapshot.Entries;

    /// <summary>チャンネル番号。チャンネルの無い場所では 0。</summary>
    public uint MapChannel { get; } = snapshot.MapChannel;

    public string MapName { get; } = snapshot.MapName;

    public long MapGeneration { get; } = snapshot.MapGeneration;
}
