namespace Replay.Models.Results;

/// <summary>Immutable snapshot of raw replay packet counters.</summary>
/// <param name="PacketCount">Number of packets encountered.</param>
/// <param name="TotalPacketBytes">Total encoded bytes consumed for packets.</param>
/// <param name="PacketsWithBunches">Number of packets containing one or more bunches.</param>
/// <param name="BunchCount">Total bunches encountered in packets.</param>
/// <param name="MalformedPacketCount">Malformed packet count maintained by the packet reader.</param>
/// <param name="PartialErrorCount">Partial-sequence error count maintained by the packet reader.</param>
/// <param name="MinTimeSeconds">Minimum packet time observed.</param>
/// <param name="MaxTimeSeconds">Maximum packet time observed.</param>
public sealed record ReplayPacketStatistics(
    int PacketCount,
    long TotalPacketBytes,
    int PacketsWithBunches,
    int BunchCount,
    int MalformedPacketCount,
    int PartialErrorCount,
    float MinTimeSeconds,
    float MaxTimeSeconds);
