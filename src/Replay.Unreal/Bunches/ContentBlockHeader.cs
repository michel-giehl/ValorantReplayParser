using Replay.Models.Net;

namespace Replay.Unreal.Bunches;

internal readonly struct ContentBlockHeader
{
    public bool HasRepLayout { get; init; }
    public bool IsActor { get; init; }
    public bool IsDeleted { get; init; }
    public NetworkGuid ObjectNetGuid { get; init; }
    public NetworkGuid ClassNetGuid { get; init; }
    public NetworkGuid OuterNetGuid { get; init; }
    public bool IsStablyNamed { get; init; }
    public byte DeleteFlags { get; init; }
}
