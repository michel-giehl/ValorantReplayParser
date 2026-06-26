using Replay.Encoding.Archives;
using Replay.Encoding.Net;
using Replay.Models.Descriptors;
using Replay.Models.Errors;
using Replay.Models.Events;
using Replay.Models.Replay;
using Replay.Unreal.Bunches;
using Replay.Unreal.Bunches.Payload;
using Replay.Unreal.Channels;
using Replay.Unreal.Packets;
using Replay.Unreal.Parsing;
using Replay.Unreal.World;

namespace Replay.Unreal.Readers;

public class ReplayReaderContext
{
    public ReplayReaderContext(
        FBinaryArchive archive,
        IReplayEventSink? eventSink = null,
        DescriptorCatalog? descriptorCatalog = null,
        ParseProfile? parseProfile = null)
    {
        Archive = archive;
        BunchPayloadStats = new BunchPayloadStats();
        WorldState = new WorldState();
        EventSink = eventSink ?? NullReplayEventSink.Instance;
        ParseProfile = parseProfile ?? ParseProfile.Default;
        ExportBindingRegistry = new ExportBindingRegistry(descriptorCatalog, ParseProfile);
        BunchPayloadPipeline = new BunchPayloadPipeline(this);
    }

    public FBinaryArchive Archive { get; }
    public ReplayInfo ReplayInfo { get; set; } = new();
    public ReplayInfoSerializationMetadata ReplayInfoSerializationMetadata { get; set; } = new();
    public ReplayHeader ReplayHeader { get; set; } = new UninitializedReplayHeader();
    public ReplayVersion ReplayVersion { get; set; } = new UninitializedReplayVersion { Branch = string.Empty };
    public UEVersion UEVersion { get; set; } = new UninitializedUEVersion();
    public FBinaryArchive ReplayDataStream { get; set; } = new UninitializedBinaryArchive();
    public NetGuidCache NetGuidCache { get; } = new();
    public RawPacketStats PacketStats { get; } = new();
    internal RawPacketReader RawPacketReader { get; } = new();
    public BunchPayloadStats BunchPayloadStats { get; }
    public WorldState WorldState { get; }
    public IReplayEventSink EventSink { get; }
    public float CurrentTimeSeconds { get; internal set; }
    public Dictionary<uint, ActorChannelState> ChannelStates => WorldState.Channels;
    public List<ActorChannelState> ActorChannelOpens => WorldState.ActorChannelHistory;
    public BunchPayloadPipeline BunchPayloadPipeline { get; }
    public ExportBindingRegistry ExportBindingRegistry { get; }
    public ParseProfile ParseProfile { get; set; }
}
