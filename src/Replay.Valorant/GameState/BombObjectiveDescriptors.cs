using Replay.Models.Descriptors;
using Replay.Models.Unreal;
using Replay.Unreal.Parsing;

namespace Replay.Valorant.GameState;

public sealed class BombObjectiveClassNetCacheDescriptor : ClassNetCacheDescriptor<BombObjectiveClassNetCacheDescriptor>
{
    public override string Path => "/Game/GameModes/Components/Comp_BombEvents.Comp_BombEvents_C_ClassNetCache";
    protected override void Configure()
    {
        AddFunction<BombPlantedRpcParameters>("BombPlantedRPC", BombPlantedRpcParameters.ExportPath, ExportCategory.GameState);
        AddFunction<BombDefusedRpcParameters>("BombDefusedRPC", BombDefusedRpcParameters.ExportPath, ExportCategory.GameState);
    }
}

public sealed class BombPlantedRpcParameters : ExportGroupDescriptor<BombPlantedRpcParameters>
{
    public const string ExportPath = "/Game/GameModes/Components/Comp_BombEvents.Comp_BombEvents_C:BombPlantedRPC";
    public override string Path => ExportPath;
    public override ExportCategory Categories => ExportCategory.GameState;
    public override ExportGroupKind Kind => ExportGroupKind.ClassNetCache;
    public override FieldStreamGrammar Grammar => FieldStreamGrammar.FunctionParameters;
    public FVector PlantLocation { get; set; }
    public uint BombPlanter { get; set; }
    public byte PlantSite { get; set; }
    protected override void Configure()
    {
        AddProperty(x => x.PlantLocation).FVector();
        AddProperty(x => x.BombPlanter).ObjectNetGuid();
        AddProperty(x => x.PlantSite).EnumRemainingBits();
    }
}

public sealed class BombDefusedRpcParameters : ExportGroupDescriptor<BombDefusedRpcParameters>
{
    public const string ExportPath = "/Game/GameModes/Components/Comp_BombEvents.Comp_BombEvents_C:BombDefusedRPC";
    public override string Path => ExportPath;
    public override ExportCategory Categories => ExportCategory.GameState;
    public override ExportGroupKind Kind => ExportGroupKind.ClassNetCache;
    public override FieldStreamGrammar Grammar => FieldStreamGrammar.FunctionParameters;
    public uint DefusingCharacter { get; set; }
    protected override void Configure() => AddProperty(x => x.DefusingCharacter).ObjectNetGuid();
}
