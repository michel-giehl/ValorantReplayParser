using Replay.Models.Descriptors;
using Replay.Models.Unreal;
using Replay.Unreal.Parsing;

namespace Replay.Valorant.Descriptors.Agents.Clay;

public sealed class RazeForceParameters : ExportGroupDescriptor<RazeForceParameters>
{
    public override string Path => "/Script/ShooterGame.ForceModuleManagerComponent:NetMulticastApplyForceModule";
    public override ExportCategory Categories => ExportCategory.Ability;
    public override ExportGroupKind Kind => ExportGroupKind.ClassNetCache;
    public override FieldStreamGrammar Grammar => FieldStreamGrammar.FunctionParameters;
    public int? HandleNumber { get; set; }
    public uint? ModuleType { get; set; }
    public uint? Source { get; set; }
    public FVector? SourceLocation { get; set; }
    public uint? Module { get; set; }
    public float? Duration { get; set; }
    public uint? Character { get; set; }
    public float? NetTimestamp { get; set; }
    public int? RespawnNumber { get; set; }
    protected override void Configure()
    {
        AddPropertyHandle(0, x => x.HandleNumber).Int32();
        AddPropertyHandle(1, x => x.ModuleType).EnumRemainingBits();
        AddPropertyHandle(2, x => x.Source).ObjectNetGuid();
        AddPropertyHandle(3, x => x.SourceLocation).FVector();
        AddPropertyHandle(4, x => x.Module).ObjectNetGuid();
        AddPropertyHandle(5, x => x.Duration).Float();
        AddPropertyHandle(6, x => x.Character).ObjectNetGuid();
        AddPropertyHandle(7, x => x.NetTimestamp).Float();
        AddPropertyHandle(8, x => x.RespawnNumber).Int32();
    }
}
