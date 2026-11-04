using Replay.Models.Descriptors;
using Replay.Models.Events;

namespace Replay.Valorant.Events.Ownership;

/// <summary>
/// An export named who an actor belongs to, emitted just before that export. Owners arrive late and change, so an
/// actor can have several of these.
/// </summary>
public sealed record ValorantActorOwnerObserved(
    float TimeSeconds,
    int PacketId,
    uint ActorNetGuid,
    ActorOwnerReferences References)
    : ReplayEvent(TimeSeconds, PacketId)
{
    public static ValorantActorOwnerObserved? From(ExportGroupReceived export) =>
        ActorOwnerReferences.Read(export.Payload) is { IsEmpty: false } references
            ? new ValorantActorOwnerObserved(export.TimeSeconds, export.PacketId, export.ActorNetGuid, references)
            : null;
}
