using Replay.Models.Descriptors;
using Replay.Unreal.Parsing;

namespace Replay.Valorant.Descriptors.Agents;

/// <summary>One object parameter. The exact function and field name are supplied by its registration.</summary>
public sealed class AbilityObjectParameters(string path, string parameter) : ExportGroupDescriptor<AbilityObjectParameters>
{
    public override string Path => path;
    public override ExportCategory Categories => ExportCategory.Ability;
    public override ExportGroupKind Kind => ExportGroupKind.ClassNetCache;
    public override FieldStreamGrammar Grammar => FieldStreamGrammar.FunctionParameters;
    public override object CreatePayloadInstance() => new AbilityObjectParameters(path, parameter);
    public uint? Actor { get; set; }
    protected override void Configure() => AddPropertyHandle(0, parameter, x => x.Actor).ObjectNetGuid();
}
