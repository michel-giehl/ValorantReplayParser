using JetBrains.Annotations;
using Replay.Models.Descriptors;

namespace Replay.Models.Events;

[UsedImplicitly]
public sealed record ExportGroupReceived(
    float TimeSeconds,
    int PacketId,
    uint ActorNetGuid,
    uint ObjectNetGuid,
    uint ChannelIndex,
    bool IsActor,
    bool IsDeleted,
    byte DeleteFlags,
    string? ExportGroupPath,
    ExportGroupKind Kind,
    ExportCategory Categories,
    uint ClassNetGuid,
    uint OuterNetGuid,
    string? ObjectPath,
    string? ClassPath,
    string? OuterPath,
    int PayloadBits,
    int ParsedBits,
    bool WasDecoded,
    IReadOnlyList<DecodedReplayField> Fields)
    : ReplayEvent(TimeSeconds, PacketId);