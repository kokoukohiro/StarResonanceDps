namespace StarResonanceDps.PluginSdk;

/// <summary>The kind of a captured game message.</summary>
public enum PluginPacketKind
{
    /// <summary>A notification the server sends to the client.</summary>
    Notify,

    /// <summary>A method call the client sends to the server.</summary>
    Call,

    /// <summary>The server's reply to a <see cref="Call"/>.</summary>
    Return,
}

/// <summary>The direction of a captured game message.</summary>
public enum PluginPacketDirection
{
    /// <summary>From the server to the client.</summary>
    Down,

    /// <summary>From the client to the server.</summary>
    Up,
}

/// <summary>
/// A type of game message to read: the kind, the service ID and the method ID.
/// For <see cref="PluginPacketKind.Return"/>, the IDs are those of the call it replies to.
/// </summary>
public readonly record struct PluginPacketType(PluginPacketKind Kind, ulong ServiceId, uint MethodId);

/// <summary>
/// One captured game message. The payload is decompressed and otherwise as the game sent it (protobuf bytes).
/// Replies are the reply wrapper of the called method (<c>X_Ret</c>).
/// </summary>
public sealed class PluginPacketMessage
{
    public PluginPacketMessage(
        long sequenceNumber,
        PluginPacketKind kind,
        ulong serviceId,
        uint methodId,
        uint callId,
        ReadOnlyMemory<byte> payload,
        DateTime arrivalTimeUtc)
    {
        SequenceNumber = sequenceNumber;
        Kind = kind;
        ServiceId = serviceId;
        MethodId = methodId;
        CallId = callId;
        Payload = payload;
        ArrivalTimeUtc = arrivalTimeUtc;
    }

    /// <summary>
    /// The capture order. Numbered across all messages delivered to any plugin, so a plugin sees gaps
    /// for messages that only other plugins read.
    /// </summary>
    public long SequenceNumber { get; }

    public PluginPacketKind Kind { get; }

    public PluginPacketDirection Direction => Kind == PluginPacketKind.Call ? PluginPacketDirection.Up : PluginPacketDirection.Down;

    public ulong ServiceId { get; }

    public uint MethodId { get; }

    /// <summary>The number that links a <see cref="PluginPacketKind.Return"/> to its <see cref="PluginPacketKind.Call"/>. 0 for notifications.</summary>
    public uint CallId { get; }

    public ReadOnlyMemory<byte> Payload { get; }

    /// <summary>The capture time (UTC) of the packet that completed the message.</summary>
    public DateTime ArrivalTimeUtc { get; }

    public PluginPacketType Type => new(Kind, ServiceId, MethodId);
}
