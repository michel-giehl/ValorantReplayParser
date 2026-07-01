namespace Replay.Models.Replay;

[Flags]
public enum ReplayHeaderFlags : uint
{
    HasStreamingFixes = 1 << 1,
    GameSpecificFrameData = 1 << 3,
}
