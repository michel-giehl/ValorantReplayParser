using Replay.Encoding.Archives;
using Replay.Unreal.Readers;

namespace Replay.Unreal.Chunks;

internal interface IReplayDataChunkHandler
{
    void Handle(ReplayReaderContext context, FBinaryArchive replayDataArchive);
}
