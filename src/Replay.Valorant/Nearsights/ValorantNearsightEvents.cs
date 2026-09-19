using Replay.Models.Events;
using Replay.Models.Unreal;

namespace Replay.Valorant.Nearsights;

public enum ValorantNearsightKind
{
    OmenParanoia,
    ReynaLeer,
    HarborStormSurge,
}

public enum ValorantNearsightPathSampleSource
{
    SpawnTransform,
    ReplicatedMovement,
}

public enum ValorantNearsightActivationEvidence
{
    ProjectileActiveFromRelease,
    SourceActorSpawned,
}

public enum ValorantNearsightHitCorrelation
{
    EffectContextProjectile,
    EffectContextSource,
    EffectContextAbility,
    EffectContextOuter,
}

public enum ValorantNearsightDurationSource
{
    TargetEffectBuffDuration,
}

public sealed record ValorantNearsightCast(
    float TimeSeconds,
    int PacketId,
    uint NearsightActorNetGuid,
    ValorantNearsightKind NearsightKind,
    uint? CasterCharacterNetGuid,
    uint? CasterPlayerStateNetGuid,
    string? CasterSubject,
    FVector? Location,
    FRotator? Rotation,
    FVector? Velocity)
    : ReplayEvent(TimeSeconds, PacketId);

public sealed record ValorantNearsightPathUpdated(
    float TimeSeconds,
    int PacketId,
    uint NearsightActorNetGuid,
    ValorantNearsightKind NearsightKind,
    int SampleIndex,
    ValorantNearsightPathSampleSource Source,
    FVector? Location,
    FRotator? Rotation,
    FVector? LinearVelocity,
    FVector? AngularVelocity,
    uint? ServerFrame)
    : ReplayEvent(TimeSeconds, PacketId);

public sealed record ValorantNearsightActivated(
    float TimeSeconds,
    int PacketId,
    uint NearsightActorNetGuid,
    ValorantNearsightKind NearsightKind,
    uint? SourceActorNetGuid,
    FVector? Location,
    ValorantNearsightActivationEvidence Evidence)
    : ReplayEvent(TimeSeconds, PacketId);

public sealed record ValorantNearsightPlayerHit(
    float TimeSeconds,
    int PacketId,
    uint NearsightActorNetGuid,
    ValorantNearsightKind NearsightKind,
    uint TargetCharacterNetGuid,
    uint? TargetPlayerStateNetGuid,
    string? TargetSubject,
    float? ConfiguredDurationSeconds,
    bool DurationUntilRemoved,
    ulong? EffectId,
    uint? EffectContainerNetGuid,
    uint EffectContextNetGuid,
    float? StartNetMovementTime,
    ValorantNearsightHitCorrelation Correlation,
    ValorantNearsightDurationSource DurationSource)
    : ReplayEvent(TimeSeconds, PacketId);

public sealed record ValorantNearsightPlayerEffectEnded(
    float TimeSeconds,
    int PacketId,
    uint NearsightActorNetGuid,
    ValorantNearsightKind NearsightKind,
    uint TargetCharacterNetGuid,
    uint? TargetPlayerStateNetGuid,
    string? TargetSubject,
    ulong EffectId,
    float AppliedTimeSeconds,
    float? StartNetMovementTime,
    float? StopNetMovementTime,
    float ObservedDurationSeconds)
    : ReplayEvent(TimeSeconds, PacketId);
