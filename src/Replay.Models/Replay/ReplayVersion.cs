namespace Replay.Models.Replay;

public class ReplayVersion
{
    public ushort Major { get; init; }
    public ushort Minor { get; init; }
    public ushort Patch { get; init; }
    public uint Changelist { get; init; }
    public required string Branch { get; init; }
}

