using Replay.Encoding.Archives;
using Replay.Encoding.Compression;
using Replay.Models;

namespace Replay.Unreal;

public sealed class ReplayDataStreamMaterializer
{
    private readonly FBinaryArchive _archive;
    private readonly IOodleDecompressor? _oodleDecompressor;

    public ReplayDataStreamMaterializer(FBinaryArchive archive, IOodleDecompressor? oodleDecompressor = null)
    {
        _archive = archive;
        _oodleDecompressor = oodleDecompressor;
    }

    public FBinaryArchive Materialize(ReplayInfo info)
    {
        ArgumentNullException.ThrowIfNull(info);

        if (info.TotalDataSizeInBytes < 0 || info.TotalDataSizeInBytes > int.MaxValue)
        {
            throw new InvalidReplayInfoException(
                $"Replay data stream size {info.TotalDataSizeInBytes} is outside the valid range 0..{int.MaxValue}.");
        }

        var output = new byte[checked((int)info.TotalDataSizeInBytes)];
        foreach (var dataChunk in info.DataChunks)
        {
            MaterializeChunk(info, dataChunk, output);
        }

        return new FBinaryArchive(output);
    }

    private void MaterializeChunk(ReplayInfo info, ReplayDataChunkInfo dataChunk, byte[] output)
    {
        ValidateDataChunk(dataChunk, output.Length);

        var destination = output.AsSpan(checked((int)dataChunk.StreamOffset), dataChunk.MemorySizeInBytes);

        if (info.Compressed)
        {
            _archive.Seek(dataChunk.ReplayDataOffset);
            if (dataChunk.SizeInBytes < 8)
            {
                throw new InvalidReplayInfoException(
                    $"Compressed replay-data chunk size {dataChunk.SizeInBytes} is too small for an Oodle archive header.");
            }

            var decompressedSize = _archive.ReadInt32();
            var compressedSize = _archive.ReadInt32();
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

            if (_oodleDecompressor is null)
            {
                throw new InvalidReplayInfoException("Replay data is compressed but no Oodle decompressor is configured.");
            }

            var compressedSource = _archive.ReadBytes(compressedSize).Span;
            var bytesWritten = _oodleDecompressor.Decompress(compressedSource, destination);
            if (bytesWritten != dataChunk.MemorySizeInBytes)
            {
                throw new OodleDecompressionException(
                    $"Oodle decompressed {bytesWritten} bytes; expected {dataChunk.MemorySizeInBytes}.");
            }

            return;
        }

        _archive.Seek(dataChunk.ReplayDataOffset);
        var uncompressedSource = _archive.ReadBytes(dataChunk.SizeInBytes).Span;
        if (dataChunk.SizeInBytes != dataChunk.MemorySizeInBytes)
        {
            throw new InvalidReplayInfoException(
                $"Uncompressed replay-data chunk size {dataChunk.SizeInBytes} does not match memory size {dataChunk.MemorySizeInBytes}.");
        }

        uncompressedSource.CopyTo(destination);
    }

    private static void ValidateDataChunk(ReplayDataChunkInfo dataChunk, int outputLength)
    {
        if (dataChunk.SizeInBytes < 0)
        {
            throw new InvalidReplayInfoException($"Replay-data chunk size {dataChunk.SizeInBytes} is negative.");
        }

        if (dataChunk.MemorySizeInBytes < 0)
        {
            throw new InvalidReplayInfoException(
                $"Replay-data memory size {dataChunk.MemorySizeInBytes} is negative.");
        }

        if (dataChunk.StreamOffset < 0 || dataChunk.StreamOffset > outputLength - dataChunk.MemorySizeInBytes)
        {
            throw new InvalidReplayInfoException(
                $"Replay-data stream range offset {dataChunk.StreamOffset} size {dataChunk.MemorySizeInBytes} exceeds stream size {outputLength}.");
        }
    }
}
