using Replay.Encoding.Archives;

namespace Replay.Unreal.Contracts;

public interface IHaveArchive
{
    FBinaryArchive Archive { get; }
}
