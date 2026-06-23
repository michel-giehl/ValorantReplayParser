using Replay.Encoding.Archives;
using Replay.Encoding.Net;

namespace Replay.Unreal.Parsing;

public static class PrimitiveDecoders
{
    public static readonly IFieldDecoder Int32 = new Int32Decoder();
    public static readonly IFieldDecoder UInt32 = new UInt32Decoder();
    public static readonly IFieldDecoder Float = new FloatDecoder();
    public static readonly IFieldDecoder Bool = new BoolDecoder();
    public static readonly IFieldDecoder Byte = new ByteDecoder();
    public static readonly IFieldDecoder ObjectNetGuid = new ObjectNetGuidDecoder();
    public static readonly IFieldDecoder Skip = new SkipDecoder();

    private sealed class Int32Decoder : IFieldDecoder
    {
        public void Decode(ref FieldDecodeContext context, FBitArchive archive)
        {
            _ = archive.ReadInt32();
        }
    }

    private sealed class UInt32Decoder : IFieldDecoder
    {
        public void Decode(ref FieldDecodeContext context, FBitArchive archive)
        {
            _ = archive.ReadUInt32();
        }
    }

    private sealed class FloatDecoder : IFieldDecoder
    {
        public void Decode(ref FieldDecodeContext context, FBitArchive archive)
        {
            _ = archive.ReadSingle();
        }
    }

    private sealed class BoolDecoder : IFieldDecoder
    {
        public void Decode(ref FieldDecodeContext context, FBitArchive archive)
        {
            _ = archive.ReadBit();
        }
    }

    private sealed class ByteDecoder : IFieldDecoder
    {
        public void Decode(ref FieldDecodeContext context, FBitArchive archive)
        {
            _ = archive.ReadByte();
        }
    }

    private sealed class ObjectNetGuidDecoder : IFieldDecoder
    {
        public void Decode(ref FieldDecodeContext context, FBitArchive archive)
        {
            var value = archive.ReadIntPacked();
            var guid = new NetworkGuid(value);
        }
    }

    private sealed class SkipDecoder : IFieldDecoder
    {
        public void Decode(ref FieldDecodeContext context, FBitArchive archive)
        {
            archive.SkipRemaining();
        }
    }
}
