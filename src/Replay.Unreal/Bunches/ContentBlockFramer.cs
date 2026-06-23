using Replay.Encoding.Archives;
using Replay.Encoding.Net;
using Replay.Models.Events;
using Replay.Unreal.Channels;
using Replay.Unreal.PackageMap;
using Replay.Unreal.Parsing;
using Replay.Unreal.World;

namespace Replay.Unreal.Bunches;

public class ContentBlockFramer
{
    private readonly PackageMapReader _packageMapReader;
    private readonly NetGuidCache _netGuidCache;
    private readonly WorldState _worldState;
    private readonly IReplayEventSink _eventSink;
    private readonly ExportBindingRegistry _bindingRegistry;

    public ContentBlockFramer(
        PackageMapReader packageMapReader,
        NetGuidCache netGuidCache,
        WorldState worldState,
        IReplayEventSink eventSink,
        ExportBindingRegistry? bindingRegistry = null)
    {
        _packageMapReader = packageMapReader;
        _netGuidCache = netGuidCache;
        _worldState = worldState;
        _eventSink = eventSink;
        _bindingRegistry = bindingRegistry ?? new ExportBindingRegistry();
    }

    public void FrameContentBlocks(
        FBitArchive payload,
        ActorChannelState channel,
        BunchPayloadStats stats,
        float timeSeconds,
        int packetId)
    {
        while (!payload.AtEnd)
        {
            var bHasRepLayout = payload.ReadBit();
            if (bHasRepLayout)
            {
                stats.RepLayoutContentBlockCount++;
            }

            var bIsActor = payload.ReadBit();
            var objectNetGuid = default(NetworkGuid);
            var classNetGuid = default(NetworkGuid);
            var outerNetGuid = channel.ActorNetGuid;
            var bStablyNamed = false;

            if (bIsActor)
            {
                stats.ActorContentBlockCount++;
            }
            else
            {
                objectNetGuid = _packageMapReader.InternalLoadObject(
                    payload,
                    isExportingNetGuidBunch: false,
                    recursionDepth: 0);
                bStablyNamed = payload.ReadBit();

                if (!bStablyNamed)
                {
                    var bIsDestroyMessage = payload.ReadBit();
                    if (bIsDestroyMessage)
                    {
                        var deleteFlags = payload.ReadByte();
                        DestroySubobject(
                            objectNetGuid,
                            channel,
                            deleteFlags,
                            destroyedWithActor: false,
                            timeSeconds,
                            packetId,
                            stats);
                        stats.DeletedContentBlockCount++;
                        stats.ContentBlockCount++;
                        continue;
                    }

                    classNetGuid = _packageMapReader.InternalLoadObject(
                        payload,
                        isExportingNetGuidBunch: false,
                        recursionDepth: 0);
                    if (!classNetGuid.IsValid)
                    {
                        DestroySubobject(
                            objectNetGuid,
                            channel,
                            deleteFlags: 0,
                            destroyedWithActor: false,
                            timeSeconds,
                            packetId,
                            stats);
                        stats.DeletedContentBlockCount++;
                        stats.ContentBlockCount++;
                        continue;
                    }

                    var bActorIsOuter = payload.ReadBit();
                    if (!bActorIsOuter)
                    {
                        outerNetGuid = _packageMapReader.InternalLoadObject(
                            payload,
                            isExportingNetGuidBunch: false,
                            recursionDepth: 0);
                    }
                }

                stats.SubobjectContentBlockCount++;
            }

            var payloadBits = (int)payload.ReadIntPacked();
            if (payloadBits < 0 || payloadBits > payload.BitsRemaining)
            {
                stats.MalformedPayloadCount++;
                stats.MalformedContentBlockCount++;
                return;
            }

            var contentPayload = payload.ReadSubArchive(payloadBits);
            if (!bIsActor)
            {
                ObserveSubobject(
                    objectNetGuid,
                    classNetGuid,
                    outerNetGuid,
                    bStablyNamed,
                    channel,
                    timeSeconds,
                    packetId,
                    stats);
            }

            var resolvedPath = ResolveClassPath(bIsActor, classNetGuid, channel);
            if (resolvedPath is not null)
            {
                TryParseContentPayload(contentPayload, resolvedPath, channel, timeSeconds, packetId, stats);
            }
            else
            {
                contentPayload.SkipRemaining();
                stats.ContentPayloadBitsSkipped += payloadBits;
            }

            stats.ContentBlockCount++;
        }
    }

    private string? ResolveClassPath(bool bIsActor, NetworkGuid classNetGuid, ActorChannelState channel)
    {
        if (bIsActor)
        {
            if (channel.ArchetypePath is not null)
            {
                return channel.ArchetypePath;
            }

            if (channel.ArchetypeNetGuid.IsValid && _netGuidCache.TryGetPath(channel.ArchetypeNetGuid.Value, out var archetypePath))
            {
                return archetypePath;
            }

            if (channel.ActorPath is not null)
            {
                return channel.ActorPath;
            }

            return channel.ActorNetGuid.IsValid && _netGuidCache.TryGetPath(channel.ActorNetGuid.Value, out var actorPath)
                ? actorPath
                : null;
        }

        if (classNetGuid.IsValid && _netGuidCache.TryGetPath(classNetGuid.Value, out var path))
        {
            return path;
        }

        return null;
    }

    private void TryParseContentPayload(
        FBitArchive contentPayload,
        string classPath,
        ActorChannelState channel,
        float timeSeconds,
        int packetId,
        BunchPayloadStats stats)
    {
        var boundGroup = _bindingRegistry.GetBoundGroup(classPath);
        if (boundGroup is not null && boundGroup.Enabled)
        {
            var context = new FieldDecodeContext
            {
                WorldState = _worldState,
                NetGuidCache = _netGuidCache,
                EventSink = _eventSink,
                CurrentPacketId = packetId,
                CurrentTimeSeconds = timeSeconds,
                ChannelIndex = channel.ChannelIndex,
                ActorNetGuid = channel.ActorNetGuid,
                ExportGroupPath = classPath,
            };
            FieldPayloadParser.ParseContentPayload(contentPayload, boundGroup, ref context);
            stats.ContentPayloadBitsParsed += (int)contentPayload.BitLength;
            return;
        }

        var boundCache = _bindingRegistry.GetBoundCache(classPath);
        if (boundCache is not null && boundCache.Enabled)
        {
            var context = new FieldDecodeContext
            {
                WorldState = _worldState,
                NetGuidCache = _netGuidCache,
                EventSink = _eventSink,
                CurrentPacketId = packetId,
                CurrentTimeSeconds = timeSeconds,
                ChannelIndex = channel.ChannelIndex,
                ActorNetGuid = channel.ActorNetGuid,
                ExportGroupPath = classPath,
            };
            FieldPayloadParser.ParseClassNetCachePayload(contentPayload, boundCache, ref context);
            stats.ContentPayloadBitsParsed += (int)contentPayload.BitLength;
            return;
        }

        contentPayload.SkipRemaining();
        stats.ContentPayloadBitsSkipped += (int)contentPayload.BitLength;
    }

    private void ObserveSubobject(
        NetworkGuid objectNetGuid,
        NetworkGuid classNetGuid,
        NetworkGuid outerNetGuid,
        bool isStablyNamed,
        ActorChannelState channel,
        float timeSeconds,
        int packetId,
        BunchPayloadStats stats)
    {
        if (!objectNetGuid.IsValid)
        {
            return;
        }

        var isNew = !_worldState.ObjectsByNetGuid.TryGetValue(objectNetGuid.Value, out var objectState);
        var isRecreated = objectState is { IsActive: false };

        if (isNew)
        {
            objectState = new ObjectState
            {
                NetGuid = objectNetGuid,
                ActorNetGuid = channel.ActorNetGuid,
                ChannelIndex = channel.ChannelIndex,
                IsActive = true,
                IsStablyNamed = isStablyNamed,
                ClassNetGuid = classNetGuid,
                OuterNetGuid = outerNetGuid,
                FirstObservedTimeSeconds = timeSeconds,
                FirstObservedPacketId = packetId,
                CreatedTimeSeconds = timeSeconds,
                CreatedPacketId = packetId,
            };
            _worldState.ObjectsByNetGuid.Add(objectNetGuid.Value, objectState);
        }
        else
        {
            objectState!.ChannelIndex = channel.ChannelIndex;
            objectState.IsActive = true;
            objectState.IsStablyNamed |= isStablyNamed;
            if (classNetGuid.IsValid)
            {
                objectState.ClassNetGuid = classNetGuid;
            }

            if (outerNetGuid.IsValid)
            {
                objectState.OuterNetGuid = outerNetGuid;
            }

            if (isRecreated)
            {
                objectState.CreatedTimeSeconds = timeSeconds;
                objectState.CreatedPacketId = packetId;
                objectState.DestroyTimeSeconds = null;
                objectState.DestroyPacketId = null;
                objectState.DeleteFlags = 0;
            }
        }

        ResolveObjectPaths(objectState);
        channel.SubobjectNetGuids.Add(objectNetGuid.Value);
        if (_worldState.ActorsByNetGuid.TryGetValue(channel.ActorNetGuid.Value, out var actor))
        {
            actor.SubobjectNetGuids.Add(objectNetGuid.Value);
        }

        if (!isNew && !isRecreated)
        {
            return;
        }

        stats.SubobjectCreatedCount++;
        _eventSink.Emit(new SubobjectCreated(
            timeSeconds,
            packetId,
            objectNetGuid.Value,
            channel.ActorNetGuid.Value,
            channel.ChannelIndex,
            objectState.ClassNetGuid.Value,
            objectState.OuterNetGuid.Value,
            objectState.ObjectPath,
            objectState.ClassPath,
            objectState.IsStablyNamed));
    }

    private void DestroySubobject(
        NetworkGuid objectNetGuid,
        ActorChannelState channel,
        byte deleteFlags,
        bool destroyedWithActor,
        float timeSeconds,
        int packetId,
        BunchPayloadStats stats)
    {
        if (!objectNetGuid.IsValid)
        {
            return;
        }

        var shouldEmit = true;
        if (!_worldState.ObjectsByNetGuid.TryGetValue(objectNetGuid.Value, out var objectState))
        {
            objectState = new ObjectState
            {
                NetGuid = objectNetGuid,
                ActorNetGuid = channel.ActorNetGuid,
                ChannelIndex = channel.ChannelIndex,
                IsActive = false,
                FirstObservedTimeSeconds = timeSeconds,
                FirstObservedPacketId = packetId,
                DestroyTimeSeconds = timeSeconds,
                DestroyPacketId = packetId,
                DeleteFlags = deleteFlags,
            };
            ResolveObjectPaths(objectState);
            _worldState.ObjectsByNetGuid.Add(objectNetGuid.Value, objectState);
        }
        else
        {
            shouldEmit = objectState.IsActive || objectState.DestroyPacketId is null;
            objectState.IsActive = false;
            objectState.DestroyTimeSeconds = timeSeconds;
            objectState.DestroyPacketId = packetId;
            objectState.DeleteFlags = deleteFlags;
        }

        channel.SubobjectNetGuids.Add(objectNetGuid.Value);
        if (_worldState.ActorsByNetGuid.TryGetValue(channel.ActorNetGuid.Value, out var actor))
        {
            actor.SubobjectNetGuids.Add(objectNetGuid.Value);
        }

        if (!shouldEmit)
        {
            return;
        }

        stats.SubobjectDestroyedCount++;
        _eventSink.Emit(new SubobjectDestroyed(
            timeSeconds,
            packetId,
            objectNetGuid.Value,
            channel.ActorNetGuid.Value,
            channel.ChannelIndex,
            deleteFlags,
            destroyedWithActor));
    }

    private void ResolveObjectPaths(ObjectState objectState)
    {
        if (_netGuidCache.TryGetPath(objectState.NetGuid.Value, out var objectPath))
        {
            objectState.ObjectPath = objectPath;
        }

        if (_netGuidCache.TryGetPath(objectState.ClassNetGuid.Value, out var classPath))
        {
            objectState.ClassPath = classPath;
        }

        if (_netGuidCache.TryGetPath(objectState.OuterNetGuid.Value, out var outerPath))
        {
            objectState.OuterPath = outerPath;
        }
    }
}
