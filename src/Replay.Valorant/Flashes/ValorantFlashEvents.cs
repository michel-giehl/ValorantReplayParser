using Replay.Models.Events;
using Replay.Models.Unreal;

namespace Replay.Valorant.Flashes;

public enum ValorantFlashKind
{
    SkyeGuidingLight,
    KayoFlashDriveOverhand,
    KayoFlashDriveUnderhand,
    BreachFlashpoint,
    PhoenixCurveballLeft,
    PhoenixCurveballRight,
    VyseArcRose,
    YoruBlindside,
}

public enum ValorantFlashPathSampleSource
{
    SpawnTransform,
    ReplicatedMovement,
}

public enum ValorantFlashExplosionEvidence
{
    StopProjectileRpc,
    SkyeFlashSource,
    VyseFlashSource,
    ActorDestroyedFallback,
}

public enum ValorantFlashHitCorrelation
{
    CausingProjectile,
    CausingFlashSource,
    CausingActorOuter,
    BlindConfigAndUniqueExplosion,
    Unresolved,
}

public enum ValorantFlashDurationSource
{
    BlindManagerInitialDuration,
    EffectDataGameplayTag,
    Missing,
}

public sealed record ValorantFlashCast(
    float TimeSeconds,
    int PacketId,
    uint FlashActorNetGuid,
    ValorantFlashKind FlashKind,
    uint? CasterCharacterNetGuid,
    uint? CasterPlayerStateNetGuid,
    string? CasterSubject,
    FVector? Location,
    FRotator? Rotation,
    FVector? Velocity)
    : ReplayEvent(TimeSeconds, PacketId);

public sealed record ValorantFlashPathUpdated(
    float TimeSeconds,
    int PacketId,
    uint FlashActorNetGuid,
    ValorantFlashKind FlashKind,
    int SampleIndex,
    ValorantFlashPathSampleSource Source,
    FVector? Location,
    FRotator? Rotation,
    FVector? LinearVelocity,
    FVector? AngularVelocity,
    uint? ServerFrame)
    : ReplayEvent(TimeSeconds, PacketId);

public sealed record ValorantFlashExploded(
    float TimeSeconds,
    int PacketId,
    uint FlashActorNetGuid,
    ValorantFlashKind FlashKind,
    FVector? Location,
    ValorantFlashExplosionEvidence Evidence,
    double? MaxFlashDurationSeconds)
    : ReplayEvent(TimeSeconds, PacketId);

public sealed record ValorantFlashPlayerHit(
    float TimeSeconds,
    int PacketId,
    uint? FlashActorNetGuid,
    ValorantFlashKind? FlashKind,
    uint TargetCharacterNetGuid,
    uint? TargetPlayerStateNetGuid,
    string? TargetSubject,
    float? InitialDurationSeconds,
    uint? BlindId,
    ulong? EffectId,
    uint? BlindConfigNetGuid,
    uint? CausingActorNetGuid,
    float? StartNetMovementTime,
    ValorantFlashHitCorrelation Correlation,
    ValorantFlashDurationSource DurationSource)
    : ReplayEvent(TimeSeconds, PacketId);
