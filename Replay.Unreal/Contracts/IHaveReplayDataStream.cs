using Replay.Encoding.Archives;

namespace Replay.Unreal.Contracts;

public interface IHaveReplayDataStream
{
    FBinaryArchive ReplayDataStream { get; set; }
}
