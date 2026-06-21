namespace Replay.Models;

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
        ArgumentNullException.ThrowIfNull(replayEvent);
    }
}

public abstract record ReplayEvent(float TimeSeconds, int PacketId);

public sealed record ActorOpened(
    float TimeSeconds,
    int PacketId,
    uint ActorNetGuid,
    uint ChannelIndex,
    bool IsDynamic,
    string? ActorPath,
    string? ArchetypePath)
    : ReplayEvent(TimeSeconds, PacketId);

public sealed record ActorSpawned(
    float TimeSeconds,
    int PacketId,
    uint ActorNetGuid,
    uint ChannelIndex,
    string? ArchetypePath,
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

public sealed record ActorDestroyed(
    float TimeSeconds,
    int PacketId,
    uint ActorNetGuid,
    uint ChannelIndex)
    : ReplayEvent(TimeSeconds, PacketId);

public sealed record SubobjectCreated(
    float TimeSeconds,
    int PacketId,
    uint ObjectNetGuid,
    uint ActorNetGuid,
    uint ChannelIndex,
    uint ClassNetGuid,
    uint OuterNetGuid,
    string? ObjectPath,
    string? ClassPath,
    bool IsStablyNamed)
    : ReplayEvent(TimeSeconds, PacketId);

public sealed record SubobjectDestroyed(
    float TimeSeconds,
    int PacketId,
    uint ObjectNetGuid,
    uint ActorNetGuid,
    uint ChannelIndex,
    byte DeleteFlags,
    bool DestroyedWithActor)
    : ReplayEvent(TimeSeconds, PacketId);
