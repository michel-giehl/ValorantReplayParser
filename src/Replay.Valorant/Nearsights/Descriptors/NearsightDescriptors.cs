using Replay.Models.Descriptors;
using Replay.Models.Unreal;
using Replay.Unreal.Parsing;
using Replay.Valorant.Descriptors;

namespace Replay.Valorant.Nearsights.Descriptors;

internal static class NearsightPaths
{
    public const string OmenProjectile =
        "/Game/Characters/Wraith/S0/Ability_Q/Projectile_Wraith_Q_NearsightMissile.Projectile_Wraith_Q_NearsightMissile_C";
    public const string ReynaProjectile =
        "/Game/Characters/Vampire/S0/Ability_4/Projectile_Vampire_4_NearsightAoE.Projectile_Vampire_4_NearsightAoE_C";
    public const string ReynaSource =
        "/Game/Characters/Vampire/S0/Ability_4/GameObject_Vampire_4_NearsightAOE_Source.GameObject_Vampire_4_NearsightAOE_Source_C";
    public const string HarborProjectile =
        "/Game/Characters/Mage/S0/Ability_4/Projectile_Mage_4_SplashGrenade.Projectile_Mage_4_SplashGrenade_C";
    public const string HarborSource =
        "/Game/Characters/Mage/S0/Ability_4/GameObject_Mage_4_SplashGrenade.GameObject_Mage_4_SplashGrenade_C";
}

internal interface INearsightProjectilePayload
{
    FRepMovement? ReplicatedMovement { get; }
    uint? Owner { get; }
    uint? Instigator { get; }
    bool HasDecoded(string propertyName);
}

public abstract class NearsightProjectileDescriptor<TDescriptor> : ExportGroupDescriptor<TDescriptor>,
    INearsightProjectilePayload
    where TDescriptor : NearsightProjectileDescriptor<TDescriptor>
{
    public override ExportCategory Categories => ExportCategory.Ability | ExportCategory.Effects;
    public override ExportGroupKind Kind => ExportGroupKind.Actor;

    public FRepMovement? ReplicatedMovement { get; set; }
    public uint? Owner { get; set; }
    public uint? Instigator { get; set; }

    protected override void Configure()
    {
        AddPropertyHandle(10, x => x.ReplicatedMovement, ExportCategory.Movement)
            .ReplicatedMovement(ERotatorQuantization.ByteComponents);
        AddPropertyHandle(11, x => x.Owner, ExportCategory.Ability).ObjectNetGuid();
        AddPropertyHandle(13, x => x.Instigator, ExportCategory.Ability).ObjectNetGuid();
    }
}

public sealed class OmenNearsightProjectileDescriptor
    : NearsightProjectileDescriptor<OmenNearsightProjectileDescriptor>
{
    public override string Path => NearsightPaths.OmenProjectile;
}

public sealed class ReynaNearsightProjectileDescriptor
    : NearsightProjectileDescriptor<ReynaNearsightProjectileDescriptor>
{
    public override string Path => NearsightPaths.ReynaProjectile;
}

public sealed class HarborNearsightProjectileDescriptor
    : NearsightProjectileDescriptor<HarborNearsightProjectileDescriptor>
{
    public override string Path => NearsightPaths.HarborProjectile;
}

internal interface INearsightSourcePayload
{
    uint? Owner { get; }
    uint? Instigator { get; }
}

public abstract class NearsightSourceDescriptor<TDescriptor> : ExportGroupDescriptor<TDescriptor>,
    INearsightSourcePayload
    where TDescriptor : NearsightSourceDescriptor<TDescriptor>
{
    public override ExportCategory Categories => ExportCategory.Ability | ExportCategory.Effects;
    public override ExportGroupKind Kind => ExportGroupKind.Actor;

    public uint? Owner { get; set; }
    public uint? Instigator { get; set; }

    protected override void Configure()
    {
        AddPropertyHandle(11, x => x.Owner, ExportCategory.Ability).ObjectNetGuid();
        AddPropertyHandle(13, x => x.Instigator, ExportCategory.Ability).ObjectNetGuid();
    }
}

public sealed class ReynaNearsightSourceDescriptor
    : NearsightSourceDescriptor<ReynaNearsightSourceDescriptor>
{
    public override string Path => NearsightPaths.ReynaSource;
}

public sealed class HarborNearsightSourceDescriptor
    : NearsightSourceDescriptor<HarborNearsightSourceDescriptor>
{
    public override string Path => NearsightPaths.HarborSource;
}

internal static class NearsightProjectileClassNetCacheDescriptors
{
    public static IReadOnlyList<ClassNetCacheDescriptor> Create() =>
    [
        CreateStopProjectile(NearsightPaths.OmenProjectile, 3),
        CreateStopProjectile(NearsightPaths.ReynaProjectile, 4),
        CreateStopProjectile(NearsightPaths.HarborProjectile, 3),
    ];

    private static ClassNetCacheDescriptor CreateStopProjectile(string projectilePath, uint handle) =>
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
