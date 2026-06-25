using OozSharp;

namespace Replay.Encoding.Compression;

public sealed class OozSharpOodleDecompressor : IOodleDecompressor
{
    private readonly Kraken _kraken = new();

    public ReadOnlyMemory<byte> Decompress(ReadOnlySpan<byte> compressed, int decompressedSize)
    {
        try
        {
            var decompressed = _kraken.Decompress(compressed, decompressedSize);
            if (decompressed.Length != decompressedSize)
            {
                throw new OodleDecompressionException(
                    $"Oodle decompressed {decompressed.Length} bytes; expected {decompressedSize}.");
            }

            return decompressed;
        }
        catch (OodleDecompressionException)
        {
            throw;
        }
        catch (Exception exception)
        {
            throw new OodleDecompressionException("Oodle decompression failed.", exception);
        }
    }
}
