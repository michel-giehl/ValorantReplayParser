using Replay.Encoding.Net;

namespace Replay.Unreal;

public sealed class ObjectState
{
    public required NetworkGuid NetGuid { get; init; }
    public required NetworkGuid ActorNetGuid { get; init; }
    public uint ChannelIndex { get; set; }
    public bool IsStablyNamed { get; set; }
    public bool IsActive { get; set; }
    public NetworkGuid ClassNetGuid { get; set; }
    public NetworkGuid OuterNetGuid { get; set; }
    public string? ObjectPath { get; set; }
    public string? ClassPath { get; set; }
    public string? OuterPath { get; set; }
    public float FirstObservedTimeSeconds { get; init; }
    public int FirstObservedPacketId { get; init; }
    public float? CreatedTimeSeconds { get; set; }
    public int? CreatedPacketId { get; set; }
    public float? DestroyTimeSeconds { get; set; }
    public int? DestroyPacketId { get; set; }
    public byte DeleteFlags { get; set; }
}
