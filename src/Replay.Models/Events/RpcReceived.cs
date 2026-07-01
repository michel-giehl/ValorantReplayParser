using JetBrains.Annotations;
using Replay.Models.Descriptors;

namespace Replay.Models.Events;

[UsedImplicitly]
public sealed record RpcReceived(
    float TimeSeconds,
    int PacketId,
    uint ActorNetGuid,
    uint ObjectNetGuid,
    uint ChannelIndex,
    string ClassPath,
    string FunctionName,
    string FunctionExportPath,
    int FunctionHandle,
    ExportCategory Categories,
    int PayloadBits,
    int ParsedBits,
    bool WasDecoded,
    IReadOnlyList<DecodedReplayField> Fields)
    : ReplayEvent(TimeSeconds, PacketId);