using Replay.Model;

namespace Replay.Unreal;

public sealed record ReplayInfoReadResult(
    ReplayInfo Info,
    ReplayInfoSerializationMetadata SerializationMetadata);
