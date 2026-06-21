using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Replay.Encoding.Archives;
using Replay.Models.Replay;
using Replay.Unreal.Packets;
using Replay.Unreal.Readers;

namespace Replay.Unreal.Chunks;

public sealed class PlaybackPacketReplayDataChunkHandler : IReplayDataChunkHandler
{
    private readonly ILogger<PlaybackPacketReplayDataChunkHandler> _logger;
    private readonly ILoggerFactory? _loggerFactory;

    public PlaybackPacketReplayDataChunkHandler(
        ILogger<PlaybackPacketReplayDataChunkHandler>? logger = null,
        ILoggerFactory? loggerFactory = null)
    {
        _logger = logger ?? NullLogger<PlaybackPacketReplayDataChunkHandler>.Instance;
        _loggerFactory = loggerFactory;
    }

    public void Handle(ReplayReaderContext context, ReplayDataChunkInfo dataChunk, FBinaryArchive replayDataArchive)
    {
        var reader = new PlaybackPacketReader(
            context,
            dataChunk,
            replayDataArchive,
            _loggerFactory?.CreateLogger<PlaybackPacketReader>(),
            _loggerFactory);
        _logger.LogDebug("Reading playback packets from replay-data chunk {ChunkIndex}.", dataChunk.ChunkIndex);
        reader.Read();
    }
}
