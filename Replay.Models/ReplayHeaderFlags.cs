namespace Replay.Models;

[Flags]
public enum ReplayHeaderFlags : uint
{
    None = 0,
    ClientRecorded = 1 << 0,
    HasStreamingFixes = 1 << 1,
    DeltaCheckpoints = 1 << 2,
    GameSpecificFrameData = 1 << 3,
    ReplayConnection = 1 << 4,
    ActorPrioritizationEnabled = 1 << 5,
    NetRelevancyEnabled = 1 << 6,
    AsyncRecorded = 1 << 7,
}
