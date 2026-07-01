using Replay.Encoding.Archives;
using Replay.Unreal.Readers;

namespace Replay.Unreal.Chunks;

public interface IReplayDataChunkHandler
{
    void Handle(ReplayReaderContext context, FBinaryArchive replayDataArchive);
}
