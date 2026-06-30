using Replay.Models.Events;
using Replay.Models.Net;
using Replay.Unreal.Channels;
using Replay.Unreal.Readers;

namespace Replay.Unreal.Bunches;

internal sealed class ActorChannelLifecycleService : IActorChannelLifecycleService
{
    private readonly ReplayReaderContext _context;

    public ActorChannelLifecycleService(ReplayReaderContext context)
    {
        _context = context;
    }

    public void OpenActor(ActorChannelState channel, BunchPayloadStats stats)
    {
        var actorNetGuid = channel.ActorNetGuid;
        if (!actorNetGuid.IsValid)
        {
            return;
        }

        _context.EventSink.Emit(new ActorSpawned(
            channel.OpenTimeSeconds,
            channel.OpenPacketId,
            actorNetGuid.Value,
            channel.ChannelIndex,
            actorNetGuid.IsDynamic,
            channel.ActorPath,
            channel.ArchetypeNetGuid.Value,
            channel.ArchetypePath,
            channel.ReplicationClassPath,
            channel.LevelGuid.Value,
            channel.SpawnLocation,
            channel.SpawnRotation,
            channel.SpawnScale,
            channel.SpawnVelocity));
    }

    public void CloseActorChannel(ActorChannelState channel, RawBunchHeader header, BunchPayloadStats stats)
    {
        if (!channel.IsOpen)
        {
            return;
        }

        channel.IsOpen = false;
        channel.IsDormant = header.bDormant;
        channel.ClosePacketId = header.PacketId;
        channel.CloseTimeSeconds = _context.CurrentTimeSeconds;
        channel.CloseReason = header.CloseReason;
        stats.ActorChannelCloseCount++;


        if (channel.ActorNetGuid.IsValid)
        {
            _context.EventSink.Emit(new ActorClosed(
                _context.CurrentTimeSeconds,
                header.PacketId,
                channel.ActorNetGuid.Value,
                channel.ChannelIndex,
                header.CloseReason));
        }
    }
}