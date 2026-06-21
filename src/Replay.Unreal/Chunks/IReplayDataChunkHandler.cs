using Replay.Encoding.Archives;
using Replay.Models;

namespace Replay.Unreal;

public interface IReplayDataChunkHandler
{
    void Handle(ReplayReaderContext context, ReplayDataChunkInfo dataChunk, FBinaryArchive replayDataArchive);
}
