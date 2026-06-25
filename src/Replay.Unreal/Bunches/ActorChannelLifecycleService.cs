using Replay.Encoding.Net;
using Replay.Models.Errors;
using Replay.Models.Events;
using Replay.Models.Net;
using Replay.Unreal.Channels;
using Replay.Unreal.Readers;
using Replay.Unreal.World;

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

        var worldState = _context.WorldState;
        var isNew = !worldState.ActorsByNetGuid.TryGetValue(actorNetGuid.Value, out var actor);
        if (actor?.LifecycleStatus == ActorLifecycleStatus.Destroyed)
        {
            throw new InvalidReplayInfoException(
                $"Actor net GUID {actorNetGuid.Value} was reopened after destruction.");
        }

        if (isNew)
        {
            actor = CreateActorState(channel);
            worldState.ActorsByNetGuid.Add(actorNetGuid.Value, actor);
            stats.ActorCreatedCount++;
        }
        else
        {
            ReopenActor(actor!, channel);
        }

        EmitOpened(channel, actorNetGuid, actor!);
        EmitSpawnedIfNeeded(channel, actorNetGuid, actor!, isNew);
    }

    public void CloseActorChannel(ActorChannelState channel, RawBunchHeader header, BunchPayloadStats stats)
    {
        if (!channel.IsOpen)
        {
            return;
        }

        MarkChannelClosed(channel, header, stats);
        if (!_context.WorldState.ActorsByNetGuid.TryGetValue(channel.ActorNetGuid.Value, out var actor))
        {
            return;
        }

        MarkActorClosed(actor, header);
        EmitClosed(channel, header, actor);

        if (header.CloseReason == ChannelCloseReason.Dormancy)
        {
            stats.ActorDormantCount++;
        }

        if (header.CloseReason == ChannelCloseReason.Destroyed)
        {
            DestroyActorAndSubobjects(channel, header, actor, stats);
        }
    }

    private ActorState CreateActorState(ActorChannelState channel) => new()
    {
        NetGuid = channel.ActorNetGuid,
        ChannelIndex = channel.ChannelIndex,
        IsDynamic = channel.ActorNetGuid.IsDynamic,
        LifecycleStatus = ActorLifecycleStatus.Open,
        ActorPath = channel.ActorPath,
        ArchetypeNetGuid = channel.ArchetypeNetGuid,
        ArchetypePath = channel.ArchetypePath,
        ReplicationClassPath = channel.ReplicationClassPath,
        LevelNetGuid = channel.LevelGuid,
        FirstObservedTimeSeconds = channel.OpenTimeSeconds,
        FirstObservedPacketId = channel.OpenPacketId,
        OpenTimeSeconds = channel.OpenTimeSeconds,
        OpenPacketId = channel.OpenPacketId,
        OpenCount = 1,
        SpawnTimeSeconds = channel.ActorNetGuid.IsDynamic ? channel.OpenTimeSeconds : null,
        SpawnPacketId = channel.ActorNetGuid.IsDynamic ? channel.OpenPacketId : null,
        SpawnLocation = channel.SpawnLocation,
        SpawnRotation = channel.SpawnRotation,
        SpawnScale = channel.SpawnScale,
        SpawnVelocity = channel.SpawnVelocity,
        Location = channel.SpawnLocation,
        Rotation = channel.SpawnRotation,
        Scale = channel.SpawnScale,
        Velocity = channel.SpawnVelocity,
    };

    private static void ReopenActor(ActorState actor, ActorChannelState channel)
    {
        actor.ChannelIndex = channel.ChannelIndex;
        actor.LifecycleStatus = ActorLifecycleStatus.Open;
        actor.ActorPath ??= channel.ActorPath;
        actor.ArchetypeNetGuid = channel.ArchetypeNetGuid.IsValid
            ? channel.ArchetypeNetGuid
            : actor.ArchetypeNetGuid;
        actor.ArchetypePath ??= channel.ArchetypePath;
        actor.ReplicationClassPath ??= channel.ReplicationClassPath;
        actor.LevelNetGuid = channel.LevelGuid.IsValid ? channel.LevelGuid : actor.LevelNetGuid;
        actor.OpenTimeSeconds = channel.OpenTimeSeconds;
        actor.OpenPacketId = channel.OpenPacketId;
        actor.OpenCount++;
        actor.CloseTimeSeconds = null;
        actor.ClosePacketId = null;
        actor.CloseReason = null;
    }

    private void EmitOpened(ActorChannelState channel, NetworkGuid actorNetGuid, ActorState actor)
    {
        _context.EventSink.Emit(new ActorOpened(
            channel.OpenTimeSeconds,
            channel.OpenPacketId,
            actorNetGuid.Value,
            channel.ChannelIndex,
            actorNetGuid.IsDynamic,
            actor.ActorPath,
            actor.ArchetypePath));
    }

    private void EmitSpawnedIfNeeded(
        ActorChannelState channel,
        NetworkGuid actorNetGuid,
        ActorState actor,
        bool isNew)
    {
        if (!isNew || !actorNetGuid.IsDynamic)
        {
            return;
        }

        _context.EventSink.Emit(new ActorSpawned(
            channel.OpenTimeSeconds,
            channel.OpenPacketId,
            actorNetGuid.Value,
            channel.ChannelIndex,
            actor.ArchetypePath,
            actor.SpawnLocation,
            actor.SpawnRotation,
            actor.SpawnScale,
            actor.SpawnVelocity));
    }

    private void MarkChannelClosed(ActorChannelState channel, RawBunchHeader header, BunchPayloadStats stats)
    {
        channel.IsOpen = false;
        channel.IsDormant = header.bDormant;
        channel.ClosePacketId = header.PacketId;
        channel.CloseTimeSeconds = _context.CurrentTimeSeconds;
        channel.CloseReason = header.CloseReason;
        stats.ActorChannelCloseCount++;
    }

    private void MarkActorClosed(ActorState actor, RawBunchHeader header)
    {
        actor.ClosePacketId = header.PacketId;
        actor.CloseTimeSeconds = _context.CurrentTimeSeconds;
        actor.CloseReason = header.CloseReason;
        actor.LifecycleStatus = header.CloseReason switch
        {
            ChannelCloseReason.Destroyed => ActorLifecycleStatus.Destroyed,
            ChannelCloseReason.Dormancy => ActorLifecycleStatus.Dormant,
            _ => ActorLifecycleStatus.Closed,
        };
    }

    private void EmitClosed(ActorChannelState channel, RawBunchHeader header, ActorState actor)
    {
        _context.EventSink.Emit(new ActorClosed(
            _context.CurrentTimeSeconds,
            header.PacketId,
            actor.NetGuid.Value,
            channel.ChannelIndex,
            header.CloseReason));
    }

    private void DestroyActorAndSubobjects(
        ActorChannelState channel,
        RawBunchHeader header,
        ActorState actor,
        BunchPayloadStats stats)
    {
        actor.DestroyTimeSeconds = _context.CurrentTimeSeconds;
        actor.DestroyPacketId = header.PacketId;
        stats.ActorDestroyedCount++;

        foreach (var objectNetGuid in actor.SubobjectNetGuids)
        {
            DestroyActiveSubobject(channel, header, actor, objectNetGuid, stats);
        }

        _context.EventSink.Emit(new ActorDestroyed(
            _context.CurrentTimeSeconds,
            header.PacketId,
            actor.NetGuid.Value,
            channel.ChannelIndex));
    }

    private void DestroyActiveSubobject(
        ActorChannelState channel,
        RawBunchHeader header,
        ActorState actor,
        uint objectNetGuid,
        BunchPayloadStats stats)
    {
        if (!_context.WorldState.ObjectsByNetGuid.TryGetValue(objectNetGuid, out var objectState) ||
            !objectState.IsActive)
        {
            return;
        }

        objectState.IsActive = false;
        objectState.DestroyTimeSeconds = _context.CurrentTimeSeconds;
        objectState.DestroyPacketId = header.PacketId;
        objectState.DeleteFlags = 0;
        stats.SubobjectDestroyedCount++;
        _context.EventSink.Emit(new SubobjectDestroyed(
            _context.CurrentTimeSeconds,
            header.PacketId,
            objectState.NetGuid.Value,
            actor.NetGuid.Value,
            channel.ChannelIndex,
            DeleteFlags: 0,
            DestroyedWithActor: true));
    }
}
