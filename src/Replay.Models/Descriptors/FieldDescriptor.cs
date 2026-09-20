using System.Reflection;
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
}
