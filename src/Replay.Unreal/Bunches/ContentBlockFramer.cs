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
            var header = ReadContentBlockHeader(payload, channel);
            RecordHeaderStats(header, stats);

            if (header.IsDeleted)
            {
                HandleDeletedContentBlock(header, channel, timeSeconds, packetId, stats);
                continue;
            }

            if (!TryReadContentPayload(payload, stats, out var contentPayload))
            {
                return;
            }

            if (!header.IsActor)
            {
                ObserveSubobject(header, channel, timeSeconds, packetId, stats);
            }

            DispatchContentPayload(contentPayload, header, channel, timeSeconds, packetId, stats);
            stats.ContentBlockCount++;
        }
    }

    private ContentBlockHeader ReadContentBlockHeader(FBitArchive payload, ActorChannelState channel)
    {
        var hasRepLayout = payload.ReadBit();
        if (payload.ReadBit())
        {
            return new ContentBlockHeader
            {
                HasRepLayout = hasRepLayout,
                IsActor = true,
                OuterNetGuid = channel.ActorNetGuid,
            };
        }

        return ReadSubobjectContentBlockHeader(payload, channel, hasRepLayout);
    }

    private ContentBlockHeader ReadSubobjectContentBlockHeader(
        FBitArchive payload,
        ActorChannelState channel,
        bool hasRepLayout)
    {
        var objectNetGuid = _packageMapReader.InternalLoadObject(
            payload,
            isExportingNetGuidBunch: false,
            recursionDepth: 0);
        var isStablyNamed = payload.ReadBit();
        var classNetGuid = default(NetworkGuid);
        var outerNetGuid = channel.ActorNetGuid;

        if (isStablyNamed)
        {
            return new ContentBlockHeader
            {
                HasRepLayout = hasRepLayout,
                ObjectNetGuid = objectNetGuid,
                ClassNetGuid = classNetGuid,
                OuterNetGuid = outerNetGuid,
                IsStablyNamed = isStablyNamed,
            };
        }
        if (payload.ReadBit())
        {
            return new ContentBlockHeader
            {
                HasRepLayout = hasRepLayout,
                ObjectNetGuid = objectNetGuid,
                OuterNetGuid = outerNetGuid,
                DeleteFlags = payload.ReadByte(),
                IsDeleted = true,
            };
        }

        classNetGuid = _packageMapReader.InternalLoadObject(
            payload,
            isExportingNetGuidBunch: false,
            recursionDepth: 0);
        if (!classNetGuid.IsValid)
        {
            return new ContentBlockHeader
            {
                HasRepLayout = hasRepLayout,
                ObjectNetGuid = objectNetGuid,
                OuterNetGuid = outerNetGuid,
                IsDeleted = true,
            };
        }

        if (!payload.ReadBit())
        {
            outerNetGuid = _packageMapReader.InternalLoadObject(
                payload,
                isExportingNetGuidBunch: false,
                recursionDepth: 0);
        }

        return new ContentBlockHeader
        {
            HasRepLayout = hasRepLayout,
            ObjectNetGuid = objectNetGuid,
            ClassNetGuid = classNetGuid,
            OuterNetGuid = outerNetGuid,
            IsStablyNamed = isStablyNamed,
        };
    }

    private static void RecordHeaderStats(ContentBlockHeader header, BunchPayloadStats stats)
    {
        if (header.HasRepLayout)
        {
            stats.RepLayoutContentBlockCount++;
        }

        if (header.IsActor)
        {
            stats.ActorContentBlockCount++;
        }
        else if (!header.IsDeleted)
        {
            stats.SubobjectContentBlockCount++;
        }
    }

    private static bool TryReadContentPayload(
        FBitArchive payload,
        BunchPayloadStats stats,
        out FBitArchive contentPayload)
    {
        var bitCount = payload.ReadIntPacked();
        if (bitCount > int.MaxValue || bitCount > payload.BitsRemaining)
        {
            stats.MalformedPayloadCount++;
            stats.MalformedContentBlockCount++;
            contentPayload = payload;
            return false;
        }

        contentPayload = payload.ReadSubArchive((int)bitCount);
        return true;
    }

    private void HandleDeletedContentBlock(
        ContentBlockHeader header,
        ActorChannelState channel,
        float timeSeconds,
        int packetId,
        BunchPayloadStats stats)
    {
        DestroySubobject(
            header.ObjectNetGuid,
            channel,
            header.DeleteFlags,
            destroyedWithActor: false,
            timeSeconds,
            packetId,
            stats);
        stats.DeletedContentBlockCount++;
        stats.ContentBlockCount++;
    }

    private string? ResolveClassPath(ContentBlockHeader header, ActorChannelState channel)
    {
        if (header.IsActor)
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

        if (header.ClassNetGuid.IsValid && _netGuidCache.TryGetPath(header.ClassNetGuid.Value, out var path))
        {
            return path;
        }

        return null;
    }

    private void DispatchContentPayload(
        FBitArchive contentPayload,
        ContentBlockHeader header,
        ActorChannelState channel,
        float timeSeconds,
        int packetId,
        BunchPayloadStats stats)
    {
        var classPath = ResolveClassPath(header, channel);
        if (classPath is null)
        {
            SkipRemainingContentPayload(contentPayload, stats);
            return;
        }

        var context = CreateDecodeContext(classPath, channel, timeSeconds, packetId);
        if (header.HasRepLayout && !ParseRepLayoutContent(contentPayload, classPath, ref context, stats))
        {
            return;
        }

        ParseClassNetCacheContent(contentPayload, classPath, ref context, stats);
    }

    private bool ParseRepLayoutContent(
        FBitArchive contentPayload,
        string classPath,
        ref FieldDecodeContext context,
        BunchPayloadStats stats)
    {
        var boundGroup = _bindingRegistry.GetBoundGroup(classPath);
        if (boundGroup is null || !boundGroup.Enabled)
        {
            SkipRemainingContentPayload(contentPayload, stats);
            return false;
        }

        var beforeRepLayout = contentPayload.BitsRemaining;
        FieldPayloadParser.ParseRepLayoutProperties(contentPayload, boundGroup, ref context);
        stats.ContentPayloadBitsParsed += beforeRepLayout - contentPayload.BitsRemaining;
        return true;
    }

    private void ParseClassNetCacheContent(
        FBitArchive contentPayload,
        string classPath,
        ref FieldDecodeContext context,
        BunchPayloadStats stats)
    {
        if (contentPayload.AtEnd)
        {
            return;
        }

        var boundCache = GetBoundClassNetCache(classPath);
        if (boundCache is null || !boundCache.Enabled)
        {
            SkipRemainingContentPayload(contentPayload, stats);
            return;
        }

        var beforeClassNetCache = contentPayload.BitsRemaining;
        FieldPayloadParser.ParseClassNetCachePayload(contentPayload, boundCache, ref context);
        stats.ContentPayloadBitsParsed += beforeClassNetCache - contentPayload.BitsRemaining;
    }

    private static void SkipRemainingContentPayload(FBitArchive contentPayload, BunchPayloadStats stats)
    {
        var skippedBits = contentPayload.BitsRemaining;
        contentPayload.SkipRemaining();
        stats.ContentPayloadBitsSkipped += skippedBits;
    }

    private FieldDecodeContext CreateDecodeContext(
        string classPath,
        ActorChannelState channel,
        float timeSeconds,
        int packetId) => new()
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

    private BoundClassNetCache? GetBoundClassNetCache(string classPath)
    {
        return _bindingRegistry.GetBoundCache(classPath)
            ?? _bindingRegistry.GetBoundCache(classPath + "_ClassNetCache");
    }

    private void ObserveSubobject(
        ContentBlockHeader header,
        ActorChannelState channel,
        float timeSeconds,
        int packetId,
        BunchPayloadStats stats) =>
        ObserveSubobject(
            header.ObjectNetGuid,
            header.ClassNetGuid,
            header.OuterNetGuid,
            header.IsStablyNamed,
            channel,
            timeSeconds,
            packetId,
            stats);

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
