// This file is part of StarResonanceDps. See LICENSING.md for the license, additional permissions and additional terms.

using System.Collections.Frozen;
using System.Threading;
using System.Threading.Channels;
using StarResonanceDps.Core.CombatRuntime;
using StarResonanceDps.PluginSdk;

namespace StarResonanceDps.App.Services;

/// <summary>
/// 戦闘のできごとの受け手(<see cref="IPluginCombatEventService.ReadAsync"/> の繰り返し)。
/// Core の <see cref="CombatObservationFeed"/> を受け、受け手のいる種類だけ SDK の型へ写して受け手ごとの順番待ちに入れる。
/// 受け手のいる種類は <see cref="CombatObservationFeed.ActiveKinds"/> へ書き、受け手のいない種類は Core が何も作らない。
/// </summary>
internal sealed partial class PluginPacketHub
{
    private const PluginCombatEventKinds AllCombatEventKinds =
        PluginCombatEventKinds.Damage
        | PluginCombatEventKinds.SkillActivation
        | PluginCombatEventKinds.TakenHit
        | PluginCombatEventKinds.CombatLog
        | PluginCombatEventKinds.PlayerBuff
        | PluginCombatEventKinds.MonsterBuff
        | PluginCombatEventKinds.MonsterSkill
        | PluginCombatEventKinds.MonsterHpZero;

    private readonly List<CombatEventReader> _combatEventReaders = [];
    private FrozenDictionary<PluginCombatEventKinds, CombatEventReader[]> _combatEventReadersByKind =
        FrozenDictionary<PluginCombatEventKinds, CombatEventReader[]>.Empty;

    /// <summary>
    /// まだ取られていない戦闘のできごと1件を、勘定で何バイトと数えるか。できごと(と中に持つ記録・登場人物・文字列)1件の割り当ての大きさ。
    /// タイムラインの記録は一番大きい型(敵が付けたバフ、登場人物2人)の値。
    /// </summary>
    private static int GetPendingBytes(PluginCombatEventKinds kind) => kind switch
    {
        PluginCombatEventKinds.Damage => 176,
        PluginCombatEventKinds.SkillActivation => 96,
        PluginCombatEventKinds.TakenHit => 200,
        PluginCombatEventKinds.CombatLog => 248,
        PluginCombatEventKinds.PlayerBuff => 152,
        PluginCombatEventKinds.MonsterBuff => 152,
        PluginCombatEventKinds.MonsterSkill => 64,
        PluginCombatEventKinds.MonsterHpZero => 80,
        _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, "Unknown combat event kind.")
    };

    private void SubscribeCombatObservations()
    {
        CombatObservationFeed.DamageObserved += observation =>
            Deliver(PluginCombatEventKinds.Damage, observation, PluginCombatEventConverter.ToDamageEvent);
        CombatObservationFeed.SkillActivationObserved += observation =>
            Deliver(PluginCombatEventKinds.SkillActivation, observation, PluginCombatEventConverter.ToSkillActivationEvent);
        CombatObservationFeed.TakenHitObserved += observation =>
            Deliver(PluginCombatEventKinds.TakenHit, observation, PluginCombatEventConverter.ToTakenHitEvent);
        CombatObservationFeed.CombatLogObserved += observation =>
            Deliver(PluginCombatEventKinds.CombatLog, observation, PluginCombatEventConverter.ToCombatLogEvent);
        CombatObservationFeed.BuffObserved += observation =>
            Deliver(PluginCombatEventConverter.GetBuffEventKind(observation), observation, PluginCombatEventConverter.ToBuffEvent);
        CombatObservationFeed.MonsterSkillObserved += observation =>
            Deliver(PluginCombatEventKinds.MonsterSkill, observation, PluginCombatEventConverter.ToMonsterSkillEvent);
        CombatObservationFeed.MonsterHpZeroObserved += observation =>
            Deliver(PluginCombatEventKinds.MonsterHpZero, observation, PluginCombatEventConverter.ToMonsterHpZeroEvent);
    }

    /// <summary>パケット処理のスレッドで、その種類の受け手がいれば1回だけ写し、受け手ごとの順番待ちに入れる。通し番号はゲームのメッセージと共有。</summary>
    private void Deliver<TObservation>(
        PluginCombatEventKinds kind,
        TObservation observation,
        Func<long, TObservation, PluginCombatEvent> convert)
    {
        var readersByKind = Volatile.Read(ref _combatEventReadersByKind);
        if (!readersByKind.TryGetValue(kind, out var readers))
        {
            return;
        }

        var combatEvent = convert(Interlocked.Increment(ref _sequenceNumber), observation);
        var pendingBytes = GetPendingBytes(kind);
        foreach (var reader in readers)
        {
            reader.Enqueue(combatEvent, pendingBytes);
        }
    }

    private CombatEventReader? RegisterCombatEventReader(Account account, PluginCombatEventKinds kinds)
    {
        lock (_sync)
        {
            if (account.IsClosed)
            {
                return null;
            }

            var reader = new CombatEventReader(account, kinds);
            _combatEventReaders.Add(reader);
            RebuildCombatEventLookup();
            return reader;
        }
    }

    private void UnregisterCombatEventReader(CombatEventReader reader)
    {
        lock (_sync)
        {
            if (_combatEventReaders.Remove(reader))
            {
                RebuildCombatEventLookup();
            }
        }

        reader.Complete();
    }

    /// <summary>そのプラグインの受け手を外す(<see cref="_sync"/> の中)。外した受け手は、ロックの外で <see cref="CombatEventReader.Complete"/> する。</summary>
    private List<CombatEventReader> RemoveCombatEventReadersNoLock(Account account)
    {
        var removed = _combatEventReaders.Where(reader => reader.Account == account).ToList();
        if (removed.Count > 0)
        {
            _combatEventReaders.RemoveAll(reader => reader.Account == account);
            RebuildCombatEventLookup();
        }

        return removed;
    }

    private void RebuildCombatEventLookup()
    {
        var lookup = new Dictionary<PluginCombatEventKinds, List<CombatEventReader>>();
        var activeKinds = PluginCombatEventKinds.None;
        foreach (var reader in _combatEventReaders)
        {
            activeKinds |= reader.Kinds;
            for (var bit = 1; bit <= (int)AllCombatEventKinds; bit <<= 1)
            {
                var kind = (PluginCombatEventKinds)bit;
                if ((reader.Kinds & kind) == 0)
                {
                    continue;
                }

                if (!lookup.TryGetValue(kind, out var readers))
                {
                    readers = [];
                    lookup.Add(kind, readers);
                }

                readers.Add(reader);
            }
        }

        Volatile.Write(ref _combatEventReadersByKind, lookup.ToFrozenDictionary(pair => pair.Key, pair => pair.Value.ToArray()));
        CombatObservationFeed.ActiveKinds = (CombatObservationKinds)(int)activeKinds;
    }

    public sealed partial class Account
    {
        public async IAsyncEnumerable<PluginCombatEvent> ReadAsync(
            PluginCombatEventKinds kinds,
            [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            if (kinds == PluginCombatEventKinds.None || (kinds & ~AllCombatEventKinds) != 0)
            {
                throw new ArgumentException("Pass one or more defined combat event kinds.", nameof(kinds));
            }

            if (_hub.RegisterCombatEventReader(this, kinds) is not { } reader)
            {
                yield break;
            }

            try
            {
                // 本体がプラグインを止めると受け手が外され(Close)、待ちは false で終わる。
                while (await reader.WaitToReadAsync(cancellationToken).ConfigureAwait(false))
                {
                    while (reader.TryRead(out var combatEvent))
                    {
                        yield return combatEvent;
                    }
                }
            }
            finally
            {
                _hub.UnregisterCombatEventReader(reader);
            }
        }
    }

    private sealed class CombatEventReader
    {
        // 外すとき(Complete)は、プラグインの繰り返しとは別のスレッドからも読んで捨てる。
        private readonly Channel<(PluginCombatEvent Event, int PendingBytes)> _channel =
            Channel.CreateUnbounded<(PluginCombatEvent Event, int PendingBytes)>();

        public CombatEventReader(Account account, PluginCombatEventKinds kinds)
        {
            Account = account;
            Kinds = kinds;
        }

        public Account Account { get; }

        public PluginCombatEventKinds Kinds { get; }

        public void Enqueue(PluginCombatEvent combatEvent, int pendingBytes)
        {
            if (_channel.Writer.TryWrite((combatEvent, pendingBytes)))
            {
                Account.AddPending(pendingBytes);
            }
        }

        public ValueTask<bool> WaitToReadAsync(CancellationToken cancellationToken)
        {
            return _channel.Reader.WaitToReadAsync(cancellationToken);
        }

        public bool TryRead(out PluginCombatEvent combatEvent)
        {
            if (!_channel.Reader.TryRead(out var item))
            {
                combatEvent = null!;
                return false;
            }

            Account.RemovePending(item.PendingBytes);
            combatEvent = item.Event;
            return true;
        }

        /// <summary>受け手を外したときに、繰り返しを終わらせ、溜まった分を捨てる。</summary>
        public void Complete()
        {
            _channel.Writer.TryComplete();
            while (_channel.Reader.TryRead(out var item))
            {
                Account.RemovePending(item.PendingBytes);
            }
        }
    }
}
