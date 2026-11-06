using System.Reflection;
using Replay.Models.Net;
using Replay.Models.Replay;

namespace Replay.Models.Descriptors;

public sealed class FieldDescriptor
{
    public string? ExportName { get; init; }
    public string? PropertyName { get; init; }
    public PropertyInfo? TargetProperty { get; init; }
    public uint? Handle { get; init; }
    public ExportCategory Categories { get; init; }
    public IFieldDecoderDescriptor? Decoder { get; init; }
    public VersionedDefinition<IFieldDecoderDescriptor>? DecoderDefinition { get; init; }

    /// <summary>
    /// Resolves this field's handle from the replay's own export table instead of a fixed handle or a first
    /// name match. Used for flattened struct members whose names repeat and whose position is only known
    /// relative to the rest of the struct. Returning null leaves the field unbound for that replay.
    /// </summary>
    public Func<NetFieldExportGroup, uint?>? HandleResolver { get; init; }

    /// <summary>
    /// Receives the decoded value instead of assigning <see cref="TargetProperty"/>. The payload is the
    /// descriptor instance created for one update.
    /// </summary>
    public Action<object, DecodedFieldValue, object?>? Assigner { get; init; }

    /// <summary>
    /// Computes per-replay state when the field is bound to a replay export group. The state is handed to
    /// <see cref="Assigner"/> with every decoded value, so a custom assigner can report how well the group fit.
    /// </summary>
    public Func<NetFieldExportGroup, object?>? BindingStateResolver { get; init; }

    /// <summary>Property name reported by <c>HasDecoded</c> when <see cref="Assigner"/> runs.</summary>
    public string? DecodedMarker { get; init; }
}
