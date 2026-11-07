namespace Replay.Valorant.Crosshair;

/// <summary>
/// One replicated ordering of the crosshair leaves: which <see cref="CrosshairWireLeaf"/> sits at which position
/// of the flattened block. Every variant orders the same <see cref="CrosshairWireLayout.Leaves"/>; they differ only
/// in where a few of them sit.
/// </summary>
public sealed class CrosshairWireVariant
{
    private readonly int[] _leafAtPosition;
    private readonly int[] _positionOfLeaf;

    internal CrosshairWireVariant(string name, int[] leafAtPosition)
    {
        Name = name;
        _leafAtPosition = leafAtPosition;
        _positionOfLeaf = new int[leafAtPosition.Length];
        for (var position = 0; position < leafAtPosition.Length; position++)
        {
            _positionOfLeaf[leafAtPosition[position]] = position;
        }
    }

    /// <summary>Stable name of the ordering, for diagnostics.</summary>
    public string Name { get; }

    /// <summary>Position inside the block of the leaf with <paramref name="leafIndex"/> (<see cref="CrosshairWireLeaf.Index"/>).</summary>
    public int PositionOf(int leafIndex) => _positionOfLeaf[leafIndex];

    /// <summary>The leaf this ordering places at <paramref name="position"/>.</summary>
    public CrosshairWireLeaf LeafAt(int position) => CrosshairWireLayout.Leaves[_leafAtPosition[position]];

    /// <summary>Name the export table gives the leaf at <paramref name="position"/>.</summary>
    public string ExportNameAt(int position) => LeafAt(position).ExportName;
}
