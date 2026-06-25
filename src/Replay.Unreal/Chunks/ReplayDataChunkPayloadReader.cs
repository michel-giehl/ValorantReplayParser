using Replay.Encoding.Archives;
using Replay.Encoding.Compression;
using Replay.Models.Errors;
using Replay.Models.Replay;

namespace Replay.Unreal.Chunks;

public sealed class ReplayDataChunkPayloadReader
{
    private readonly IOodleDecompressor? _oodleDecompressor;
    private const int MaxChunkSize = 1024 * 1024 * 256; // Max 256 MB

    public ReplayDataChunkPayloadReader(IOodleDecompressor? oodleDecompressor = null)
    {
        _oodleDecompressor = oodleDecompressor;
    }

    public FBinaryArchive ReadPayload(ReplayInfo info, ReplayDataChunkInfo dataChunk, FBinaryArchive chunkArchive)
    {
        ArgumentNullException.ThrowIfNull(info);
        ArgumentNullException.ThrowIfNull(dataChunk);
        ArgumentNullException.ThrowIfNull(chunkArchive);

        if (info.Encrypted)
        {
            throw new InvalidReplayInfoException("Encrypted VALORANT replay-data chunks are not supported.");
        }

        if (dataChunk.MemorySizeInBytes is < 0 or > MaxChunkSize)
        {
            throw new InvalidReplayInfoException(
                $"Replay-data memory size {dataChunk.MemorySizeInBytes} is invalid.");
        }

        if (!info.Compressed)
        {
            if (dataChunk.SizeInBytes != dataChunk.MemorySizeInBytes)
            {
                throw new InvalidReplayInfoException(
                    $"Uncompressed replay-data chunk size {dataChunk.SizeInBytes} does not match memory size {dataChunk.MemorySizeInBytes}.");
            }

            return new FBinaryArchive(chunkArchive.ReadBytes(dataChunk.SizeInBytes));
        }

        if (_oodleDecompressor is null)
        {
            throw new InvalidReplayInfoException("Replay data is compressed but no Oodle decompressor is configured.");
        }

        if (dataChunk.SizeInBytes < 8)
        {
            throw new InvalidReplayInfoException(
                $"Compressed replay-data chunk size {dataChunk.SizeInBytes} is too small for an Oodle archive header.");
        }

        var decompressedSize = chunkArchive.ReadInt32();
        var compressedSize = chunkArchive.ReadInt32();
        if (decompressedSize != dataChunk.MemorySizeInBytes)
        {
            throw new InvalidReplayInfoException(
                $"Oodle archive decompressed size {decompressedSize} does not match replay-data memory size {dataChunk.MemorySizeInBytes}.");
        }

        var expectedCompressedSize = dataChunk.SizeInBytes - 8;
        if (compressedSize != expectedCompressedSize)
        {
            throw new InvalidReplayInfoException(
                $"Oodle archive compressed size {compressedSize} does not match replay-data payload size {expectedCompressedSize}.");
        }

        var compressedSource = chunkArchive.ReadBytes(compressedSize).Span;
        var decompressed = _oodleDecompressor.Decompress(compressedSource, dataChunk.MemorySizeInBytes);
        if (decompressed.Length != dataChunk.MemorySizeInBytes)
        {
            throw new OodleDecompressionException(
                $"Oodle decompressed {decompressed.Length} bytes; expected {dataChunk.MemorySizeInBytes}.");
        }

        return new FBinaryArchive(decompressed);
    }

}
