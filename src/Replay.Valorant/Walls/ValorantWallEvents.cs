using Replay.Models.Events;
using Replay.Models.Unreal;

namespace Replay.Valorant.Walls;

public enum ValorantWallKind
{
    SageBarrierOrb,
    VyseShear,
}

public enum ValorantWallPlacementEvidence
{
    SageWallActorSpawned,
    VyseTrapAnchorsInitialized,
}

public enum ValorantWallActivationEvidence
{
    VyseDynamicCollisionEnabled,
}

public enum ValorantWallSegmentDestructionEvidence
{
    DisableCollisionRpc,
    ReplicatedNotAlive,
    ActorDestroyedFallback,
}

public enum ValorantWallDestructionEvidence
{
    SageRootActorDestroyed,
    VyseActiveWallActorDestroyed,
    VyseUnactivatedTrapDestroyed,
}

public sealed record ValorantWallPlaced(
    float TimeSeconds,
    int PacketId,
    uint WallActorNetGuid,
    ValorantWallKind WallKind,
    uint? CasterCharacterNetGuid,
    uint? CasterPlayerStateNetGuid,
    string? CasterSubject,
    FVector? Location,
    FRotator? Rotation,
    FVector? WallStart,
    FVector? WallEnd,
    FVector? ImpactPoint,
    FVector? ImpactNormal,
    ValorantWallPlacementEvidence Evidence)
    : ReplayEvent(TimeSeconds, PacketId);

public sealed record ValorantWallSegmentSpawned(
    float TimeSeconds,
    int PacketId,
    uint WallActorNetGuid,
    uint SegmentActorNetGuid,
    int SegmentIndex,
    FVector? Location,
    FRotator? Rotation)
    : ReplayEvent(TimeSeconds, PacketId);

public sealed record ValorantWallActivated(
    float TimeSeconds,
    int PacketId,
    uint WallActorNetGuid,
    uint ActiveWallActorNetGuid,
    ValorantWallKind WallKind,
    FVector? Location,
    FRotator? Rotation,
    FVector? WallStart,
    FVector? WallEnd,
    FVector? ImpactNormal,
    uint? TriggerCharacterNetGuid,
    uint? TriggerPlayerStateNetGuid,
    string? TriggerSubject,
    ValorantWallActivationEvidence Evidence)
    : ReplayEvent(TimeSeconds, PacketId);

public sealed record ValorantWallSegmentDestroyed(
    float TimeSeconds,
    int PacketId,
    uint WallActorNetGuid,
    uint SegmentActorNetGuid,
    int SegmentIndex,
    FVector? Location,
    float LifetimeSeconds,
    ValorantWallSegmentDestructionEvidence Evidence)
    : ReplayEvent(TimeSeconds, PacketId);

public sealed record ValorantWallDestroyed(
    float TimeSeconds,
    int PacketId,
    uint WallActorNetGuid,
    uint? ActiveWallActorNetGuid,
    ValorantWallKind WallKind,
    FVector? Location,
    float LifetimeSeconds,
    float? ActiveDurationSeconds,
    ValorantWallDestructionEvidence Evidence)
    : ReplayEvent(TimeSeconds, PacketId);
