namespace Replay.Unreal.Bunches;

/// <summary>
/// Resource limits for incomplete bunch payloads retained by a replay parse.
/// </summary>
internal sealed record PartialBunchLimits
{
    private const long MiB = 1024L * 1024L;

    public static PartialBunchLimits Default { get; } = new();

    public PartialBunchLimits(
        long maxPayloadBytesPerChannel = 16 * MiB,
        long maxRetainedCapacityBytes = 128 * MiB,
        int maxPendingAssemblies = 1024,
        long maxReplacementTransientCapacityBytes = 256 * MiB)
    {
        if (maxPayloadBytesPerChannel < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(maxPayloadBytesPerChannel));
        }

        if (maxRetainedCapacityBytes < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(maxRetainedCapacityBytes));
        }

        if (maxPendingAssemblies < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(maxPendingAssemblies));
        }

        if (maxReplacementTransientCapacityBytes < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(maxReplacementTransientCapacityBytes));
        }

        MaxPayloadBytesPerChannel = maxPayloadBytesPerChannel;
        MaxRetainedCapacityBytes = maxRetainedCapacityBytes;
        MaxPendingAssemblies = maxPendingAssemblies;
        MaxReplacementTransientCapacityBytes = maxReplacementTransientCapacityBytes;
    }

    public long MaxPayloadBytesPerChannel { get; }

    public long MaxRetainedCapacityBytes { get; }

    public int MaxPendingAssemblies { get; }

    public long MaxReplacementTransientCapacityBytes { get; }
}
