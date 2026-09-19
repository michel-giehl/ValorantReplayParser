using Replay.Models.Descriptors;
using Replay.Models.Unreal;
using Replay.Unreal.Parsing;
using Replay.Valorant.Descriptors;

namespace Replay.Valorant.Smokes.Descriptors;

public abstract class SmokeOwnedActorDescriptor<TDescriptor> : ExportGroupDescriptor<TDescriptor>
    where TDescriptor : SmokeOwnedActorDescriptor<TDescriptor>
{
    public override ExportCategory Categories => ExportCategory.Ability | ExportCategory.Effects;
    public override ExportGroupKind Kind => ExportGroupKind.Actor;

    public uint? Owner { get; set; }
    public uint? Instigator { get; set; }

    protected override void Configure()
    {
        ConfigureBeforeOwnership();
        AddPropertyHandle(11, x => x.Owner, ExportCategory.Ability).ObjectNetGuid();
        AddPropertyHandle(13, x => x.Instigator, ExportCategory.Ability).ObjectNetGuid();
        ConfigureAfterOwnership();
    }

    protected virtual void ConfigureBeforeOwnership()
    {
    }

    protected virtual void ConfigureAfterOwnership()
    {
    }
}

public abstract class MovingSmokeActorDescriptor<TDescriptor> : SmokeOwnedActorDescriptor<TDescriptor>
    where TDescriptor : MovingSmokeActorDescriptor<TDescriptor>
{
    public FRepMovement? ReplicatedMovement { get; set; }

    protected override void ConfigureBeforeOwnership() =>
        AddPropertyHandle(10, x => x.ReplicatedMovement, ExportCategory.Movement)
            .ReplicatedMovement(ERotatorQuantization.ByteComponents);
}

public abstract class SmokeAbilityDescriptor<TDescriptor> : SmokeOwnedActorDescriptor<TDescriptor>
    where TDescriptor : SmokeAbilityDescriptor<TDescriptor>
{
    public FVector? RelativeScale3D { get; set; }
    public bool? IsInPersistentData { get; set; }
    public uint? CreatedByCharacter { get; set; }

    protected override void ConfigureBeforeOwnership() =>
        AddPropertyHandle(6, x => x.RelativeScale3D, ExportCategory.Ability).FVectorNetQuantize100();

    protected override void ConfigureAfterOwnership()
    {
        AddPropertyHandle(14, "bInPersistentData", x => x.IsInPersistentData, ExportCategory.Ability).Bool();
        AddPropertyHandle(58, x => x.CreatedByCharacter, ExportCategory.Ability).ObjectNetGuid();
    }
}

internal static class SmokeClassNetCacheDescriptors
{
    public static ClassNetCacheDescriptor CreateMovedToPersistentData(string abilityPath, uint handle) =>
        new(
            abilityPath + "_ClassNetCache",
            [
                new RpcDescriptor
                {
                    Name = "MulticastOnItemMovedToPersistentData",
                    FunctionExportPath = abilityPath + ":MulticastOnItemMovedToPersistentData",
                    Handle = handle,
                    Categories = ExportCategory.Ability | ExportCategory.Effects,
                    Decoder = ValorantPayloadDecoders.NoParametersRpc,
                },
            ]);

    public static ClassNetCacheDescriptor CreateStopProjectile(string projectilePath, uint handle) =>
        new(
            projectilePath + "_ClassNetCache",
            [
                new RpcDescriptor
                {
                    Name = "MulticastStopProjectile",
                    FunctionExportPath = projectilePath + ":MulticastStopProjectile",
                    Handle = handle,
                    Categories = ExportCategory.Ability | ExportCategory.Effects,
                    Decoder = ValorantPayloadDecoders.NoParametersRpc,
                },
            ]);
}
