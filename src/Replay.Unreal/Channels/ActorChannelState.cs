using Replay.Encoding.Net;
using Replay.Models.Net;
using Replay.Models.Unreal;

namespace Replay.Unreal.Channels;

public sealed class ActorChannelState
{
    public uint ChannelIndex { get; init; }
    public bool IsOpen { get; set; }
    public bool IsDormant { get; set; }
    public NetworkGuid ActorNetGuid { get; set; }
    public NetworkGuid ArchetypeNetGuid { get; set; }
    public NetworkGuid LevelGuid { get; set; }
    public string? ActorPath { get; set; }
    public string? ArchetypePath { get; set; }
    public string? ReplicationClassPath { get; set; }
    public FVector? SpawnLocation { get; set; }
    public FRotator? SpawnRotation { get; set; }
    public FVector? SpawnScale { get; set; }
    public FVector? SpawnVelocity { get; set; }
    public int OpenPacketId { get; set; }
    public float OpenTimeSeconds { get; set; }
    public int? ClosePacketId { get; set; }
    public float? CloseTimeSeconds { get; set; }
    public ChannelCloseReason? CloseReason { get; set; }
    public HashSet<uint> SubobjectNetGuids { get; } = [];
}
