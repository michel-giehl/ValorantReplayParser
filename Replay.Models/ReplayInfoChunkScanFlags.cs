namespace Replay.Models;

[Flags]
public enum ReplayInfoChunkScanFlags : uint
{
    None = 0,
    SkipHeaderChunkTest = 1,
}
