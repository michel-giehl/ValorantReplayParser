using Replay.Models.Replay;

namespace Replay.Unreal.Info;

public sealed record ReplayInfoReadResult(
    ReplayInfo Info,
    ReplayInfoSerializationMetadata SerializationMetadata);
