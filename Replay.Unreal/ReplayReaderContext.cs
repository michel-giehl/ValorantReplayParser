using Replay.Encoding.Archives;
using Replay.Encoding.Net;
using Replay.Models;

namespace Replay.Unreal;

public class ReplayReaderContext
{
    public ReplayReaderContext(FBinaryArchive archive)
    {
        Archive = archive;
    }

    public FBinaryArchive Archive { get; }
    public ReplayInfo ReplayInfo { get; set; } = new();
    public ReplayInfoSerializationMetadata ReplayInfoSerializationMetadata { get; set; } = new();
    public ReplayHeader ReplayHeader { get; set; } = new UninitializedReplayHeader();
    public ReplayVersion ReplayVersion { get; set; } = new UninitializedReplayVersion { Branch = string.Empty };
    public UEVersion UEVersion { get; set; } = new UninitializedUEVersion();
    public FBinaryArchive ReplayDataStream { get; set; } = new UninitializedBinaryArchive();
    public NetGuidCache NetGuidCache { get; } = new();
    public List<PlaybackPacket> PlaybackPackets { get; } = [];
    public List<ReplayParseError> Errors { get; } = [];
}
