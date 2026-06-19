namespace Replay.Encoding.Net;

public sealed class NetFieldExport
{
    public required uint Handle { get; init; }

    public required uint CompatibleChecksum { get; init; }

    public required string Name { get; init; }
}
