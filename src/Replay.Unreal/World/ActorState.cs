using Replay.Encoding.Net;
using Replay.Models.Net;
using Replay.Models.Unreal;

namespace Replay.Unreal.World;

public sealed class ActorState
{
    public required NetworkGuid NetGuid { get; init; }
    public uint ChannelIndex { get; set; }
    public bool IsDynamic { get; init; }
    public ActorLifecycleStatus LifecycleStatus { get; set; }
    public string? ActorPath { get; set; }
    public NetworkGuid ArchetypeNetGuid { get; set; }
    public string? ArchetypePath { get; set; }
    public NetworkGuid LevelNetGuid { get; set; }
    public float FirstObservedTimeSeconds { get; init; }
    public int FirstObservedPacketId { get; init; }
    public float OpenTimeSeconds { get; set; }
    public int OpenPacketId { get; set; }
    public int OpenCount { get; set; }
    public float? SpawnTimeSeconds { get; init; }
    public int? SpawnPacketId { get; init; }
    public float? CloseTimeSeconds { get; set; }
    public int? ClosePacketId { get; set; }
    public ChannelCloseReason? CloseReason { get; set; }
    public float? DestroyTimeSeconds { get; set; }
    public int? DestroyPacketId { get; set; }
    public FVector? SpawnLocation { get; set; }
    public FRotator? SpawnRotation { get; set; }
    public FVector? SpawnScale { get; set; }
    public FVector? SpawnVelocity { get; set; }
    public FVector? Location { get; set; }
    public FRotator? Rotation { get; set; }
    public FVector? Scale { get; set; }
    public FVector? Velocity { get; set; }
    public NetworkGuid OwnerNetGuid { get; set; }
    public NetworkGuid InstigatorNetGuid { get; set; }
    public NetworkGuid CreatedByCharacterNetGuid { get; set; }
    public NetworkGuid AttachParentNetGuid { get; set; }
    public NetworkGuid AttachComponentNetGuid { get; set; }
    public HashSet<uint> SubobjectNetGuids { get; } = [];
}
