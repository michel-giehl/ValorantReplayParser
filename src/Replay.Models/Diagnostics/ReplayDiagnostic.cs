namespace Replay.Models.Diagnostics;

/// <summary>A structured, recoverable warning observed while reading a replay.</summary>
/// <remarks>
/// Diagnostics contain descriptive strings and optional replay location values. They do not retain exceptions,
/// payload bytes, archives, callbacks, or parser state.
/// </remarks>
/// <param name="Code">The kind of recoverable condition.</param>
/// <param name="Message">Description of the sequence anomaly or speculative fallback.</param>
/// <param name="PacketId">Packet associated with the warning, when known.</param>
/// <param name="ChannelIndex">Channel associated with the warning, when known.</param>
/// <param name="TimeSeconds">Replay time associated with the warning, when known.</param>
/// <param name="ExportGroupPath">Export-group path associated with the warning, when known.</param>
/// <param name="FieldName">Field or function associated with the warning, when known.</param>
public sealed record ReplayDiagnostic(
    ReplayDiagnosticCode Code,
    string Message,
    int? PacketId = null,
    uint? ChannelIndex = null,
    float? TimeSeconds = null,
    string? ExportGroupPath = null,
    string? FieldName = null);
