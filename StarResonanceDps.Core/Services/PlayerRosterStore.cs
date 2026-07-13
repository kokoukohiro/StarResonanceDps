using System;
using System.Collections.Generic;
using System.Linq;
using StarResonanceDps.Core.Models;

namespace StarResonanceDps.Core.Services;

public sealed class PlayerRosterStore
{
    private static readonly Lazy<PlayerRosterStore> LazyInstance = new(() => new PlayerRosterStore());

    private readonly object _sync = new();
    private readonly Dictionary<long, PlayerRosterEntry> _entries = [];
    private IReadOnlyList<PlayerRosterEntry> _snapshot = Array.AsReadOnly(Array.Empty<PlayerRosterEntry>());
    private string _mapName = string.Empty;
    private long _mapGeneration;

    private PlayerRosterStore()
    {
    }

    public static PlayerRosterStore Instance => LazyInstance.Value;

    public event EventHandler<PlayerRosterChangedEventArgs>? RosterChanged;

    public IReadOnlyList<PlayerRosterEntry> Snapshot
    {
        get
        {
            lock (_sync)
            {
                return _snapshot;
            }
        }
    }

    public PlayerRosterSnapshot Current
    {
        get
        {
            lock (_sync)
            {
                return new PlayerRosterSnapshot(_snapshot, _mapName, _mapGeneration);
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

    public void UpdateMapName(string? mapName)
    {
        var normalizedMapName = mapName ?? string.Empty;
        PublishIfChanged(
            () => _mapName = normalizedMapName,
            forcePublish: false);
    }

    public void Upsert(PlayerRosterEntry entry)
    {
        if (entry.CharacterId == 0)
        {
            return;
        }

        PublishIfChanged(
            () =>
            {
                var normalized = Clone(entry);
                if (_entries.TryGetValue(normalized.CharacterId, out var existing))
                {
                    if (existing.IsSelf && !normalized.IsSelf)
                    {
                        normalized = normalized with { IsSelf = true };
                    }

                    if (existing.IsNpc && !normalized.IsNpc)
                    {
                        normalized = normalized with { IsNpc = true };
                    }
                }

                _entries[normalized.CharacterId] = normalized;
            },
            forcePublish: false);
    }

    public void Replace(IEnumerable<PlayerRosterEntry> entries, string? mapName = null)
    {
        ArgumentNullException.ThrowIfNull(entries);

        var nextEntries = entries
            .Where(entry => entry.CharacterId != 0)
            .Select(Clone)
            .GroupBy(entry => entry.CharacterId)
            .Select(group =>
            {
                var selected = group.LastOrDefault(entry => entry.IsSelf) ?? group.Last();
                return selected with { IsSelf = group.Any(entry => entry.IsSelf) };
            })
            .ToArray();

        PublishIfChanged(
            () =>
            {
                _entries.Clear();
                foreach (var entry in nextEntries)
                {
                    _entries[entry.CharacterId] = entry;
                }

                if (mapName is not null)
                {
                    _mapName = mapName;
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
        PlayerRosterSnapshot? changedSnapshot = null;

        lock (_sync)
        {
            var previousEntries = _snapshot;
            var previousMapName = _mapName;
            var previousMapGeneration = _mapGeneration;

            update();

            var nextSnapshot = CreateSnapshotNoLock();
            if (!forcePublish
                && previousEntries.SequenceEqual(nextSnapshot)
                && string.Equals(previousMapName, _mapName, StringComparison.Ordinal)
                && previousMapGeneration == _mapGeneration)
            {
                return;
            }

            _snapshot = nextSnapshot;
            changedSnapshot = new PlayerRosterSnapshot(_snapshot, _mapName, _mapGeneration);
        }

        RosterChanged?.Invoke(this, new PlayerRosterChangedEventArgs(changedSnapshot!));
    }

    private IReadOnlyList<PlayerRosterEntry> CreateSnapshotNoLock()
    {
        return Array.AsReadOnly(_entries.Values
            .Select(Clone)
            .OrderByDescending(entry => entry.IsSelf)
            .ThenBy(entry => entry.Name, StringComparer.Ordinal)
            .ThenBy(entry => entry.CharacterId)
            .ToArray());
    }

    private static PlayerRosterEntry Clone(PlayerRosterEntry entry)
    {
        return new PlayerRosterEntry(
            entry.CharacterId,
            entry.Name ?? string.Empty,
            entry.ProfessionId,
            entry.CombatPower,
            entry.SeasonStrength,
            entry.CurrentHp,
            entry.MaxHp,
            entry.ClassSpec,
            entry.IsSelf,
            entry.CombatAttributes,
            entry.SubProfessionId,
            entry.Level,
            entry.SeasonLevel,
            entry.EquipmentData,
            entry.IsNpc,
            entry.CurrentShield,
            entry.CurrentStamina,
            entry.MaxStamina);
    }
}

public sealed record PlayerRosterSnapshot(
    IReadOnlyList<PlayerRosterEntry> Entries,
    string MapName,
    long MapGeneration);

public sealed class PlayerRosterChangedEventArgs(PlayerRosterSnapshot roster) : EventArgs
{
    public IReadOnlyList<PlayerRosterEntry> Snapshot { get; } = roster.Entries;

    public string MapName { get; } = roster.MapName;

    public long MapGeneration { get; } = roster.MapGeneration;
}
