using Google.Protobuf;

namespace StarResonanceDps.Core.Protocols.Zdps;

internal enum ZdpsDungeonContextReadResult
{
    NoDungeonState,
    DungeonStateRead,
    InvalidPayload
}

internal readonly record struct ZdpsDungeonContextReadOutcome(
    ZdpsDungeonContextReadResult Result,
    bool IsInDungeon);

internal static class ZdpsDungeonContextReader
{
    private const uint SyncDungeonDataVDataTag = 10;
    private const uint DungeonSyncDataFlowInfoTag = 18;
    private const uint DungeonFlowInfoStateTag = 8;

    public static ZdpsDungeonContextReadOutcome Read(ReadOnlySpan<byte> payload)
    {
        try
        {
            var input = new CodedInputStream(payload.ToArray());
            while (input.ReadTag() is var tag && tag != 0)
            {
                if (tag == SyncDungeonDataVDataTag)
                {
                    return ReadDungeonSyncData(input.ReadBytes());
                }

                input.SkipLastField();
            }

            return new ZdpsDungeonContextReadOutcome(ZdpsDungeonContextReadResult.NoDungeonState, false);
        }
        catch (InvalidProtocolBufferException)
        {
            return new ZdpsDungeonContextReadOutcome(ZdpsDungeonContextReadResult.InvalidPayload, false);
        }
    }

    private static ZdpsDungeonContextReadOutcome ReadDungeonSyncData(ByteString payload)
    {
        var input = new CodedInputStream(payload.ToByteArray());
        while (input.ReadTag() is var tag && tag != 0)
        {
            if (tag == DungeonSyncDataFlowInfoTag)
            {
                return ReadDungeonFlowInfo(input.ReadBytes());
            }

            input.SkipLastField();
        }

        return new ZdpsDungeonContextReadOutcome(ZdpsDungeonContextReadResult.NoDungeonState, false);
    }

    private static ZdpsDungeonContextReadOutcome ReadDungeonFlowInfo(ByteString payload)
    {
        var input = new CodedInputStream(payload.ToByteArray());
        while (input.ReadTag() is var tag && tag != 0)
        {
            if (tag == DungeonFlowInfoStateTag)
            {
                return new ZdpsDungeonContextReadOutcome(
                    ZdpsDungeonContextReadResult.DungeonStateRead,
                    input.ReadInt32() != 0);
            }

            input.SkipLastField();
        }

        return new ZdpsDungeonContextReadOutcome(ZdpsDungeonContextReadResult.DungeonStateRead, false);
    }
}
