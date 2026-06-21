namespace Replay.Encoding.Net;

public sealed class NetFieldExportGroup
{
    public required string PathName { get; init; }

    public required uint PathNameIndex { get; init; }

    public required uint NetFieldExportsLength { get; init; }

    public required NetFieldExport?[] NetFieldExports { get; init; }
}
