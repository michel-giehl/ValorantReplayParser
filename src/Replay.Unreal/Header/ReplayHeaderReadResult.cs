using Replay.Models.Replay;

namespace Replay.Unreal.Header;

public sealed record ReplayHeaderReadResult(
    ReplayHeader Header,
    ReplayVersion ReplayVersion,
    UEVersion UEVersion);
