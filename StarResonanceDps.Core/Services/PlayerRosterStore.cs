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

    /// <summary>
    /// 初めて現れた順。<b>行はソートせず、新しい行を末尾に足すためにこれが要る。</b>
    ///
    /// <para>
    /// <see cref="_entries"/> は <see cref="Replace"/> のたびに丸ごと入れ替わるうえ、
    /// <c>Dictionary</c> の列挙順は契約ではない(削除で空いた枠に新しい要素が入る)。
    /// だから順序はストア側で明示的に持つ。居なくなったら番号も捨てる。
    /// </para>
    /// </summary>
    private readonly Dictionary<long, long> _firstSeenOrderByCharacterId = [];
    private long _nextFirstSeenOrder;
    private IReadOnlyList<PlayerRosterEntry> _snapshot = Array.AsReadOnly(Array.Empty<PlayerRosterEntry>());
    private string _mapName = string.Empty;
    private uint _mapChannel;
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
                return new PlayerRosterSnapshot(_snapshot, _mapName, _mapChannel, _mapGeneration);
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
                ClearFirstSeenOrderNoLock();
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
                }

                _entries[normalized.CharacterId] = normalized;
                EnsureFirstSeenOrderNoLock(normalized.CharacterId);
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
                    EnsureFirstSeenOrderNoLock(entry.CharacterId);
                }

                // 居なくなった人の番号は捨てる。戻ってきたら末尾に付き直す。
                foreach (var characterId in _firstSeenOrderByCharacterId.Keys.ToArray())
                {
                    if (!_entries.ContainsKey(characterId))
                    {
                        _firstSeenOrderByCharacterId.Remove(characterId);
                    }
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
                ClearFirstSeenOrderNoLock();
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
            changedSnapshot = new PlayerRosterSnapshot(_snapshot, _mapName, _mapChannel, _mapGeneration);
        }

        RosterChanged?.Invoke(this, new PlayerRosterChangedEventArgs(changedSnapshot!));
    }

    /// <summary>
    /// 並びは <b>自分 → パーティ(PT順) → PT外の灰色 → PT外のライブ(初出順)</b>。
    ///
    /// <para>
    /// <b>名前や値でソートしない。</b> 新しい行は必ず末尾に付く。
    /// 勝手に並べ替わると、見ている行が動いて追えなくなる。
    /// </para>
    /// </summary>
    private IReadOnlyList<PlayerRosterEntry> CreateSnapshotNoLock()
    {
        return Array.AsReadOnly(_entries.Values
            .Select(Clone)
            .OrderBy(GetGroupRank)
            // パーティ内だけは PT番号順。番号が無い人はその後ろへ初出順で続く。
            .ThenBy(entry => entry.IsPartyMember && entry.PartyNumber.HasValue
                ? entry.PartyNumber!.Value
                : int.MaxValue)
            .ThenBy(GetFirstSeenOrderNoLock)
            .ToArray());
    }

    private static int GetGroupRank(PlayerRosterEntry entry)
    {
        if (entry.IsSelf)
        {
            return 0;
        }

        if (entry.IsPartyMember)
        {
            return 1;
        }

        // 灰色(キャッシュしか無い行)はライブより上。
        return entry.IsLive ? 3 : 2;
    }

    private long GetFirstSeenOrderNoLock(PlayerRosterEntry entry)
    {
        return _firstSeenOrderByCharacterId.TryGetValue(entry.CharacterId, out var order)
            ? order
            : long.MaxValue;
    }

    private void EnsureFirstSeenOrderNoLock(long characterId)
    {
        if (characterId != 0 && !_firstSeenOrderByCharacterId.ContainsKey(characterId))
        {
            _firstSeenOrderByCharacterId[characterId] = _nextFirstSeenOrder++;
        }
    }

    private void ClearFirstSeenOrderNoLock()
    {
        _firstSeenOrderByCharacterId.Clear();
        _nextFirstSeenOrder = 0;
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
            entry.IsPartyMember,
            entry.PartyNumber,
            entry.IsLive);
    }
}

public sealed record PlayerRosterSnapshot(
    IReadOnlyList<PlayerRosterEntry> Entries,
    string MapName,
    uint MapChannel,
    long MapGeneration);

public sealed class PlayerRosterChangedEventArgs(PlayerRosterSnapshot roster) : EventArgs
{
    public IReadOnlyList<PlayerRosterEntry> Snapshot { get; } = roster.Entries;

    /// <summary>チャンネル番号。チャンネルの無い場所では 0。</summary>
    public uint MapChannel { get; } = roster.MapChannel;

    public string MapName { get; } = roster.MapName;

    public long MapGeneration { get; } = roster.MapGeneration;
}
