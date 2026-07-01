using Microsoft.Extensions.Logging;
using Replay.Encoding.Archives;
using Replay.Encoding.Net;
using Replay.Models.Descriptors;
using Replay.Models.Events;
using Replay.Models.Net;
using Replay.Unreal.Bunches.Payload;
using Replay.Unreal.Channels;
using Replay.Unreal.PackageMap;
using Replay.Unreal.Parsing;
using Replay.Unreal.Readers;

namespace Replay.Unreal.Bunches;

internal sealed class ContentBlockFramer
{
    private readonly NetGuidCache _netGuidCache;
    private readonly IReplayEventSink _eventSink;
    private readonly ContentBlockHeaderReader _headerReader;
    private readonly ContentBlockPathResolver _pathResolver;
    private readonly FieldPayloadParser _fieldPayloadParser;
    private readonly ExportBindingRegistry _bindingRegistry;
    private readonly IPropertyPayloadDecoder? _propertyPayloadDecoder;
    private readonly ILoggerFactory? _loggerFactory;

    public ContentBlockFramer(
        PackageMapReader packageMapReader,
        NetGuidCache netGuidCache,
        IReplayEventSink eventSink,
        FieldPayloadParser fieldPayloadParser,
        ExportBindingRegistry? bindingRegistry = null,
        IPropertyPayloadDecoder? propertyPayloadDecoder = null)
        : this(
            packageMapReader,
            netGuidCache,
            eventSink,
            fieldPayloadParser,
            bindingRegistry,
            propertyPayloadDecoder,
            loggerFactory: null)
    {
    }

    public ContentBlockFramer(
        PackageMapReader packageMapReader,
        ReplayReaderContext context,
        IPropertyPayloadDecoder? propertyPayloadDecoder = null)
        : this(
            packageMapReader,
            context.NetGuidCache,
            context.EventSink,
            new FieldPayloadParser(),
            context.ExportBindingRegistry,
            propertyPayloadDecoder,
            context.LoggerFactory)
    {
    }

    private ContentBlockFramer(
        PackageMapReader packageMapReader,
        NetGuidCache netGuidCache,
        IReplayEventSink eventSink,
        FieldPayloadParser fieldPayloadParser,
        ExportBindingRegistry? bindingRegistry,
        IPropertyPayloadDecoder? propertyPayloadDecoder,
        ILoggerFactory? loggerFactory)
    {
        _headerReader = new ContentBlockHeaderReader(packageMapReader);
        _pathResolver = new ContentBlockPathResolver(netGuidCache);
        _netGuidCache = netGuidCache;
        _eventSink = eventSink;
        _fieldPayloadParser = fieldPayloadParser;
        _bindingRegistry = bindingRegistry ?? new ExportBindingRegistry();
        _propertyPayloadDecoder = propertyPayloadDecoder;
        _loggerFactory = loggerFactory;
    }

    public void FrameContentBlocks(
        FBitArchive payload,
        ActorChannelState channel,
        BunchPayloadStats stats,
        float timeSeconds,
        int packetId,
        string replayVersionBranch)
    {
        while (!payload.AtEnd)
        {
            var header = _headerReader.Read(payload, channel);
            RecordHeaderStats(header, stats);

            if (header.IsDeleted)
            {
                HandleDeletedContentBlock(header, channel, timeSeconds, packetId, stats);
                continue;
            }

            if (!TryReadContentPayloadBitCount(payload, stats, out var contentPayloadBitCount))
            {
                return;
            }

            if (header.HasRepLayout)
            {
                FrameRepLayoutContentBlock(payload, contentPayloadBitCount, header, channel, timeSeconds, packetId,
                    replayVersionBranch, stats);
            }
            else
            {
                FrameClassNetCacheContentBlock(payload, contentPayloadBitCount, header, channel, timeSeconds, packetId,
                    replayVersionBranch, stats);
            }

            stats.ContentBlockCount++;
        }
    }

    private FBitArchive DecodeContentPayload(
        FBitArchive payload,
        int bitCount,
        ActorChannelState channel,
        string replayVersionBranch)
    {
        if (_propertyPayloadDecoder is null ||
            !channel.ActorNetGuid.IsValid ||
            string.IsNullOrWhiteSpace(replayVersionBranch))
        {
            return payload.ReadSubArchive(bitCount);
        }

        return _propertyPayloadDecoder.Decode(payload, bitCount, channel.ActorNetGuid.Value, replayVersionBranch);
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

    private static bool TryReadContentPayloadBitCount(
        FBitArchive payload,
        BunchPayloadStats stats,
        out int bitCount)
    {
        var encodedBitCount = payload.ReadIntPacked();
        if (encodedBitCount > int.MaxValue || encodedBitCount > payload.BitsRemaining)
        {
            stats.MalformedPayloadCount++;
            stats.MalformedContentBlockCount++;
            bitCount = 0;
            return false;
        }

        bitCount = (int)encodedBitCount;
        return true;
    }

    private void HandleDeletedContentBlock(
        ContentBlockHeader header,
        ActorChannelState channel,
        float timeSeconds,
        int packetId,
        BunchPayloadStats stats)
    {
        EmitExportGroupReceived(
            timeSeconds,
            packetId,
            channel,
            header,
            exportGroupPath: ResolveDeletedObjectPath(header),
            kind: ExportGroupKind.Unknown,
            categories: ExportCategory.None,
            payloadBits: 0,
            parsedBits: 0,
            wasDecoded: false,
            fields: []);
        stats.DeletedContentBlockCount++;
        stats.ContentBlockCount++;
    }

    private void FrameRepLayoutContentBlock(
        FBitArchive payload,
        int payloadBits,
        ContentBlockHeader header,
        ActorChannelState channel,
        float timeSeconds,
        int packetId,
        string replayVersionBranch,
        BunchPayloadStats stats)
    {
        var exportGroupPath = _pathResolver.ResolveExportGroupPath(header, channel);
        if (exportGroupPath is null)
        {
            SkipContentPayload(payload, payloadBits, stats);
            EmitExportGroupReceived(
                timeSeconds,
                packetId,
                channel,
                header,
                exportGroupPath: null,
                kind: ExportGroupKind.Unknown,
                categories: ExportCategory.None,
                payloadBits: payloadBits,
                parsedBits: 0,
                wasDecoded: false,
                fields: []);
            return;
        }

        var boundGroup = _bindingRegistry.GetBoundGroup(exportGroupPath);
        if (boundGroup is null || !boundGroup.Enabled)
        {
            SkipContentPayload(payload, payloadBits, stats);
            EmitExportGroupReceived(
                timeSeconds,
                packetId,
                channel,
                header,
                exportGroupPath,
                kind: _bindingRegistry.GetExportGroupKind(exportGroupPath),
                categories: boundGroup?.Categories ?? ExportCategory.None,
                payloadBits: payloadBits,
                parsedBits: 0,
                wasDecoded: false,
                fields: []);
            return;
        }

        using var decodedPayload = DecodeContentPayload(payload, payloadBits, channel, replayVersionBranch);
        var context = CreateDecodeContext(exportGroupPath, channel, header, timeSeconds, packetId);
        var beforeRepLayout = decodedPayload.BitsRemaining;
        var fields = _fieldPayloadParser.ParseRepLayoutProperties(decodedPayload, boundGroup, ref context);
        var parsedBits = checked((int)(beforeRepLayout - decodedPayload.BitsRemaining));
        stats.ContentPayloadBitsParsed += parsedBits;

        EmitExportGroupReceived(
            timeSeconds,
            packetId,
            channel,
            header,
            exportGroupPath,
            boundGroup.SourceDescriptor.Kind,
            boundGroup.Categories,
            payloadBits,
            parsedBits,
            wasDecoded: true,
            fields);
    }

    private void FrameClassNetCacheContentBlock(
        FBitArchive payload,
        int payloadBits,
        ContentBlockHeader header,
        ActorChannelState channel,
        float timeSeconds,
        int packetId,
        string replayVersionBranch,
        BunchPayloadStats stats)
    {
        if (payloadBits == 0)
        {
            return;
        }

        var classPath = _pathResolver.ResolveClassPath(header, channel);
        if (classPath is null)
        {
            SkipContentPayload(payload, payloadBits, stats);
            EmitExportGroupReceived(
                timeSeconds,
                packetId,
                channel,
                header,
                exportGroupPath: null,
                kind: ExportGroupKind.ClassNetCache,
                categories: ExportCategory.None,
                payloadBits: payloadBits,
                parsedBits: 0,
                wasDecoded: false,
                fields: []);
            return;
        }

        var boundCache = _bindingRegistry.GetBoundCache(classPath);
        if (boundCache is null || !boundCache.Enabled)
        {
            SkipContentPayload(payload, payloadBits, stats);
            EmitExportGroupReceived(
                timeSeconds,
                packetId,
                channel,
                header,
                classPath,
                ExportGroupKind.ClassNetCache,
                ExportCategory.None,
                payloadBits,
                parsedBits: 0,
                wasDecoded: false,
                fields: []);
            return;
        }

        using var decodedPayload = DecodeContentPayload(payload, payloadBits, channel, replayVersionBranch);
        var context = CreateDecodeContext(classPath, channel, header, timeSeconds, packetId);
        var beforeClassNetCache = decodedPayload.BitsRemaining;
        var invocations = _fieldPayloadParser.ParseClassNetCachePayload(decodedPayload, boundCache, ref context);
        var parsedBits = checked((int)(beforeClassNetCache - decodedPayload.BitsRemaining));
        stats.ContentPayloadBitsParsed += parsedBits;

        EmitExportGroupReceived(
            timeSeconds,
            packetId,
            channel,
            header,
            classPath,
            ExportGroupKind.ClassNetCache,
            ExportCategory.None,
            payloadBits,
            parsedBits,
            wasDecoded: true,
            fields: []);

        foreach (var invocation in invocations)
        {
            _eventSink.Emit(new RpcReceived(
                timeSeconds,
                packetId,
                channel.ActorNetGuid.Value,
                GetObjectNetGuid(header, channel).Value,
                channel.ChannelIndex,
                classPath,
                invocation.Name,
                invocation.FunctionExportPath,
                invocation.Handle,
                invocation.Categories,
                invocation.PayloadBits,
                invocation.ParsedBits,
                invocation.WasDecoded,
                invocation.Fields));
        }
    }
    private static void SkipContentPayload(FBitArchive payload, int bitCount, BunchPayloadStats stats)
    {
        payload.SkipBits(bitCount);
        stats.ContentPayloadBitsSkipped += bitCount;
    }

    private FieldDecodeContext CreateDecodeContext(
        string exportGroupPath,
        ActorChannelState channel,
        ContentBlockHeader header,
        float timeSeconds,
        int packetId) => new()
    {
        NetGuidCache = _netGuidCache,
        LoggerFactory = _loggerFactory,
        EventSink = _eventSink,
        CurrentPacketId = packetId,
        CurrentTimeSeconds = timeSeconds,
        ChannelIndex = channel.ChannelIndex,
        ActorNetGuid = channel.ActorNetGuid,
        ObjectNetGuid = GetObjectNetGuid(header, channel),
        ExportGroupPath = exportGroupPath,
    };

    private void EmitExportGroupReceived(
        float timeSeconds,
        int packetId,
        ActorChannelState channel,
        ContentBlockHeader header,
        string? exportGroupPath,
        ExportGroupKind kind,
        ExportCategory categories,
        int payloadBits,
        int parsedBits,
        bool wasDecoded,
        IReadOnlyList<DecodedReplayField> fields)
    {
        var objectNetGuid = GetObjectNetGuid(header, channel);
        var classNetGuid = header.IsActor ? channel.ArchetypeNetGuid : header.ClassNetGuid;
        var outerNetGuid = header.IsActor ? channel.LevelGuid : header.OuterNetGuid;

        _eventSink.Emit(new ExportGroupReceived(
            timeSeconds,
            packetId,
            channel.ActorNetGuid.Value,
            objectNetGuid.Value,
            channel.ChannelIndex,
            header.IsActor,
            header.IsDeleted,
            header.DeleteFlags,
            exportGroupPath,
            kind,
            categories,
            classNetGuid.Value,
            outerNetGuid.Value,
            ResolveObjectPath(header, channel, objectNetGuid),
            ResolveClassPath(header, channel, classNetGuid),
            ResolvePath(outerNetGuid),
            payloadBits,
            parsedBits,
            wasDecoded,
            fields));
    }

    private static NetworkGuid GetObjectNetGuid(ContentBlockHeader header, ActorChannelState channel) =>
        header.IsActor ? channel.ActorNetGuid : header.ObjectNetGuid;

    private string? ResolveDeletedObjectPath(ContentBlockHeader header) =>
        ResolvePath(header.ObjectNetGuid);

    private string? ResolveObjectPath(ContentBlockHeader header, ActorChannelState channel, NetworkGuid objectNetGuid)
    {
        if (header.IsActor)
        {
            return channel.ActorPath ?? ResolvePath(objectNetGuid);
        }

        return ResolvePath(objectNetGuid);
    }

    private string? ResolveClassPath(ContentBlockHeader header, ActorChannelState channel, NetworkGuid classNetGuid)
    {
        if (header.IsActor)
        {
            return channel.ReplicationClassPath ?? channel.ArchetypePath ?? ResolvePath(classNetGuid);
        }

        return ResolvePath(classNetGuid);
    }

    private string? ResolvePath(NetworkGuid netGuid) =>
        netGuid.IsValid && _netGuidCache.TryGetPath(netGuid.Value, out var path) ? path : null;
}
