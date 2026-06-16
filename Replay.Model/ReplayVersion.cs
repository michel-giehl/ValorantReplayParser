namespace Replay.Model;

public class ReplayVersion
{
    public ushort Major { get; set; }
    public ushort Minor { get; set; }
    public ushort Patch { get; set; }
    public uint Changelist { get; set; }
    public required string Branch { get; set; }
}

public sealed class UninitializedReplayVersion : ReplayVersion;