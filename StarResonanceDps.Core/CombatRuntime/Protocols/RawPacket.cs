using System.Buffers;

namespace StarResonanceDps.Core.CombatRuntime.Protocols;

public class RawPacket
{
    public byte[] Data { get; set; } = null!;
    public int Len { get; set; }
    public DateTime LastPacketTime { get; set; } = DateTime.MinValue;

    public void Set(int len)
    {
        Data = ArrayPool<byte>.Shared.Rent(len);
        Len = len;
    }

    public void Return()
    {
        ArrayPool<byte>.Shared.Return(Data);
    }
}
