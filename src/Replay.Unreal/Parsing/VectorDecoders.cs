using Replay.Encoding.Archives;
using Replay.Models.Descriptors;

namespace Replay.Unreal.Parsing;

public static class VectorDecoders
{
    public static readonly IFieldDecoder Vector100 = new QuantizedVectorDecoder(scaleFactor: 10);
    public static readonly IFieldDecoder Vector1 = new QuantizedVectorDecoder(scaleFactor: 1);
    public static readonly IFieldDecoder VectorDouble = new DoubleVectorDecoder();
    public static readonly IFieldDecoder VectorFloat = new FloatVectorDecoder();
    public static readonly IFieldDecoder RotationShort = new ShortRotationDecoder();

    private sealed class QuantizedVectorDecoder : IFieldDecoder
    {
        private readonly int _scaleFactor;

        public QuantizedVectorDecoder(int scaleFactor)
        {
            _scaleFactor = scaleFactor;
        }

        public DecodedFieldValue Decode(ref FieldDecodeContext context, FBitArchive archive)
        {
            var vector = archive.ReadBit()
                ? ArchiveVectorReaders.ReadQuantizedVector(archive, _scaleFactor)
                : ArchiveVectorReaders.ReadDoubleVector(archive);
            return DecodedFieldValue.FromVector(vector);
        }
    }

    private sealed class DoubleVectorDecoder : IFieldDecoder
    {
        public DecodedFieldValue Decode(ref FieldDecodeContext context, FBitArchive archive) =>
            DecodedFieldValue.FromVector(ArchiveVectorReaders.ReadDoubleVector(archive));
    }

    private sealed class FloatVectorDecoder : IFieldDecoder
    {
        public DecodedFieldValue Decode(ref FieldDecodeContext context, FBitArchive archive) =>
            DecodedFieldValue.FromVector(ArchiveVectorReaders.ReadFloatVector(archive));
    }

    private sealed class ShortRotationDecoder : IFieldDecoder
    {
        public DecodedFieldValue Decode(ref FieldDecodeContext context, FBitArchive archive) =>
            DecodedFieldValue.FromRotator(ArchiveVectorReaders.ReadRotationShort(archive));
    }
}
