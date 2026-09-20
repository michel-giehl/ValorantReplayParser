using Replay.Models.Replay;

namespace Replay.Unreal.Parsing;

internal interface IReplayReleaseAwareFieldDecoder
{
    IFieldDecoder Resolve(ReplayReleaseVersion? releaseVersion);
}
