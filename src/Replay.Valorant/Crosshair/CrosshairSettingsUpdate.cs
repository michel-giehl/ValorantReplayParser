using Replay.Models.Descriptors;

namespace Replay.Valorant.Crosshair;

/// <summary>One replicated leaf of an update: which member, and the value that was sent.</summary>
/// <param name="LeafIndex">Index into <see cref="CrosshairWireLayout.Leaves"/>.</param>
/// <param name="Path">Struct path of the leaf, for example <c>Primary.InnerLines.LineThickness</c>.</param>
/// <param name="Value">A <see cref="byte"/> (colour channel), <see cref="float"/>, <see cref="bool"/> or <see cref="string"/>.</param>
public readonly record struct CrosshairLeafChange(int LeafIndex, string Path, object Value);

/// <summary>
/// The crosshair members present in one <c>BombPlayerState</c> update.
/// </summary>
/// <remarks>
/// An update is sparse: only leaves that differ from the class default object are sent the first time, and
/// later updates only the leaves that changed. Merge updates in packet order into one
/// <see cref="CrosshairSettings"/> per player state with <see cref="ApplyTo"/>.
/// </remarks>
public sealed class CrosshairSettingsUpdate
{
    private readonly List<CrosshairLeafChange> _changes = [];

    public IReadOnlyList<CrosshairLeafChange> Changes => _changes;

    /// <summary>
    /// Number of exports in the replay's crosshair block that fit no leaf of the chosen wire ordering. Their leaves
    /// are not bound, so values sent for them are missing from this update and the crosshair built from it may be
    /// incomplete. Zero for a replay whose export names all fit.
    /// </summary>
    public int UnmatchedLeafCount { get; set; }

    /// <summary>True when <see cref="UnmatchedLeafCount"/> is above zero.</summary>
    public bool IsPartial => UnmatchedLeafCount > 0;

    /// <summary>Name of the <see cref="CrosshairWireVariant"/> the replay was bound with, when known.</summary>
    public string? WireLayout { get; set; }

    internal void Attach(CrosshairBlockBinding? block)
    {
        if (block is null)
        {
            return;
        }

        UnmatchedLeafCount = block.UnmatchedLeafCount;
        WireLayout = block.Variant.Name;
    }

    internal void Record(int leafIndex, DecodedFieldValue value) =>
        Add(leafIndex, CrosshairWireLayout.ToValue(CrosshairWireLayout.Leaves[leafIndex].Type, value));

    /// <summary>Appends a change for the leaf at <paramref name="leafIndex"/>; <paramref name="value"/> must have the leaf's CLR type.</summary>
    public void Add(int leafIndex, object value)
    {
        ArgumentNullException.ThrowIfNull(value);
        var leaf = CrosshairWireLayout.Leaves[leafIndex];
        var expected = leaf.Type switch
        {
            CrosshairWireType.Byte => typeof(byte),
            CrosshairWireType.Float => typeof(float),
            CrosshairWireType.Bool => typeof(bool),
            _ => typeof(string),
        };
        if (value.GetType() != expected)
        {
            throw new ArgumentException($"{leaf.Path} takes a {expected.Name}, not a {value.GetType().Name}.", nameof(value));
        }

        _changes.Add(new CrosshairLeafChange(leafIndex, leaf.Path, value));
    }

    /// <summary>Writes every leaf of this update into <paramref name="settings"/>, in the order they were decoded.</summary>
    public void ApplyTo(CrosshairSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);
        foreach (var change in _changes)
        {
            CrosshairWireLayout.Leaves[change.LeafIndex].Apply(settings, change.Value);
        }
    }
}
