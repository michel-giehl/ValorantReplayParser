using Replay.Models.Diagnostics;

namespace Replay.Unreal.Readers;

internal sealed class ReplayDiagnosticCollector
{
    public const int MaxRetainedDiagnostics = 1_000;

    private readonly List<ReplayDiagnostic> _diagnostics = [];

    public IReadOnlyList<ReplayDiagnostic> Diagnostics => _diagnostics;

    public long TotalDiagnosticCount { get; private set; }

    public long SuppressedDiagnosticCount => TotalDiagnosticCount - _diagnostics.Count;

    public ReplayReadStatus Status => TotalDiagnosticCount == 0
        ? ReplayReadStatus.Completed
        : ReplayReadStatus.CompletedWithWarnings;

    public void Add(ReplayDiagnostic diagnostic)
    {
        ArgumentNullException.ThrowIfNull(diagnostic);
        TotalDiagnosticCount = checked(TotalDiagnosticCount + 1);
        if (_diagnostics.Count < MaxRetainedDiagnostics)
        {
            _diagnostics.Add(diagnostic);
        }
    }
}
