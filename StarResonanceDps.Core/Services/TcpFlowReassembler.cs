namespace StarResonanceDps.Core.Services;

internal sealed class TcpFlowReassembler
{
    private const int MaxBufferedSegmentCount = 256;
    private const int MaxBufferedBytes = 2 * 1024 * 1024;

    private readonly Dictionary<uint, byte[]> _pendingSegments = [];
    private readonly Action<uint, uint>? _onGapRecovery;
    private uint? _nextSequence;
    private int _pendingByteCount;

    public TcpFlowReassembler(Action<uint, uint>? onGapRecovery = null)
    {
        _onGapRecovery = onGapRecovery;
    }

    public void Reset()
    {
        _pendingSegments.Clear();
        _nextSequence = null;
        _pendingByteCount = 0;
    }

    public void AddSegment(uint sequence, bool synchronize, ReadOnlySpan<byte> payload, Action<byte[]> onData)
    {
        if (synchronize)
        {
            Reset();
            _nextSequence = unchecked(sequence + 1);
            sequence = _nextSequence.Value;
        }

        if (payload.IsEmpty)
        {
            return;
        }

        var data = payload.ToArray();
        if (_nextSequence is null)
        {
            _nextSequence = sequence;
        }

        var expected = _nextSequence.Value;
        if (SequenceIsBefore(sequence, expected))
        {
            var overlap = unchecked(expected - sequence);
            if (overlap >= (uint)data.Length)
            {
                return;
            }

            data = data.AsSpan(checked((int)overlap)).ToArray();
            sequence = expected;
        }

        if (sequence == expected)
        {
            Deliver(data, onData);
            DeliverPending(onData);
            return;
        }

        if (!SequenceIsBefore(sequence, expected))
        {
            AddPending(sequence, data);
            TryRecoverFromGap(onData);
        }
    }

    private void Deliver(byte[] data, Action<byte[]> onData)
    {
        if (data.Length == 0)
        {
            return;
        }

        onData(data);
        _nextSequence = unchecked(_nextSequence!.Value + (uint)data.Length);
    }

    private void DeliverPending(Action<byte[]> onData)
    {
        while (_nextSequence is { } expected && _pendingSegments.Remove(expected, out var data))
        {
            _pendingByteCount -= data.Length;
            Deliver(data, onData);
        }
    }

    private void AddPending(uint sequence, byte[] data)
    {
        if (_pendingSegments.TryGetValue(sequence, out var existing))
        {
            if (existing.Length >= data.Length)
            {
                return;
            }

            _pendingByteCount -= existing.Length;
        }

        _pendingSegments[sequence] = data;
        _pendingByteCount += data.Length;
    }

    private void TryRecoverFromGap(Action<byte[]> onData)
    {
        if (_nextSequence is null
            || (_pendingSegments.Count < MaxBufferedSegmentCount && _pendingByteCount < MaxBufferedBytes))
        {
            return;
        }

        var expected = _nextSequence.Value;
        uint? nearestSequence = null;
        uint nearestDistance = uint.MaxValue;

        foreach (var sequence in _pendingSegments.Keys)
        {
            var distance = unchecked(sequence - expected);
            if (distance == 0 || distance >= 0x80000000 || distance >= nearestDistance)
            {
                continue;
            }

            nearestDistance = distance;
            nearestSequence = sequence;
        }

        if (nearestSequence is null || !_pendingSegments.Remove(nearestSequence.Value, out var data))
        {
            return;
        }

        _pendingByteCount -= data.Length;
        _nextSequence = nearestSequence.Value;
        _onGapRecovery?.Invoke(expected, nearestSequence.Value);
        Deliver(data, onData);
        DeliverPending(onData);
    }

    private static bool SequenceIsBefore(uint left, uint right)
    {
        return unchecked((int)(left - right)) < 0;
    }
}
