using Google.Protobuf;

namespace StarResonanceDps.Core.Protocols.Game;

internal enum GameProtoWireType : uint
{
    Varint = 0,
    Fixed64 = 1,
    LengthDelimited = 2,
    Fixed32 = 5
}

internal readonly record struct GameProtoField(
    uint Number,
    GameProtoWireType WireType,
    ulong VarintValue,
    byte[] Bytes);

internal static class GameWireReader
{
    public static bool TryReadFields(ReadOnlySpan<byte> payload, out IReadOnlyList<GameProtoField> fields)
    {
        var result = new List<GameProtoField>();
        try
        {
            var input = new CodedInputStream(payload.ToArray());
            while (input.ReadTag() is var tag && tag != 0)
            {
                var number = tag >> 3;
                var wireType = (GameProtoWireType)(tag & 0x7);
                switch (wireType)
                {
                    case GameProtoWireType.Varint:
                        result.Add(new GameProtoField(number, wireType, input.ReadUInt64(), Array.Empty<byte>()));
                        break;

                    case GameProtoWireType.Fixed64:
                        result.Add(new GameProtoField(number, wireType, input.ReadFixed64(), Array.Empty<byte>()));
                        break;

                    case GameProtoWireType.LengthDelimited:
                        result.Add(new GameProtoField(number, wireType, 0, input.ReadBytes().ToByteArray()));
                        break;

                    case GameProtoWireType.Fixed32:
                        result.Add(new GameProtoField(number, wireType, input.ReadFixed32(), Array.Empty<byte>()));
                        break;

                    default:
                        input.SkipLastField();
                        break;
                }
            }
        }
        catch (InvalidProtocolBufferException)
        {
            fields = Array.Empty<GameProtoField>();
            return false;
        }

        fields = result;
        return true;
    }

    public static IEnumerable<byte[]> GetMessages(IReadOnlyList<GameProtoField> fields, uint fieldNumber)
    {
        return fields
            .Where(field => field.Number == fieldNumber && field.WireType == GameProtoWireType.LengthDelimited)
            .Select(field => field.Bytes);
    }

    public static bool TryGetMessage(IReadOnlyList<GameProtoField> fields, uint fieldNumber, out byte[] value)
    {
        foreach (var field in fields)
        {
            if (field.Number == fieldNumber && field.WireType == GameProtoWireType.LengthDelimited)
            {
                value = field.Bytes;
                return value.Length != 0;
            }
        }

        value = Array.Empty<byte>();
        return false;
    }

    public static bool TryGetVarint(IReadOnlyList<GameProtoField> fields, uint fieldNumber, out ulong value)
    {
        foreach (var field in fields)
        {
            if (field.Number == fieldNumber && field.WireType == GameProtoWireType.Varint)
            {
                value = field.VarintValue;
                return true;
            }
        }

        value = 0;
        return false;
    }

    public static int ReadRawInt32(byte[] rawData)
    {
        try
        {
            return new CodedInputStream(rawData).ReadInt32();
        }
        catch (InvalidProtocolBufferException)
        {
            return 0;
        }
    }

    public static long ReadRawInt64(byte[] rawData)
    {
        try
        {
            return new CodedInputStream(rawData).ReadInt64();
        }
        catch (InvalidProtocolBufferException)
        {
            return 0;
        }
    }

    public static uint ReadRawUInt32(byte[] rawData)
    {
        try
        {
            return new CodedInputStream(rawData).ReadUInt32();
        }
        catch (InvalidProtocolBufferException)
        {
            return 0;
        }
    }

    public static string ReadRawString(byte[] rawData)
    {
        try
        {
            return new CodedInputStream(rawData).ReadString().TrimEnd();
        }
        catch (InvalidProtocolBufferException)
        {
            return string.Empty;
        }
    }
}
