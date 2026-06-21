using Replay.Encoding.Archives;
using Replay.Encoding.Net;
using Replay.Models;

namespace Replay.Unreal;

public class ReplayReaderContext
{
    public ReplayReaderContext(FBinaryArchive archive)
    {
        Archive = archive;
        BunchPayloadStats = new BunchPayloadStats();
        ChannelStates = new Dictionary<uint, ActorChannelState>();
        ActorChannelOpens = [];
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
    public Dictionary<uint, ActorChannelState> ChannelStates { get; }
    public List<ActorChannelState> ActorChannelOpens { get; }
    public BunchPayloadPipeline BunchPayloadPipeline { get; }
    public List<ReplayParseError> Errors { get; } = [];
}
