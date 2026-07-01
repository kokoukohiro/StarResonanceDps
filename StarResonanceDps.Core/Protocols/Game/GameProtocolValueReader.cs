using Google.Protobuf;

namespace StarResonanceDps.Core.Protocols.Game;

internal static class GameProtocolValueReader
{
    public static int ReadRawInt32(ByteString rawData)
    {
        return rawData.IsEmpty ? 0 : new CodedInputStream(rawData.ToByteArray()).ReadInt32();
    }

    public static long ReadRawInt64(ByteString rawData)
    {
        return rawData.IsEmpty ? 0L : new CodedInputStream(rawData.ToByteArray()).ReadInt64();
    }

    public static uint ReadRawUInt32(ByteString rawData)
    {
        return rawData.IsEmpty ? 0U : new CodedInputStream(rawData.ToByteArray()).ReadUInt32();
    }

    public static string ReadRawString(ByteString rawData)
    {
        return rawData.IsEmpty ? string.Empty : new CodedInputStream(rawData.ToByteArray()).ReadString();
    }
}
