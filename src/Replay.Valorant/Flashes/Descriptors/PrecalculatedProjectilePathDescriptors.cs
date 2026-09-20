using Replay.Models.Descriptors;
using Replay.Models.Unreal;
using Replay.Unreal.Parsing;

namespace Replay.Valorant.Flashes.Descriptors;

internal static class PrecalculatedProjectilePathDescriptors
{
    internal const string ComponentPath = "/Script/ShooterGame.PrecalculatedProjectileMovementComponent";
    internal const string FunctionName = "MulticastSetPath";
    internal const string FunctionPath = ComponentPath + ":" + FunctionName;

    public static void AddTo(DescriptorCatalog catalog)
    {
        catalog.AddSubobjectClassPath("PrecalculatedProjectileMovement", ComponentPath);
        catalog.Add(new PrecalculatedProjectileMovementDescriptor());
        catalog.Add(new PrecalculatedProjectileMovementClassNetCacheDescriptor());
    }
}

internal sealed class PrecalculatedProjectileMovementDescriptor
    : ExportGroupDescriptor<PrecalculatedProjectileMovementDescriptor>
{
    public override string Path => PrecalculatedProjectilePathDescriptors.ComponentPath;
    public override ExportCategory Categories => ExportCategory.Ability;
    public override ExportGroupKind Kind => ExportGroupKind.Component;

    protected override void Configure()
    {
    }
}

internal sealed class PrecalculatedProjectileMovementClassNetCacheDescriptor
    : ClassNetCacheDescriptor<PrecalculatedProjectileMovementClassNetCacheDescriptor>
{
    public override string Path => PrecalculatedProjectilePathDescriptors.ComponentPath + "_ClassNetCache";

    protected override void Configure() => AddFunctionHandle<PrecalculatedProjectileSetPathParameters>(
        0,
        PrecalculatedProjectilePathDescriptors.FunctionName,
        PrecalculatedProjectilePathDescriptors.FunctionPath,
        ExportCategory.Ability);
}

public sealed class PrecalculatedProjectileSetPathParameters
    : ExportGroupDescriptor<PrecalculatedProjectileSetPathParameters>
{
    public override string Path => PrecalculatedProjectilePathDescriptors.FunctionPath;
    public override ExportCategory Categories => ExportCategory.Ability;
    public override ExportGroupKind Kind => ExportGroupKind.ClassNetCache;
    public override FieldStreamGrammar Grammar => FieldStreamGrammar.FunctionParameters;

    public PrecalculatedProjectilePathPoint?[]? NetworkedProjectilePath { get; set; }

    protected override void Configure() => AddPropertyHandle(0, x => x.NetworkedProjectilePath)
        .RepLayoutDynamicArray<PrecalculatedProjectilePathPoint>();
}

public sealed class PrecalculatedProjectilePathPoint
    : ExportGroupDescriptor<PrecalculatedProjectilePathPoint>
{
    // Nested RepLayout elements have no exported class path of their own. This is descriptor identity only.
    public override string Path => PrecalculatedProjectilePathDescriptors.FunctionPath + ":PathPoint";
    public override ExportCategory Categories => ExportCategory.Ability;

    public float? ElapsedSeconds { get; set; }
    public FVector? Location { get; set; }
    public FVector? Velocity { get; set; }

    protected override void Configure()
    {
        AddPropertyHandle(1, x => x.ElapsedSeconds).Float();
        AddPropertyHandle(2, x => x.Location).FVector();
        AddPropertyHandle(3, x => x.Velocity).FVector();
    }
}
