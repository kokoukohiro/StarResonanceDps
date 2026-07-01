using StarResonanceDps.Core.Models;

namespace StarResonanceDps.Core.Services;

public sealed class PlayerRosterStore
{
    private static readonly Lazy<PlayerRosterStore> LazyInstance = new(() => new PlayerRosterStore());

    private readonly object _sync = new();
    private IReadOnlyList<PlayerRosterEntry> _snapshot = Array.AsReadOnly(Array.Empty<PlayerRosterEntry>());
    private string _mapName = string.Empty;

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
                return new PlayerRosterSnapshot(_snapshot, _mapName);
            }
        }
    }

    public void Replace(IEnumerable<PlayerRosterEntry> entries, string? mapName = null)
    {
        var next = Array.AsReadOnly(entries
            .Select(entry => new PlayerRosterEntry(
                entry.CharacterId,
                entry.Name ?? string.Empty,
                entry.ProfessionId,
                entry.CombatPower,
                entry.SeasonStrength,
                entry.CurrentHp,
                entry.MaxHp,
                entry.ClassSpec,
                entry.IsSelf,
                entry.CombatAttributes))
            .OrderByDescending(entry => entry.IsSelf)
            .ToArray());

        PlayerRosterSnapshot? changedSnapshot = null;
        lock (_sync)
        {
            var nextMapName = mapName ?? _mapName;
            if (_snapshot.SequenceEqual(next)
                && string.Equals(_mapName, nextMapName, StringComparison.Ordinal))
            {
                return;
            }

            _snapshot = next;
            _mapName = nextMapName;
            changedSnapshot = new PlayerRosterSnapshot(_snapshot, _mapName);
        }

        RosterChanged?.Invoke(this, new PlayerRosterChangedEventArgs(changedSnapshot!));
    }

    public void Clear()
    {
        Replace(Array.Empty<PlayerRosterEntry>(), string.Empty);
    }
}

public sealed record PlayerRosterSnapshot(IReadOnlyList<PlayerRosterEntry> Entries, string MapName);

public sealed class PlayerRosterChangedEventArgs(PlayerRosterSnapshot roster) : EventArgs
{
    public IReadOnlyList<PlayerRosterEntry> Snapshot { get; } = roster.Entries;

    public string MapName { get; } = roster.MapName;
}
