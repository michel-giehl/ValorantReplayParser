namespace Replay.Encoding.Compression;

public interface IOodleDecompressor
{
    int Decompress(ReadOnlySpan<byte> compressed, Span<byte> destination);
}
