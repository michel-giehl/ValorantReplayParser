using Replay.Models.Descriptors;
using Replay.Unreal.Parsing;

namespace Replay.Valorant.Descriptors.Agents.Gumshoe;

public static class GumshoeDescriptors
{
    public static List<ExportGroupDescriptor> CreateDescriptors() =>
    [
        new GumshoeAgentDescriptor(), new TripWireAbilityDescriptor(), new TripWireGameObjectDescriptor(),
        new SecondTripWireGameObjectDescriptor(), new CameraAbilityDescriptor(), new CameraPawnDescriptor(),
        new CameraDartProjectileDescriptor(), new CameraTrackingDartDescriptor(), new CameraDartAbilityDescriptor(),
        new UnpossessCameraAbilityDescriptor(), new CageTrapAbilityDescriptor(),
        new CageTrapProjectileDescriptor(), new CageTrapGameObjectDescriptor(), new CageZoneDescriptor(),
        new InterrogateAbilityDescriptor(), new InterrogateHatDescriptor(),
    ];

    public static List<ClassNetCacheDescriptor> CreateClassNetCacheDescriptors() =>
    [
        new TripwireClassNetCacheDescriptor(), new SecondWireClassNetCacheDescriptor(),
        Rpc(GumshoePaths.CageProjectile, "MulticastStopProjectile", 3),
    ];
    private static ClassNetCacheDescriptor Rpc(string path, string name, uint handle) =>
        new(path + "_ClassNetCache", [new RpcDescriptor
        {
            Name = name, FunctionExportPath = path + ":" + name, Handle = handle,
            Categories = ExportCategory.Ability, Decoder = ValorantPayloadDecoders.NoParametersRpc,
        }]);
}

public sealed class TripwireClassNetCacheDescriptor : ClassNetCacheDescriptor<TripwireClassNetCacheDescriptor>
{
    public override string Path => GumshoePaths.Tripwire + "_ClassNetCache";
    protected override void Configure() => AddFunctionHandle<TripwireEnemyParameters>(0, "SetEnemyInTrap",
        GumshoePaths.Tripwire + ":SetEnemyInTrap", ExportCategory.Ability);
}
public sealed class SecondWireClassNetCacheDescriptor : ClassNetCacheDescriptor<SecondWireClassNetCacheDescriptor>
{
    public override string Path => GumshoePaths.SecondWire + "_ClassNetCache";
    protected override void Configure() => AddFunctionHandle<TripwireEnemyParameters>(0, "SetEnemyInTrap",
        GumshoePaths.Tripwire + ":SetEnemyInTrap", ExportCategory.Ability);
}
public sealed class TripwireEnemyParameters : ExportGroupDescriptor<TripwireEnemyParameters>
{
    public override string Path => GumshoePaths.Tripwire + ":SetEnemyInTrap";
    public override ExportCategory Categories => ExportCategory.Ability;
    public override ExportGroupKind Kind => ExportGroupKind.ClassNetCache;
    public override FieldStreamGrammar Grammar => FieldStreamGrammar.FunctionParameters;
    public uint? PairedWire { get; set; }
    protected override void Configure() => AddPropertyHandle(0, x => x.PairedWire).ObjectNetGuid();
}
