namespace Replay.Models;

public struct RawBunchHeader
{
    public int PacketId { get; set; }
    public uint ChIndex { get; set; }
    public bool bOpen { get; set; }
    public bool bClose { get; set; }
    public bool bDormant { get; set; }
    public bool bIsReplicationPaused { get; set; }
    public bool bReliable { get; set; }
    public bool bPartial { get; set; }
    public bool bPartialInitial { get; set; }
    public bool bPartialFinal { get; set; }
    public bool bHasPackageMapExports { get; set; }
    public bool bHasMustBeMappedGUIDs { get; set; }
    public int ChSequence { get; set; }
    public string? ChName { get; set; }
    public ChannelCloseReason CloseReason { get; set; }
    public int PayloadBitCount { get; set; }
    public long PayloadBitOffset { get; set; }

    public bool HasPartialError { get; set; }
    public bool IsPartialCompleted { get; set; }
}
