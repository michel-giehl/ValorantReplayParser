using Replay.Models.Diagnostics;
using Replay.Models.Results;

namespace Replay.Valorant;

/// <summary>
/// Detached outcome of a successful full replay traversal.
/// </summary>
/// <remarks>
/// The result contains snapshots rather than parser execution state. Metadata DTOs nested within <see cref="Metadata"/>
/// remain consumer-owned mutable data. A completed status describes structural traversal under this parser's policy;
/// it does not guarantee that every VALORANT feature was semantically decoded.
/// </remarks>
/// <param name="Metadata">Detached replay metadata and version support information.</param>
/// <param name="PacketStats">Snapshot of raw packet counters.</param>
/// <param name="BunchPayloadStats">Snapshot of bunch and payload counters.</param>
/// <param name="Status">Whether the traversal completed with any recoverable diagnostics.</param>
/// <param name="Diagnostics">Read-only retained diagnostic snapshot, capped at 1,000 entries.</param>
/// <param name="TotalDiagnosticCount">Total warnings observed, including warnings omitted from <paramref name="Diagnostics"/>.</param>
/// <param name="SuppressedDiagnosticCount">Warnings omitted because the retained diagnostic limit was reached.</param>
/// <param name="ExportGroups">Detached export-group summaries, ordered by ordinal path.</param>
public sealed record ValorantReplayReadResult(
    ValorantReplayMetadata Metadata,
    ReplayPacketStatistics PacketStats,
    ReplayBunchStatistics BunchPayloadStats,
    ReplayReadStatus Status,
    IReadOnlyList<ReplayDiagnostic> Diagnostics,
    long TotalDiagnosticCount,
    long SuppressedDiagnosticCount,
    IReadOnlyList<ReplayExportGroupSummary> ExportGroups);
