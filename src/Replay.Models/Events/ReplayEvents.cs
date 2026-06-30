using Replay.Models.Descriptors;
using Replay.Models.Net;
using Replay.Models.Unreal;

namespace Replay.Models.Events;

public interface IReplayEventSink
{
    void Emit(ReplayEvent replayEvent);
}

public sealed class NullReplayEventSink : IReplayEventSink
{
    public static NullReplayEventSink Instance { get; } = new();

    private NullReplayEventSink()
    {
    }

    public void Emit(ReplayEvent replayEvent)
    {
    }
}

public abstract record ReplayEvent(float TimeSeconds, int PacketId);

public sealed record ActorSpawned(
    float TimeSeconds,
    int PacketId,
    uint ActorNetGuid,
    uint ChannelIndex,
    bool IsDynamic,
    string? ActorPath,
    uint ArchetypeNetGuid,
    string? ArchetypePath,
    string? ReplicationClassPath,
    uint LevelNetGuid,
    FVector? Location,
    FRotator? Rotation,
    FVector? Scale,
    FVector? Velocity)
    : ReplayEvent(TimeSeconds, PacketId);

public sealed record ActorClosed(
    float TimeSeconds,
    int PacketId,
    uint ActorNetGuid,
    uint ChannelIndex,
    ChannelCloseReason Reason)
    : ReplayEvent(TimeSeconds, PacketId);

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

public sealed record RpcReceived(
    float TimeSeconds,
    int PacketId,
    uint ActorNetGuid,
    uint ObjectNetGuid,
    uint ChannelIndex,
    string ClassPath,
    string FunctionName,
    string FunctionExportPath,
    int FunctionHandle,
    ExportCategory Categories,
    int PayloadBits,
    int ParsedBits,
    bool WasDecoded,
    IReadOnlyList<DecodedReplayField> Fields)
    : ReplayEvent(TimeSeconds, PacketId);

public sealed record DecodedReplayField(
    int Handle,
    string? Name,
    string? ExportName,
    ExportCategory Categories,
    DecodedFieldValue Value);