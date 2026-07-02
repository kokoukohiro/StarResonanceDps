using System.Diagnostics;

namespace StarResonanceDps.Core.Protocols.Game.Binary;

public class BlobType
{
    public virtual string DebugName => null;

    public BlobType()
    {

    }

    public BlobType(ref BlobReader blob)
    {
        Read(ref blob);
    }

    public void Read(ref BlobReader blob)
    {
        var tag = blob.ReadInt();
        if (tag != -2)
        {
            System.Diagnostics.Debug.WriteLine($"Invalid begin tag: {tag}");
            return;
        }

        var size = blob.ReadInt();
        if (size == -3)
        {
            return;
        }

        if (size < 0)
        {
            System.Diagnostics.Debug.WriteLine($"BlobType.Read size was unexpectedly negative! size = {size}");
            return;
        }

        var offset = blob.Offset;
        var index = blob.ReadInt();
        while (0 < index)
        {
            if (!ParseField(index, ref blob))
            {
                blob.Offset = offset + size;
            }

            index = blob.ReadInt();
        }

        if (index != -3)
        {
            Debug.WriteLine($"Invalid end tag {index} at {blob.Offset}");
        }
    }

    public virtual bool ParseField(int index, ref BlobReader blob)
    {
        return false;
    }

}
