namespace Replay.Unreal.Packets;

public sealed class RawPacketStats
{
    public int PacketCount { get; private set; }
    public long TotalPacketBytes { get; private set; }
    public int PacketsWithBunches { get; private set; }
    public int BunchCount { get; private set; }
    public int MalformedPacketCount { get; private set; }
    public int PartialErrorCount { get; private set; }
    public float MinTimeSeconds { get; private set; }
    public float MaxTimeSeconds { get; private set; }

    public void RecordPacket(int byteCount, float timeSeconds, RawPacketReadResult result)
    {
        if (PacketCount == 0)
        {
            MinTimeSeconds = timeSeconds;
            MaxTimeSeconds = timeSeconds;
        }
        else
        {
            MinTimeSeconds = Math.Min(MinTimeSeconds, timeSeconds);
            MaxTimeSeconds = Math.Max(MaxTimeSeconds, timeSeconds);
        }

        PacketCount++;
        TotalPacketBytes += byteCount;
        if (result.BunchCount > 0)
        {
            PacketsWithBunches++;
        }

        BunchCount += result.BunchCount;
        if (result.IsMalformed)
        {
            MalformedPacketCount++;
        }

        PartialErrorCount += result.PartialErrorCount;
    }
}
