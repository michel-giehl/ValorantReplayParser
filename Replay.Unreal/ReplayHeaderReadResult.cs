using Replay.Model;

namespace Replay.Unreal;

public sealed record ReplayHeaderReadResult(
    ReplayHeader Header,
    ReplayVersion ReplayVersion,
    UEVersion UEVersion);