using Replay.Models.Descriptors;
using Replay.Models.Net;
using Replay.Models.Unreal;
using Replay.Unreal.Parsing;
using Replay.Valorant.Descriptors;

namespace Replay.Valorant.GameState;

public sealed class BombObjectiveClassNetCacheDescriptor : ClassNetCacheDescriptor<BombObjectiveClassNetCacheDescriptor>
{
    public const string ObjectiveRpcPath = "/Game/GameModes/Components/Comp_BombEvents.Comp_BombEvents_C:";
    public override string Path => "/Game/GameModes/Components/Comp_BombEvents.Comp_BombEvents_C_ClassNetCache";
    protected override void Configure()
    {
        AddFunction<BombPlantedRpcParameters>("BombPlantedRPC", BombPlantedRpcParameters.ExportPath, ExportCategory.GameState);
        AddFunction<BombDefusedRpcParameters>("BombDefusedRPC", BombDefusedRpcParameters.ExportPath, ExportCategory.GameState);
        AddFunction<BombPickedUpRpcParameters>("BombPickedUpRPC", BombPickedUpRpcParameters.ExportPath, ExportCategory.GameState);
        AddFunction<BombDroppedRpcParameters>("BombDroppedRPC", BombDroppedRpcParameters.ExportPath, ExportCategory.GameState);
        AddFunction<BombExplodedRpcParameters>("BombExplodedRPC", BombExplodedRpcParameters.ExportPath,
            ExportCategory.GameState);
        AddFunction<BombPlantStartRpcParameters>("BombPlantStartRPC", BombPlantStartRpcParameters.ExportPath,
            ExportCategory.GameState);
        AddFunction<BombPlantCancelRpcParameters>("BombPlantCancelRPC", BombPlantCancelRpcParameters.ExportPath,
            ExportCategory.GameState);
        AddFunction<BombDefuseStartRpcParameters>("BombDefuseStartRPC", BombDefuseStartRpcParameters.ExportPath,
            ExportCategory.GameState);
        AddFunction("BombDefuseStopRPC", ObjectiveRpcPath + "BombDefuseStopRPC", ExportCategory.GameState)
            .Decode(ValorantPayloadDecoders.NoParametersRpc);
        AddFunction<BombDefuseCheckpointReachedRpcParameters>("BombDefuseCheckpointReachedRPC",
            BombDefuseCheckpointReachedRpcParameters.ExportPath, ExportCategory.GameState);
        AddFunction<BombCarrierKilledRpcParameters>("BombCarrierKilledRPC", BombCarrierKilledRpcParameters.ExportPath,
            ExportCategory.GameState);
        AddFunction<BombDefuserKilledRpcParameters>("BombDefuserKilledRPC", BombDefuserKilledRpcParameters.ExportPath,
            ExportCategory.GameState);
        AddFunction<BombPlanterKilledRpcParameters>("BombPlanterKilledRPC", BombPlanterKilledRpcParameters.ExportPath,
            ExportCategory.GameState);
    }
}

public sealed class BombExplodedRpcParameters : ExportGroupDescriptor<BombExplodedRpcParameters>
{
    public const string ExportPath = "/Game/GameModes/Components/Comp_BombEvents.Comp_BombEvents_C:BombExplodedRPC";
    public override string Path => ExportPath;
    public override ExportCategory Categories => ExportCategory.GameState;
    public override ExportGroupKind Kind => ExportGroupKind.ClassNetCache;
    public override FieldStreamGrammar Grammar => FieldStreamGrammar.FunctionParameters;
    public byte? PlantSite { get; set; }
    protected override void Configure() => AddPropertyHandle(0, x => x.PlantSite).EnumRemainingBits();
}

public sealed class BombPlantedRpcParameters : ExportGroupDescriptor<BombPlantedRpcParameters>
{
    public const string ExportPath = "/Game/GameModes/Components/Comp_BombEvents.Comp_BombEvents_C:BombPlantedRPC";
    public override string Path => ExportPath;
    public override ExportCategory Categories => ExportCategory.GameState;
    public override ExportGroupKind Kind => ExportGroupKind.ClassNetCache;
    public override FieldStreamGrammar Grammar => FieldStreamGrammar.FunctionParameters;
    public FVector PlantLocation { get; set; }
    public NetObjectReference BombPlanter { get; set; }
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
    public NetObjectReference DefusingCharacter { get; set; }
    protected override void Configure() => AddProperty(x => x.DefusingCharacter).ObjectNetGuid();
}

public sealed class BombPlantStartRpcParameters : ExportGroupDescriptor<BombPlantStartRpcParameters>
{
    public const string ExportPath = BombObjectiveClassNetCacheDescriptor.ObjectiveRpcPath + "BombPlantStartRPC";
    public override string Path => ExportPath;
    public override ExportCategory Categories => ExportCategory.GameState;
    public override ExportGroupKind Kind => ExportGroupKind.ClassNetCache;
    public override FieldStreamGrammar Grammar => FieldStreamGrammar.FunctionParameters;
    public NetObjectReference? Planter { get; set; }
    public byte? PlantSite { get; set; }
    protected override void Configure()
    {
        AddProperty(x => x.Planter).ObjectNetGuid();
        AddProperty(x => x.PlantSite).EnumRemainingBits();
    }
}

public sealed class BombPlantCancelRpcParameters : ExportGroupDescriptor<BombPlantCancelRpcParameters>
{
    public const string ExportPath = BombObjectiveClassNetCacheDescriptor.ObjectiveRpcPath + "BombPlantCancelRPC";
    public override string Path => ExportPath;
    public override ExportCategory Categories => ExportCategory.GameState;
    public override ExportGroupKind Kind => ExportGroupKind.ClassNetCache;
    public override FieldStreamGrammar Grammar => FieldStreamGrammar.FunctionParameters;
    public byte? PlantSite { get; set; }
    protected override void Configure() => AddProperty(x => x.PlantSite).EnumRemainingBits();
}

public sealed class BombDefuseStartRpcParameters : ExportGroupDescriptor<BombDefuseStartRpcParameters>
{
    public const string ExportPath = BombObjectiveClassNetCacheDescriptor.ObjectiveRpcPath + "BombDefuseStartRPC";
    public override string Path => ExportPath;
    public override ExportCategory Categories => ExportCategory.GameState;
    public override ExportGroupKind Kind => ExportGroupKind.ClassNetCache;
    public override FieldStreamGrammar Grammar => FieldStreamGrammar.FunctionParameters;
    public NetObjectReference? Defuser { get; set; }
    protected override void Configure() => AddProperty(x => x.Defuser).ObjectNetGuid();
}

public sealed class BombDefuseCheckpointReachedRpcParameters : ExportGroupDescriptor<BombDefuseCheckpointReachedRpcParameters>
{
    public const string ExportPath = BombObjectiveClassNetCacheDescriptor.ObjectiveRpcPath + "BombDefuseCheckpointReachedRPC";
    public override string Path => ExportPath;
    public override ExportCategory Categories => ExportCategory.GameState;
    public override ExportGroupKind Kind => ExportGroupKind.ClassNetCache;
    public override FieldStreamGrammar Grammar => FieldStreamGrammar.FunctionParameters;
    public NetObjectReference? Defuser { get; set; }
    public int? CheckpointNumber { get; set; }
    public double? Progress { get; set; }
    protected override void Configure()
    {
        AddProperty(x => x.Defuser).ObjectNetGuid();
        AddProperty(x => x.CheckpointNumber).Int32();
        AddProperty(x => x.Progress).Double();
    }
}

public sealed class BombCarrierKilledRpcParameters : ExportGroupDescriptor<BombCarrierKilledRpcParameters>
{
    public const string ExportPath = BombObjectiveClassNetCacheDescriptor.ObjectiveRpcPath + "BombCarrierKilledRPC";
    public override string Path => ExportPath;
    public override ExportCategory Categories => ExportCategory.GameState;
    public override ExportGroupKind Kind => ExportGroupKind.ClassNetCache;
    public override FieldStreamGrammar Grammar => FieldStreamGrammar.FunctionParameters;
    public NetObjectReference? OldCarrier { get; set; }
    protected override void Configure() => AddProperty(x => x.OldCarrier).ObjectNetGuid();
}

public sealed class BombDefuserKilledRpcParameters : ExportGroupDescriptor<BombDefuserKilledRpcParameters>
{
    public const string ExportPath = BombObjectiveClassNetCacheDescriptor.ObjectiveRpcPath + "BombDefuserKilledRPC";
    public override string Path => ExportPath;
    public override ExportCategory Categories => ExportCategory.GameState;
    public override ExportGroupKind Kind => ExportGroupKind.ClassNetCache;
    public override FieldStreamGrammar Grammar => FieldStreamGrammar.FunctionParameters;
    public NetObjectReference? ShooterCharacter { get; set; }
    protected override void Configure() => AddProperty(x => x.ShooterCharacter).ObjectNetGuid();
}

public sealed class BombPlanterKilledRpcParameters : ExportGroupDescriptor<BombPlanterKilledRpcParameters>
{
    public const string ExportPath = BombObjectiveClassNetCacheDescriptor.ObjectiveRpcPath + "BombPlanterKilledRPC";
    public override string Path => ExportPath;
    public override ExportCategory Categories => ExportCategory.GameState;
    public override ExportGroupKind Kind => ExportGroupKind.ClassNetCache;
    public override FieldStreamGrammar Grammar => FieldStreamGrammar.FunctionParameters;
    public NetObjectReference? Planter { get; set; }
    protected override void Configure() => AddProperty(x => x.Planter).ObjectNetGuid();
}
