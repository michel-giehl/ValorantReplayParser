namespace Replay.Models.Results;

/// <summary>Detached summary of one class-net-cache field.</summary>
/// <param name="Handle">Field handle.</param>
/// <param name="Name">Field name.</param>
/// <param name="CompatibleChecksum">Compatible field checksum.</param>
public sealed record ReplayExportFieldSummary(uint Handle, string Name, uint CompatibleChecksum);

/// <summary>Detached summary of an export group.</summary>
/// <param name="PathName">Ordinal export-group path.</param>
/// <param name="PathNameIndex">Path-name index associated with the group.</param>
/// <param name="Fields">Read-only field summaries ordered by handle.</param>
public sealed record ReplayExportGroupSummary(
    string PathName,
    uint PathNameIndex,
    IReadOnlyList<ReplayExportFieldSummary> Fields);
