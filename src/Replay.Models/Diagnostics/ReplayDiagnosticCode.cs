namespace Replay.Models.Diagnostics;

/// <summary>Completion state of a full replay traversal.</summary>
public enum ReplayReadStatus
{
    /// <summary>The replay traversal completed without recoverable diagnostics.</summary>
    Completed,
    /// <summary>The replay traversal completed after one or more recoverable conditions were recorded.</summary>
    CompletedWithWarnings,
}

/// <summary>Known recoverable conditions reported in <see cref="ReplayDiagnostic"/>.</summary>
public enum ReplayDiagnosticCode
{
    /// <summary>A partial-bunch sequence anomaly caused an affected assembly to be discarded.</summary>
    PartialSequenceError,
    /// <summary>An unfinished partial bunch was discarded when input ended.</summary>
    IncompletePartialBunch,
    /// <summary>A speculative typed decoder rolled back and preserved the bounded payload as raw data.</summary>
    RawPayloadFallback,
}
