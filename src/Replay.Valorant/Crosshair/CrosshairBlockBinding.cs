namespace Replay.Valorant.Crosshair;

/// <summary>An export inside the crosshair block whose name is not the one the chosen ordering expects there.</summary>
/// <param name="Position">Position inside the block, 0-based.</param>
/// <param name="Handle">Handle of the export in the replay's table.</param>
/// <param name="ExpectedName">Name the chosen ordering has at <paramref name="Position"/>.</param>
/// <param name="ActualName">Name the replay exports there.</param>
public readonly record struct CrosshairUnmatchedExport(int Position, int Handle, string ExpectedName, string ActualName);

/// <summary>
/// How the crosshair block of one replay's export table was bound: where it starts, which ordering fits it best,
/// and which exports inside it fit no leaf.
/// </summary>
/// <remarks>
/// A leaf the replay does not export at all is not a mismatch; export tables only list leaves some player sent.
/// An export that is present but has another name than the ordering expects is: its leaf is not bound, so a value
/// sent for it is lost and any crosshair built from the replay may be incomplete.
/// </remarks>
/// <param name="Start">Handle of the first leaf of the block.</param>
/// <param name="Variant">Ordering that matched the most exported names.</param>
/// <param name="MatchedLeafCount">Number of exports in the block whose name matches <paramref name="Variant"/>.</param>
/// <param name="Unmatched">Exports in the block that match no leaf of <paramref name="Variant"/>.</param>
public sealed record CrosshairBlockBinding(
    int Start,
    CrosshairWireVariant Variant,
    int MatchedLeafCount,
    IReadOnlyList<CrosshairUnmatchedExport> Unmatched)
{
    /// <summary>Number of exports inside the block that could not be bound.</summary>
    public int UnmatchedLeafCount => Unmatched.Count;
}
