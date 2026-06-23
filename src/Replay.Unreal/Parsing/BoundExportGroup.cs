using Replay.Models.Descriptors;

namespace Replay.Unreal.Parsing;

public sealed class BoundExportGroup
{
    public required ExportGroupDescriptor SourceDescriptor { get; init; }
    public ExportCategory Categories { get; init; }
    public FieldStreamGrammar Grammar { get; init; }
    public bool Enabled { get; init; }
    public required FieldBinding[] FieldsByHandle { get; init; }
}
