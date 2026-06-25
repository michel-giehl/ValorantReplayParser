namespace Replay.Encoding.Compression;

public interface IOodleDecompressor
{
    ReadOnlyMemory<byte> Decompress(ReadOnlySpan<byte> compressed, int decompressedSize);
}
