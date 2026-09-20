namespace Replay.Models.Descriptors;

using global::Replay.Models.Replay;

public sealed class RpcDescriptor
{
    public required string Name { get; init; }
    public required string FunctionExportPath { get; init; }
    public uint? Handle { get; init; }
    public ExportCategory Categories { get; init; }
    public ExportGroupDescriptor? ParameterDescriptor { get; init; }
    public VersionedDefinition<ExportGroupDescriptor>? ParameterDescriptorDefinition { get; init; }
    public IReadOnlyList<FieldDescriptor> Fields { get; init; } = [];
    public IRpcDecoderDescriptor? Decoder { get; init; }
    public VersionedDefinition<IRpcDecoderDescriptor>? DecoderDefinition { get; init; }
}
