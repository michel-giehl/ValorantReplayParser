using Replay.Models.Events;
using Replay.Models.Net;
using Replay.Models.Unreal;

namespace Replay.Valorant.Events.Footsteps;

public enum ValorantFootstepKind
{
    Footstep,
    JumpLaunch,
    JumpLand,
}

public sealed record ValorantFootstepReceived(
    float TimeSeconds,
    int PacketId,
    uint ActorNetGuid,
    uint ObjectNetGuid,
    uint ChannelIndex,
    ValorantFootstepKind Kind,
    uint? CharacterNetGuid,
    uint? PlayerStateNetGuid,
    string? Subject,
    FVector? Location,
    FRotator? Rotation,
    float? Surface,
    float? Intensity,
    float? StartMovementTime,
    NetObjectReference? EffectContainer)
    : ReplayEvent(TimeSeconds, PacketId);
