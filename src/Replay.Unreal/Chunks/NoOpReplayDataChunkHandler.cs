using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Replay.Encoding.Archives;
using Replay.Models.Replay;
using Replay.Unreal.Readers;

namespace Replay.Unreal.Chunks;

public sealed class NoOpReplayDataChunkHandler : IReplayDataChunkHandler
{
    private readonly ILogger<NoOpReplayDataChunkHandler> _logger;

    public NoOpReplayDataChunkHandler(ILogger<NoOpReplayDataChunkHandler>? logger = null)
    {
        _logger = logger ?? NullLogger<NoOpReplayDataChunkHandler>.Instance;
    }

    public void Handle(ReplayReaderContext context, ReplayDataChunkInfo dataChunk, FBinaryArchive replayDataArchive)
    {
#pragma warning disable CA1873
        _logger.LogDebug(
            "Skipping replay-data chunk {ChunkIndex} with {ByteCount} decompressed bytes.",
            dataChunk.ChunkIndex,
            replayDataArchive.Length);
#pragma warning restore CA1873
    }
}
