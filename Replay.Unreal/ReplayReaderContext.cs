using Replay.Model;
using Replay.Model.Contracts;
using Replay.Unreal.Contracts;

namespace Replay.Unreal;

public class ReplayReaderContext : IHaveArchive, IHaveReplayHeader, IHaveReplayVersion, IHaveErrors
{
    public ReplayReaderContext(FBinaryArchive archive)
    {
        Archive = archive;
    }

    public FBinaryArchive Archive { get; }

    public ReplayHeader ReplayHeader { get; set; } = new UninitializedReplayHeader();
    public ReplayVersion ReplayVersion { get; set; } = new UninitializedReplayVersion { Branch = string.Empty };
    public UEVersion UEVersion { get; set; } = new UninitializedUEVersion();
    public List<ReplayParseError> Errors { get; } = [];
}