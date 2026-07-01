using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Replay.Encoding.Archives;
using Replay.Unreal.Readers;

namespace Replay.Unreal.Chunks;

public sealed class NoOpReplayDataChunkHandler : IReplayDataChunkHandler
{
    public void Handle(ReplayReaderContext context, FBinaryArchive replayDataArchive)
    {
        var logger = context.LoggerFactory?.CreateLogger<NoOpReplayDataChunkHandler>()
                     ?? NullLogger<NoOpReplayDataChunkHandler>.Instance;

#pragma warning disable CA1873
        logger.LogDebug("Skipping replay-data chunk {ChunkIndex}.", replayDataArchive.Length);
#pragma warning restore CA1873
    }
}