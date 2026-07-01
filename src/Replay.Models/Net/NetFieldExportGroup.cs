namespace Replay.Models.Net;

public sealed class NetFieldExportGroup
{
    public required string PathName { get; init; }

    public required uint PathNameIndex { get; init; }

    public uint NetFieldExportsLength => checked((uint)NetFieldExports.Length);

    public required NetFieldExport?[] NetFieldExports { get; init; }
}