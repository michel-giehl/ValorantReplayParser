using Microsoft.Extensions.Logging;
using Replay.Encoding.Net;
using Replay.Models.Descriptors;
using Replay.Models.Events;
using Replay.Unreal.World;

namespace Replay.Unreal.Parsing;

public sealed class FieldDecodeContext
{
    public WorldState? WorldState { get; init; }
    public NetGuidCache? NetGuidCache { get; init; }
    public IReplayEventSink? EventSink { get; init; }
    public ILoggerFactory? LoggerFactory { get; init; }
    public int CurrentPacketId { get; init; }
    public float CurrentTimeSeconds { get; init; }
    public uint ChannelIndex { get; init; }
    public NetworkGuid ActorNetGuid { get; init; }
    public NetworkGuid ObjectNetGuid { get; init; }
    public string? ExportGroupPath { get; set; }
    public string? FieldName { get; set; }
    public ExportCategory Categories { get; set; }
}
