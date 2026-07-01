using Replay.Encoding.Archives;
using Replay.Unreal.Packets;
using Replay.Unreal.Readers;

namespace Replay.Unreal.Chunks;

public sealed class PlaybackPacketReplayDataChunkHandler : IReplayDataChunkHandler
{
    public void Handle(ReplayReaderContext context, FBinaryArchive replayDataArchive)
    {
        var reader = new PlaybackPacketReader(
            context,
            replayDataArchive);
        reader.Read();
    }
}
