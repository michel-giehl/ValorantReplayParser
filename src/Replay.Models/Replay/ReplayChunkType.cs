namespace Replay.Models.Replay;

public enum ReplayChunkType : uint
{
    Header = 0,
    ReplayData = 1,
    Checkpoint = 2,
    Event = 3,
    Unknown = 0xFFFFFFFF
}
