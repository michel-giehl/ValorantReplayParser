namespace Replay.Model;

[Flags]
public enum ReplayInfoChunkScanFlags : uint
{
    None = 0,
    SkipHeaderChunkTest = 1,
}
