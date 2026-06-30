using Microsoft.Extensions.Logging;
using Replay.Encoding.Archives;
using Replay.Encoding.Net;
using Replay.Models.Descriptors;
using Replay.Models.Events;
using Replay.Models.Replay;
using Replay.Unreal.Bunches;
using Replay.Unreal.Bunches.Payload;
using Replay.Unreal.Channels;
using Replay.Unreal.Packets;
using Replay.Unreal.Parsing;

namespace Replay.Unreal.Readers;

public class ReplayReaderContext
{
    public ReplayReaderContext(
        FBinaryArchive archive,
        IReplayEventSink? eventSink = null,
        DescriptorCatalog? descriptorCatalog = null,
        ParseProfile? parseProfile = null,
        ILoggerFactory? loggerFactory = null)
    {
        Archive = archive;
        BunchPayloadStats = new BunchPayloadStats();
        EventSink = eventSink ?? NullReplayEventSink.Instance;
        ParseProfile = parseProfile ?? ParseProfile.Default;
        LoggerFactory = loggerFactory;
        ExportBindingRegistry = new ExportBindingRegistry(descriptorCatalog, ParseProfile);
        BunchPayloadPipeline = new BunchPayloadPipeline(this);
    }

    public FBinaryArchive Archive { get; }
    public ReplayInfo ReplayInfo { get; set; } = new();
    public ReplayInfoSerializationMetadata ReplayInfoSerializationMetadata { get; set; } = new();
    public ReplayHeader ReplayHeader { get; set; } = new();
    public ReplayVersion ReplayVersion { get; set; } = new() { Branch = string.Empty };
    public UEVersion UEVersion { get; set; } = new();
    public FBinaryArchive ReplayDataStream { get; set; } = new(ReadOnlyMemory<byte>.Empty);
    public NetGuidCache NetGuidCache { get; } = new();
    public RawPacketStats PacketStats { get; } = new();
    internal RawPacketReader RawPacketReader { get; } = new();
    public BunchPayloadStats BunchPayloadStats { get; }
    public IReplayEventSink EventSink { get; }
    public float CurrentTimeSeconds { get; internal set; }
    public Dictionary<uint, ActorChannelState> ChannelStates { get; } = [];
    public List<ActorChannelState> ActorChannelOpens { get; } = [];
    public BunchPayloadPipeline BunchPayloadPipeline { get; }
    public ExportBindingRegistry ExportBindingRegistry { get; }
    public ParseProfile ParseProfile { get; set; }
    public ILoggerFactory? LoggerFactory { get; }
}