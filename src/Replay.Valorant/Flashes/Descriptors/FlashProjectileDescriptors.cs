using Replay.Models.Descriptors;
using Replay.Models.Unreal;
using Replay.Unreal.Parsing;
using Replay.Valorant.Descriptors;

namespace Replay.Valorant.Flashes.Descriptors;

internal interface IFlashProjectilePayload
{
    FRepMovement? ReplicatedMovement { get; }
    uint? Owner { get; }
    uint? Instigator { get; }
    bool HasDecoded(string propertyName);
}

public abstract class FlashProjectileDescriptor<TDescriptor> : ExportGroupDescriptor<TDescriptor>,
    IFlashProjectilePayload
    where TDescriptor : FlashProjectileDescriptor<TDescriptor>
{
    public override ExportCategory Categories => ExportCategory.Ability | ExportCategory.Effects;
    public override ExportGroupKind Kind => ExportGroupKind.Actor;

    public FRepMovement? ReplicatedMovement { get; set; }
    public uint? Owner { get; set; }
    public uint? Instigator { get; set; }

    protected virtual ERotatorQuantization MovementRotationQuantization =>
        ERotatorQuantization.ByteComponents;

    protected override void Configure()
    {
        AddPropertyHandle(10, x => x.ReplicatedMovement, ExportCategory.Movement)
            .ReplicatedMovement(MovementRotationQuantization);
        AddPropertyHandle(11, x => x.Owner, ExportCategory.Ability).ObjectNetGuid();
        AddPropertyHandle(13, x => x.Instigator, ExportCategory.Ability).ObjectNetGuid();
    }
}

public sealed class SkyeFlashProjectileDescriptor : FlashProjectileDescriptor<SkyeFlashProjectileDescriptor>
{
    public override string Path => FlashPaths.SkyeProjectile;
}

public sealed class KayoOverhandFlashProjectileDescriptor
    : FlashProjectileDescriptor<KayoOverhandFlashProjectileDescriptor>
{
    public override string Path => FlashPaths.KayoOverhandProjectile;
}

public sealed class KayoUnderhandFlashProjectileDescriptor
    : FlashProjectileDescriptor<KayoUnderhandFlashProjectileDescriptor>
{
    public override string Path => FlashPaths.KayoUnderhandProjectile;
}

public sealed class BreachFlashProjectileDescriptor : FlashProjectileDescriptor<BreachFlashProjectileDescriptor>
{
    public override string Path => FlashPaths.BreachProjectile;
}

public sealed class PhoenixLeftFlashProjectileDescriptor
    : FlashProjectileDescriptor<PhoenixLeftFlashProjectileDescriptor>
{
    public override string Path => FlashPaths.PhoenixLeftProjectile;
}

public sealed class PhoenixRightFlashProjectileDescriptor
    : FlashProjectileDescriptor<PhoenixRightFlashProjectileDescriptor>
{
    public override string Path => FlashPaths.PhoenixRightProjectile;
}

public sealed class YoruFlashProjectileDescriptor
    : FlashProjectileDescriptor<YoruFlashProjectileDescriptor>
{
    public override string Path => FlashPaths.YoruProjectile;

    protected override ERotatorQuantization MovementRotationQuantization =>
        ERotatorQuantization.ByteComponents;
}

internal static class FlashProjectileClassNetCacheDescriptors
{
    public static IReadOnlyList<ClassNetCacheDescriptor> Create() =>
    [
        CreateStopProjectile(FlashPaths.KayoOverhandProjectile),
        CreateStopProjectile(FlashPaths.KayoUnderhandProjectile),
        CreateStopProjectile(FlashPaths.BreachProjectile),
        CreateStopProjectile(FlashPaths.PhoenixLeftProjectile),
        CreateStopProjectile(FlashPaths.PhoenixRightProjectile),
        CreateStopProjectile(FlashPaths.YoruProjectile),
    ];

    private static ClassNetCacheDescriptor CreateStopProjectile(string projectilePath) =>
        new(
            projectilePath + "_ClassNetCache",
            [
                new RpcDescriptor
                {
                    Name = "MulticastStopProjectile",
                    FunctionExportPath = projectilePath + ":MulticastStopProjectile",
                    Handle = 3,
                    Categories = ExportCategory.Ability | ExportCategory.Effects,
                    Decoder = ValorantPayloadDecoders.NoParametersRpc,
                },
            ]);
}
