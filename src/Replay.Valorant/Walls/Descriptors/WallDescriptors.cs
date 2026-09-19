using Replay.Models.Descriptors;
using Replay.Models.Unreal;
using Replay.Unreal.Parsing;
using Replay.Valorant.Descriptors;

namespace Replay.Valorant.Walls.Descriptors;

internal static class WallPaths
{
    public const string SageAbility =
        "/Game/Characters/Thorne/S0/Ability_E/Ability_Thorne_E_Wall_Fortifying.Ability_Thorne_E_Wall_Fortifying_C";
    public const string SageWall =
        "/Game/Characters/Thorne/S0/Ability_E/GameObject_Thorne_E_Wall_Fortifying.GameObject_Thorne_E_Wall_Fortifying_C";
    public const string SageSegment =
        "/Game/Characters/Thorne/S0/Ability_E/GameObject_Thorne_E_Wall_Segment_Fortifying.GameObject_Thorne_E_Wall_Segment_Fortifying_C";
    public const string VyseAbility =
        "/Game/Characters/Nox/S0/Ability_Q/Ability_Nox_Wall.Ability_Nox_Wall_C";
    public const string VyseTrap =
        "/Game/Characters/Nox/S0/Ability_Q/GameObject_Nox_WallTrap.GameObject_Nox_WallTrap_C";
    public const string VyseWall =
        "/Game/Characters/Nox/S0/Ability_Q/GameObject_Nox_Wall.GameObject_Nox_Wall_C";
}

internal interface IWallOwnedActor
{
    uint? Owner { get; }
    uint? Instigator { get; }
}

public abstract class WallOwnedActorDescriptor<TDescriptor> : ExportGroupDescriptor<TDescriptor>, IWallOwnedActor
    where TDescriptor : WallOwnedActorDescriptor<TDescriptor>
{
    public override ExportCategory Categories => ExportCategory.Ability;
    public override ExportGroupKind Kind => ExportGroupKind.Actor;

    public uint? Owner { get; set; }
    public uint? Instigator { get; set; }

    protected override void Configure()
    {
        AddPropertyHandle(11, x => x.Owner).ObjectNetGuid();
        AddPropertyHandle(13, x => x.Instigator).ObjectNetGuid();
        ConfigureWallFields();
    }

    protected virtual void ConfigureWallFields()
    {
    }
}

public sealed class SageWallDescriptor : WallOwnedActorDescriptor<SageWallDescriptor>
{
    public override string Path => WallPaths.SageWall;
}

public sealed class SageWallSegmentDescriptor : WallOwnedActorDescriptor<SageWallSegmentDescriptor>
{
    public override string Path => WallPaths.SageSegment;
    public bool? IsAlive { get; set; }

    protected override void ConfigureWallFields() =>
        AddPropertyHandle(15, x => x.IsAlive).Bool();
}

public sealed class VyseWallTrapDescriptor : WallOwnedActorDescriptor<VyseWallTrapDescriptor>
{
    public override string Path => WallPaths.VyseTrap;
}

public sealed class VyseWallDescriptor : WallOwnedActorDescriptor<VyseWallDescriptor>
{
    public override string Path => WallPaths.VyseWall;
}

public sealed class VyseInitializeTrapAnchorsParameters
    : ExportGroupDescriptor<VyseInitializeTrapAnchorsParameters>
{
    public override string Path => WallPaths.VyseTrap + ":MulticastInitializeTrapAnchors";
    public override ExportCategory Categories => ExportCategory.Ability;
    public override ExportGroupKind Kind => ExportGroupKind.ClassNetCache;
    public override FieldStreamGrammar Grammar => FieldStreamGrammar.FunctionParameters;

    public FVector? WallStartPoint { get; set; }
    public FVector? WallEndPoint { get; set; }
    public FVector? ImpactPoint { get; set; }
    public FVector? ImpactNormal { get; set; }

    protected override void Configure()
    {
        AddPropertyHandle(0, x => x.WallStartPoint).FVector();
        AddPropertyHandle(1, x => x.WallEndPoint).FVector();
        AddPropertyHandle(2, "Impact Point", x => x.ImpactPoint).FVector();
        AddPropertyHandle(3, x => x.ImpactNormal).FVector();
    }
}

public sealed class VyseInitializeWallParameters : ExportGroupDescriptor<VyseInitializeWallParameters>
{
    public override string Path => WallPaths.VyseWall + ":MulticastInitializeWall";
    public override ExportCategory Categories => ExportCategory.Ability;
    public override ExportGroupKind Kind => ExportGroupKind.ClassNetCache;
    public override FieldStreamGrammar Grammar => FieldStreamGrammar.FunctionParameters;

    public FVector? WallStartLocation { get; set; }
    public FVector? WallEndLocation { get; set; }
    public FVector? WallImpactNormal { get; set; }
    public uint? EnemyTrigger { get; set; }

    protected override void Configure()
    {
        AddPropertyHandle(0, x => x.WallStartLocation).FVector();
        AddPropertyHandle(1, x => x.WallEndLocation).FVector();
        AddPropertyHandle(2, x => x.WallImpactNormal).FVector();
        AddPropertyHandle(3, x => x.EnemyTrigger).ObjectNetGuid();
    }
}

public sealed class SageWallSegmentClassNetCacheDescriptor
    : ClassNetCacheDescriptor<SageWallSegmentClassNetCacheDescriptor>
{
    public override string Path => WallPaths.SageSegment + "_ClassNetCache";

    protected override void Configure() =>
        AddFunctionHandle(0, "DisableCollision", WallPaths.SageSegment + ":DisableCollision", ExportCategory.Ability)
            .Decode(ValorantPayloadDecoders.NoParametersRpc);
}

public sealed class VyseWallTrapClassNetCacheDescriptor
    : ClassNetCacheDescriptor<VyseWallTrapClassNetCacheDescriptor>
{
    public override string Path => WallPaths.VyseTrap + "_ClassNetCache";

    protected override void Configure() =>
        AddFunctionHandle<VyseInitializeTrapAnchorsParameters>(
            0,
            "MulticastInitializeTrapAnchors",
            WallPaths.VyseTrap + ":MulticastInitializeTrapAnchors",
            ExportCategory.Ability);
}

public sealed class VyseWallClassNetCacheDescriptor
    : ClassNetCacheDescriptor<VyseWallClassNetCacheDescriptor>
{
    public override string Path => WallPaths.VyseWall + "_ClassNetCache";

    protected override void Configure()
    {
        AddFunctionHandle(0, "MulticastEnableDynamicCollision",
                WallPaths.VyseWall + ":MulticastEnableDynamicCollision", ExportCategory.Ability)
            .Decode(ValorantPayloadDecoders.NoParametersRpc);
        AddFunctionHandle<VyseInitializeWallParameters>(
            1,
            "MulticastInitializeWall",
            WallPaths.VyseWall + ":MulticastInitializeWall",
            ExportCategory.Ability);
    }
}
