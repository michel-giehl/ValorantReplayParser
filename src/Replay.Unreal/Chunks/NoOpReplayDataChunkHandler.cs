using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Replay.Encoding.Archives;
using Replay.Models.Replay;
using Replay.Unreal.Readers;

namespace Replay.Unreal.Chunks;

public sealed class NoOpReplayDataChunkHandler : IReplayDataChunkHandler
{
    public void Handle(ReplayReaderContext context, ReplayDataChunkInfo dataChunk, FBinaryArchive replayDataArchive)
    {
        var logger = context.LoggerFactory?.CreateLogger<NoOpReplayDataChunkHandler>()
            ?? NullLogger<NoOpReplayDataChunkHandler>.Instance;

#pragma warning disable CA1873
        logger.LogDebug(
            "Skipping replay-data chunk {ChunkIndex} with {ByteCount} decompressed bytes.",
            dataChunk.ChunkIndex,
            replayDataArchive.Length);
#pragma warning restore CA1873
    }
}
