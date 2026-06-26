using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Replay.Encoding.Archives;
using Replay.Encoding.Compression;
using Replay.Models.Errors;
using Replay.Models.Replay;
using Replay.Unreal.Header;
using Replay.Unreal.Readers;

namespace Replay.Unreal.Chunks;

public sealed class ReplayChunkDispatcher
{
    private readonly ReplayDataChunkPayloadReader _replayDataChunkPayloadReader;
    private readonly IReplayDataChunkHandler _replayDataChunkHandler;

    public ReplayChunkDispatcher(
        IOodleDecompressor? oodleDecompressor = null,
        IReplayDataChunkHandler? replayDataChunkHandler = null)
    {
        _replayDataChunkPayloadReader = new ReplayDataChunkPayloadReader(oodleDecompressor);
        _replayDataChunkHandler = replayDataChunkHandler ?? new PlaybackPacketReplayDataChunkHandler();
    }

    public void DispatchAll(ReplayReaderContext context)
    {
        var logger = context.LoggerFactory?.CreateLogger<ReplayChunkDispatcher>()
            ?? NullLogger<ReplayChunkDispatcher>.Instance;

        while (!context.Archive.AtEnd)
        {
            DispatchNext(context, logger);
        }
    }

    private void DispatchNext(ReplayReaderContext context, ILogger<ReplayChunkDispatcher> logger)
    {
        try
        {
            DispatchNextCore(context, logger);
        }
        catch (ArchiveReadException exception)
        {
            throw new InvalidReplayInfoException(
                $"Error while parsing replay chunk: {exception.Message}", exception);
        }
        catch (OverflowException exception)
        {
            throw new InvalidReplayInfoException(
                $"Error while parsing replay chunk: {exception.Message}", exception);
        }
    }

    private void DispatchNextCore(ReplayReaderContext context, ILogger<ReplayChunkDispatcher> logger)
    {
        var typeOffset = context.Archive.Position;
        var chunkType = (ReplayChunkType)context.Archive.ReadUInt32();
        var chunk = new ReplayChunkInfo
        {
            ChunkType = chunkType,
            SizeInBytes = context.Archive.ReadInt32(),
            TypeOffset = typeOffset,
            DataOffset = context.Archive.Position,
        };

        context.ReplayInfo.Chunks.Add(chunk);
        var chunkIndex = context.ReplayInfo.Chunks.Count - 1;
        var chunkBytes = context.Archive.ReadBytes(chunk.SizeInBytes);
        var chunkArchive = new FBinaryArchive(chunkBytes);
        var chunkEndOffset = checked(chunk.DataOffset + chunk.SizeInBytes);

// Logs are cheap and only get called a few hundred times
#pragma warning disable CA1873
        logger.LogDebug("Dispatching replay chunk {ChunkIndex} of type {ChunkType}.", chunkIndex, chunkType);

        switch (chunkType)
        {
            case ReplayChunkType.Header:
                DispatchHeader(context, chunkIndex, chunkArchive);
                break;
            case ReplayChunkType.Checkpoint:
                logger.LogDebug("Skipping checkpoint chunk {ChunkIndex}.", chunkIndex);
                break;
            case ReplayChunkType.Event:
                logger.LogDebug("Skipping event chunk {ChunkIndex}.", chunkIndex);
                break;
            case ReplayChunkType.ReplayData:
                DispatchReplayData(context, chunkIndex, chunkArchive, chunk.DataOffset);
                break;
            case ReplayChunkType.Unknown:
            default:
                logger.LogDebug("Skipping unknown replay chunk {ChunkIndex} of type {ChunkType}.", chunkIndex,
                    chunkType);
                break;
        }
#pragma warning restore CA1873
        context.Archive.Seek(chunkEndOffset);
    }

    private static void DispatchHeader(ReplayReaderContext context, int chunkIndex, FBinaryArchive chunkArchive)
    {
        if (context.ReplayInfo.HeaderChunkIndex != ReplayInfo.NoChunkIndex)
        {
            throw new InvalidReplayInfoException("Replay info contains multiple header chunks.");
        }

        context.ReplayInfo.HeaderChunkIndex = chunkIndex;
        var result = new ReplayHeaderReader(chunkArchive).Read();
        context.ReplayHeader = result.Header;
        context.ReplayVersion = result.ReplayVersion;
        context.UEVersion = result.UEVersion;
    }

    private void DispatchReplayData(
        ReplayReaderContext context,
        int chunkIndex,
        FBinaryArchive chunkArchive,
        long chunkDataOffset)
    {
        var dataChunk = new ReplayDataChunkInfo
        {
            ChunkIndex = chunkIndex,
            StreamOffset = context.ReplayInfo.TotalDataSizeInBytes,
            Time1 = chunkArchive.ReadUInt32(),
            Time2 = chunkArchive.ReadUInt32(),
            SizeInBytes = chunkArchive.ReadInt32(),
            MemorySizeInBytes = chunkArchive.ReadInt32(),
            ReplayDataOffset = chunkDataOffset + chunkArchive.Position,
        };

        if (dataChunk.MemorySizeInBytes < 0)
        {
            throw new InvalidReplayInfoException(
                $"Replay-data memory size {dataChunk.MemorySizeInBytes} is negative.");
        }

        context.ReplayInfo.TotalDataSizeInBytes =
            checked(context.ReplayInfo.TotalDataSizeInBytes + dataChunk.MemorySizeInBytes);
        context.ReplayInfo.DataChunks.Add(dataChunk);

        var replayDataArchive = _replayDataChunkPayloadReader.ReadPayload(context.ReplayInfo, dataChunk, chunkArchive);
        context.ReplayDataStream = replayDataArchive;
        _replayDataChunkHandler.Handle(context, dataChunk, replayDataArchive);
    }
}
