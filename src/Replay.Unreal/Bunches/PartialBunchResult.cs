using Replay.Models.Net;

namespace Replay.Unreal.Bunches;

internal readonly struct PartialBunchResult
{
    public RawBunchHeader Header { get; init; }

    public bool ShouldProcessCompletePayload { get; init; }
}