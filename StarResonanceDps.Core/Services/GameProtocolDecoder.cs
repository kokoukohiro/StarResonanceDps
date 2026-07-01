using System.Buffers.Binary;
using ZstdSharp;

namespace StarResonanceDps.Core.Services;

internal sealed class GameProtocolDecoder : IDisposable
{
    private const int HeaderLength = 6;
    private const int NotifyHeaderLength = 16;
    private const int FrameDownSequenceLength = 4;
    private const int MaxFrameLength = 16 * 1024 * 1024;
    private const int MaxBufferedLength = MaxFrameLength * 2;
    private const int DecompressionScratchLength = 4 * 1024 * 1024;

    private readonly Action<ulong, uint, byte[]> _onNotify;
    private readonly Action<string, Exception?>? _onDiagnosticFailure;
    private byte[] _buffer = new byte[8192];
    private int _bufferOffset;
    private int _bufferCount;
    private Decompressor? _decompressor;
    private byte[]? _decompressionScratch;

    public GameProtocolDecoder(
        Action<ulong, uint, byte[]> onNotify,
        Action<string, Exception?>? onDiagnosticFailure = null)
    {
        _onNotify = onNotify;
        _onDiagnosticFailure = onDiagnosticFailure;
    }

    public void Reset()
    {
        _bufferOffset = 0;
        _bufferCount = 0;
    }

    public void Append(ReadOnlySpan<byte> data)
    {
        if (data.IsEmpty)
        {
            return;
        }

        if (!EnsureCapacity(data.Length))
        {
            _onDiagnosticFailure?.Invoke(
                "Protocol decoder buffer capacity was exceeded. Buffered data was discarded.",
                null);
            Reset();
            return;
        }

        data.CopyTo(_buffer.AsSpan(_bufferOffset + _bufferCount));
        _bufferCount += data.Length;
        ProcessBufferedFrames();
    }

    public void Dispose()
    {
        _decompressor?.Dispose();
        _decompressor = null;
        _decompressionScratch = null;
    }

    private bool EnsureCapacity(int incomingLength)
    {
        if (incomingLength > MaxBufferedLength || _bufferCount + incomingLength > MaxBufferedLength)
        {
            return false;
        }

        var requiredLength = _bufferOffset + _bufferCount + incomingLength;
        if (requiredLength <= _buffer.Length)
        {
            return true;
        }

        if (_bufferCount + incomingLength <= _buffer.Length)
        {
            _buffer.AsSpan(_bufferOffset, _bufferCount).CopyTo(_buffer);
            _bufferOffset = 0;
            return true;
        }

        var nextLength = _buffer.Length;
        while (nextLength < _bufferCount + incomingLength)
        {
            nextLength *= 2;
        }

        if (nextLength > MaxBufferedLength)
        {
            nextLength = MaxBufferedLength;
        }

        if (nextLength < _bufferCount + incomingLength)
        {
            return false;
        }

        var next = new byte[nextLength];
        _buffer.AsSpan(_bufferOffset, _bufferCount).CopyTo(next);
        _buffer = next;
        _bufferOffset = 0;
        return true;
    }

    private void ProcessBufferedFrames()
    {
        while (_bufferCount >= HeaderLength)
        {
            var data = _buffer.AsSpan(_bufferOffset, _bufferCount);
            if (!TryReadFrameHeader(data, out var frameLength, out _))
            {
                Consume(1);
                continue;
            }

            if (_bufferCount < frameLength)
            {
                return;
            }

            ProcessFrame(data[..frameLength]);
            Consume(frameLength);
        }
    }

    private static bool TryReadFrameHeader(ReadOnlySpan<byte> data, out int frameLength, out ushort rawMessageType)
    {
        frameLength = 0;
        rawMessageType = 0;

        if (data.Length < HeaderLength)
        {
            return false;
        }

        var declaredLength = BinaryPrimitives.ReadUInt32BigEndian(data);
        if (declaredLength < HeaderLength || declaredLength > MaxFrameLength)
        {
            return false;
        }

        rawMessageType = BinaryPrimitives.ReadUInt16BigEndian(data[4..]);
        var messageType = rawMessageType & 0x7FFF;
        if (messageType > 8)
        {
            return false;
        }

        frameLength = checked((int)declaredLength);
        return true;
    }

    private void ProcessFrame(ReadOnlySpan<byte> frame)
    {
        if (!TryReadFrameHeader(frame, out _, out var rawMessageType))
        {
            return;
        }

        var compressed = (rawMessageType & 0x8000) != 0;
        var messageType = rawMessageType & 0x7FFF;
        var payload = frame[HeaderLength..];

        switch (messageType)
        {
            case 2:
                ProcessNotify(payload, compressed);
                break;

            case 6:
                ProcessFrameDown(payload, compressed);
                break;
        }
    }

    private void ProcessFrameDown(ReadOnlySpan<byte> payload, bool compressed)
    {
        if (payload.Length < FrameDownSequenceLength)
        {
            return;
        }

        var nestedFrames = payload[FrameDownSequenceLength..];
        if (compressed)
        {
            if (!TryDecompress(nestedFrames, out var decompressed))
            {
                return;
            }

            nestedFrames = decompressed;
        }

        ProcessFrameSequence(nestedFrames);
    }

    private void ProcessFrameSequence(ReadOnlySpan<byte> frames)
    {
        var offset = 0;
        while (frames.Length - offset >= HeaderLength)
        {
            var candidate = frames[offset..];
            if (!TryReadFrameHeader(candidate, out var frameLength, out _)
                || frameLength > candidate.Length)
            {
                return;
            }

            ProcessFrame(candidate[..frameLength]);
            offset += frameLength;
        }
    }

    private void ProcessNotify(ReadOnlySpan<byte> payload, bool compressed)
    {
        if (payload.Length < NotifyHeaderLength)
        {
            return;
        }

        var serviceId = BinaryPrimitives.ReadUInt64BigEndian(payload);
        var methodId = BinaryPrimitives.ReadUInt32BigEndian(payload[12..]);
        var message = payload[NotifyHeaderLength..];

        if (compressed)
        {
            if (!TryDecompress(message, out var decompressed))
            {
                return;
            }

            message = decompressed;
        }

        _onNotify(serviceId, methodId, message.ToArray());
    }

    private bool TryDecompress(ReadOnlySpan<byte> compressed, out ReadOnlySpan<byte> decompressed)
    {
        try
        {
            _decompressor ??= new Decompressor();
            _decompressionScratch ??= new byte[DecompressionScratchLength];
            var length = _decompressor.Unwrap(compressed, _decompressionScratch.AsSpan());
            decompressed = _decompressionScratch.AsSpan(0, length);
            return true;
        }
        catch (Exception exception)
        {
            _onDiagnosticFailure?.Invoke("Zstd decompression failed.", exception);
            decompressed = default;
            return false;
        }
    }

    private void Consume(int count)
    {
        _bufferOffset += count;
        _bufferCount -= count;

        if (_bufferCount == 0)
        {
            _bufferOffset = 0;
        }
    }
}
