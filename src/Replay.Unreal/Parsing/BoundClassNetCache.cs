using Replay.Models.Descriptors;

namespace Replay.Unreal.Parsing;

public sealed class BoundClassNetCache
{
    public required string Path { get; init; }
    public required ClassNetCacheDescriptor SourceDescriptor { get; init; }
    public FieldStreamGrammar Grammar { get; init; }
    public bool Enabled { get; init; }
    public required BoundRpcFunction[] FunctionsByHandle { get; init; }
}

public sealed class BoundRpcFunction
{
    public required string Name { get; init; }
    public required string FunctionExportPath { get; init; }
    public ExportCategory Categories { get; init; }
    public bool Enabled { get; init; }
    public BoundExportGroup? FunctionGroup { get; set; }
    public IRpcDecoder? Decoder { get; init; }
}
