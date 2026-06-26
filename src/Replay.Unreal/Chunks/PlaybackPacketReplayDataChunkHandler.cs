using Replay.Encoding.Archives;
using Replay.Models.Replay;
using Replay.Unreal.Packets;
using Replay.Unreal.Readers;

namespace Replay.Unreal.Chunks;

public sealed class PlaybackPacketReplayDataChunkHandler : IReplayDataChunkHandler
{
    public void Handle(ReplayReaderContext context, ReplayDataChunkInfo dataChunk, FBinaryArchive replayDataArchive)
    {
        var reader = new PlaybackPacketReader(
            context,
            dataChunk,
            replayDataArchive);
        reader.Read();
    }
}
