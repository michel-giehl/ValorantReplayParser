using OozSharp;

namespace Replay.Encoding.Compression;

public sealed class OozSharpOodleDecompressor : IOodleDecompressor
{
    private readonly Kraken _kraken = new();

    public int Decompress(ReadOnlySpan<byte> compressed, Span<byte> destination)
    {
        try
        {
            var decompressed = _kraken.Decompress(compressed, destination.Length);
            if (decompressed.Length > destination.Length)
            {
                throw new OodleDecompressionException(
                    $"Oodle decompressed {decompressed.Length} bytes into a {destination.Length} byte destination.");
            }

            decompressed.Span.CopyTo(destination);
            return decompressed.Length;
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
