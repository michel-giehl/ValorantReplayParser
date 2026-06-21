using Replay.Encoding.Archives;
using Replay.Models.Replay;
using Replay.Unreal.Readers;

namespace Replay.Unreal.Chunks;

public interface IReplayDataChunkHandler
{
    void Handle(ReplayReaderContext context, ReplayDataChunkInfo dataChunk, FBinaryArchive replayDataArchive);
}
