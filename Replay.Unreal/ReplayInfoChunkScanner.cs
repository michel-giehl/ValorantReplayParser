using Replay.Model;

namespace Replay.Unreal;

public sealed class ReplayInfoChunkScanner
{
    private readonly FBinaryArchive _archive;

    public ReplayInfoChunkScanner(FBinaryArchive archive)
    {
        _archive = archive;
    }

    public ReplayInfoChunkScanResult Scan(
        ReplayInfo info,
        ReplayInfoChunkScanFlags flags = ReplayInfoChunkScanFlags.None)
    {
        ArgumentNullException.ThrowIfNull(info);

        ResetChunkState(info);
        ScanChunks(info);

        long? headerChunkPayloadOffset = info.Chunks[info.HeaderChunkIndex].DataOffset;

        return new ReplayInfoChunkScanResult(headerChunkPayloadOffset);
    }

    private static void ResetChunkState(ReplayInfo info)
    {
        info.TotalDataSizeInBytes = 0;
        info.IsValid = false;
        info.HeaderChunkIndex = ReplayInfo.NoChunkIndex;
        info.Chunks.Clear();
        info.Checkpoints.Clear();
        info.Events.Clear();
        info.DataChunks.Clear();
    }

    private void ScanChunks(ReplayInfo info)
    {
        while (!_archive.AtEnd)
        {
            var typeOffset = _archive.Position;
            var chunkType = (ReplayChunkType)_archive.ReadUInt32();

            var chunk = new ReplayChunkInfo
            {
                ChunkType = chunkType,
                SizeInBytes = _archive.ReadInt32(),
                TypeOffset = typeOffset,
                DataOffset = _archive.Position,
            };

            info.Chunks.Add(chunk);
            var chunkBytes = _archive.ReadBytes(chunk.SizeInBytes);
            var chunkArchive = new FBinaryArchive(chunkBytes);
            var chunkEndOffset = checked(chunk.DataOffset + chunk.SizeInBytes);

            switch (chunkType)
            {
                case ReplayChunkType.Header:
                    ReadHeaderChunk(info, info.Chunks.Count - 1);
                    break;
                case ReplayChunkType.Checkpoint:
                    ReadCheckpointChunk(info, info.Chunks.Count - 1, chunkArchive, chunk.DataOffset);
                    break;
                case ReplayChunkType.ReplayData:
                    ReadReplayDataChunk(info, info.Chunks.Count - 1, chunkArchive, chunk.DataOffset);
                    break;
                case ReplayChunkType.Event:
                    ReadEventChunk(info, info.Chunks.Count - 1, chunkArchive, chunk.DataOffset);
                    break;
                case ReplayChunkType.Unknown:
                default:
                    break;
            }

            _archive.Seek(chunkEndOffset);
        }
    }

    private static void ReadHeaderChunk(ReplayInfo info, int chunkIndex)
    {
        if (info.HeaderChunkIndex != ReplayInfo.NoChunkIndex)
        {
            throw new InvalidReplayInfoException("Replay info contains multiple header chunks.");
        }

        info.HeaderChunkIndex = chunkIndex;
    }

    private static void ReadCheckpointChunk(
        ReplayInfo info,
        int chunkIndex,
        FBinaryArchive chunkArchive,
        long chunkDataOffset)
    {
        var checkpoint = ReadEventMetadata(chunkIndex, chunkArchive, chunkDataOffset);
        info.Checkpoints.Add(checkpoint);
    }

    private static void ReadEventChunk(
        ReplayInfo info,
        int chunkIndex,
        FBinaryArchive chunkArchive,
        long chunkDataOffset)
    {
        var replayEvent = ReadEventMetadata(chunkIndex, chunkArchive, chunkDataOffset);
        info.Events.Add(replayEvent);
    }

    private static ReplayEventInfo ReadEventMetadata(int chunkIndex, FBinaryArchive archive, long chunkDataOffset) => new()
    {
        ChunkIndex = chunkIndex,
        Id = archive.ReadFString(),
        Group = archive.ReadFString(),
        Metadata = archive.ReadFString(),
        Time1 = archive.ReadUInt32(),
        Time2 = archive.ReadUInt32(),
        SizeInBytes = archive.ReadInt32(),
        EventDataOffset = chunkDataOffset + archive.Position,
    };

    private static void ReadReplayDataChunk(
        ReplayInfo info,
        int chunkIndex,
        FBinaryArchive chunkArchive,
        long chunkDataOffset)
    {
        var dataChunk = new ReplayDataChunkInfo
        {
            ChunkIndex = chunkIndex,
            StreamOffset = info.TotalDataSizeInBytes,
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

        info.TotalDataSizeInBytes = checked(info.TotalDataSizeInBytes + dataChunk.MemorySizeInBytes);
        info.DataChunks.Add(dataChunk);
    }
}
