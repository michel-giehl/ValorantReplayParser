using Replay.Encoding.Archives;
using Replay.Models.Descriptors;

namespace Replay.Unreal.Parsing;

public static class PrimitiveDecoders
{
    public static readonly IFieldDecoder Int32 = new Int32Decoder();
    public static readonly IFieldDecoder UInt32 = new UInt32Decoder();
    public static readonly IFieldDecoder Float = new FloatDecoder();
    public static readonly IFieldDecoder Bool = new BoolDecoder();
    public static readonly IFieldDecoder Byte = new ByteDecoder();
    public static readonly IFieldDecoder ObjectNetGuid = new ObjectNetGuidDecoder();
    public static readonly IFieldDecoder Vector = new DoubleVectorDecoder();
    public static readonly IFieldDecoder VectorFloat = new FloatVectorDecoder();
    public static readonly IFieldDecoder VectorDouble = Vector;
    public static readonly IFieldDecoder VectorNetQuantize = new QuantizedVectorDecoder(scaleFactor: 1);
    public static readonly IFieldDecoder VectorNetQuantize10 = new QuantizedVectorDecoder(scaleFactor: 10);
    public static readonly IFieldDecoder VectorNetQuantize100 = new QuantizedVectorDecoder(scaleFactor: 100);
    public static readonly IFieldDecoder VectorNetQuantizeNormal = new FixedNormalVectorDecoder();
    public static readonly IFieldDecoder Skip = new SkipDecoder();

    private sealed class FloatVectorDecoder : IFieldDecoder
    {
        public DecodedFieldValue Decode(ref FieldDecodeContext context, FBitArchive archive) =>
            DecodedFieldValue.FromVector(ArchiveVectorReaders.ReadFloatVector(archive));
    }

    private sealed class DoubleVectorDecoder : IFieldDecoder
    {
        public DecodedFieldValue Decode(ref FieldDecodeContext context, FBitArchive archive) =>
            DecodedFieldValue.FromVector(ArchiveVectorReaders.ReadDoubleVector(archive));
    }

    private sealed class QuantizedVectorDecoder : IFieldDecoder
    {
        private readonly int _scaleFactor;

        public QuantizedVectorDecoder(int scaleFactor)
        {
            _scaleFactor = scaleFactor;
        }

        public DecodedFieldValue Decode(ref FieldDecodeContext context, FBitArchive archive) =>
            DecodedFieldValue.FromVector(ArchiveVectorReaders.ReadQuantizedVector(archive, _scaleFactor));
    }

    private sealed class FixedNormalVectorDecoder : IFieldDecoder
    {
        public DecodedFieldValue Decode(ref FieldDecodeContext context, FBitArchive archive) =>
            DecodedFieldValue.FromVector(ArchiveVectorReaders.ReadFixedVectorNormal(archive));
    }

    private sealed class Int32Decoder : IFieldDecoder
    {
        public DecodedFieldValue Decode(ref FieldDecodeContext context, FBitArchive archive) =>
            DecodedFieldValue.FromInt32(archive.ReadInt32());
    }

    private sealed class UInt32Decoder : IFieldDecoder
    {
        public DecodedFieldValue Decode(ref FieldDecodeContext context, FBitArchive archive) =>
            DecodedFieldValue.FromUInt32(archive.ReadUInt32());
    }

    private sealed class FloatDecoder : IFieldDecoder
    {
        public DecodedFieldValue Decode(ref FieldDecodeContext context, FBitArchive archive) =>
            DecodedFieldValue.FromFloat(archive.ReadSingle());
    }

    private sealed class BoolDecoder : IFieldDecoder
    {
        public DecodedFieldValue Decode(ref FieldDecodeContext context, FBitArchive archive) =>
            DecodedFieldValue.FromBool(archive.ReadBit());
    }

    private sealed class ByteDecoder : IFieldDecoder
    {
        public DecodedFieldValue Decode(ref FieldDecodeContext context, FBitArchive archive) =>
            DecodedFieldValue.FromByte(archive.ReadByte());
    }

    private sealed class ObjectNetGuidDecoder : IFieldDecoder
    {
        public DecodedFieldValue Decode(ref FieldDecodeContext context, FBitArchive archive) =>
            DecodedFieldValue.FromNetGuid(archive.ReadIntPacked());
    }

    private sealed class SkipDecoder : IFieldDecoder
    {
        public DecodedFieldValue Decode(ref FieldDecodeContext context, FBitArchive archive)
        {
            archive.SkipRemaining();
            return DecodedFieldValue.None;
        }
    }
}
