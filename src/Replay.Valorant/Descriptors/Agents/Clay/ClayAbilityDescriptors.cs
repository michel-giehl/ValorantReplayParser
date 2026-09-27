using Replay.Models.Descriptors;
using Replay.Models.Unreal;
using Replay.Unreal.Parsing;

namespace Replay.Valorant.Descriptors.Agents.Clay;

/// <summary>Wire layouts observed in 42e03082-2c79-4668-a242-d085760d0b53.vrf.</summary>
public abstract class ClayActorDescriptor : ExportGroupDescriptor<ClayActorDescriptor>
{
    public override ExportCategory Categories => ExportCategory.Ability | ExportCategory.Effects;
    public override ExportGroupKind Kind => ExportGroupKind.Actor;
    protected virtual bool IsAbility => false;
    protected virtual bool IsMoving => !IsAbility;
    public uint? Owner { get; set; }
    public uint? Instigator { get; set; }
    public uint? CreatedByCharacter { get; set; }
    public bool? IsInPersistentData { get; set; }
    public FRepMovement? ReplicatedMovement { get; set; }
    public uint? AttachParent { get; set; }
    public uint? AttachComponent { get; set; }
    public FVector? RelativeScale3D { get; set; }
    public FVector? LocationOffset { get; set; }
    public FRotator? RotationOffset { get; set; }
    public int? CosmeticRandomSeed { get; set; }
    public uint? RemoteRole { get; set; }
    public uint? Role { get; set; }

    protected override void Configure()
    {
        // Numeric names and enum widths are recorded in the replay export table.
        AddPropertyHandle(3, "216", x => x.RemoteRole).EnumRemainingBits();
        AddPropertyHandle(12, "215", x => x.Role).EnumRemainingBits();
        AddPropertyHandle(11, x => x.Owner).ObjectNetGuid();
        AddPropertyHandle(13, x => x.Instigator).ObjectNetGuid();
        if (IsMoving)
            AddPropertyHandle(10, x => x.ReplicatedMovement, ExportCategory.Movement)
                .ReplicatedMovement(this is ClayBoomBotPawnDescriptor ? ERotatorQuantization.ShortComponents : ERotatorQuantization.ByteComponents);
        if (IsAbility || this is ClaySatchelProjectileDescriptor)
        {
            AddPropertyHandle(4, x => x.AttachParent).ObjectNetGuid();
            AddPropertyHandle(6, x => x.RelativeScale3D).FVectorNetQuantize100();
            AddPropertyHandle(9, x => x.AttachComponent).ObjectNetGuid();
        }
        if (this is ClaySatchelProjectileDescriptor)
        {
            AddPropertyHandle(5, x => x.LocationOffset).FVectorNetQuantize100();
            AddPropertyHandle(7, x => x.RotationOffset).FRotatorShort();
        }
        if (IsAbility)
        {
            AddPropertyHandle(14, "bInPersistentData", x => x.IsInPersistentData).Bool();
            AddPropertyHandle(56, x => x.CosmeticRandomSeed).Int32();
            AddPropertyHandle(58, x => x.CreatedByCharacter).ObjectNetGuid();
        }
        ConfigureAdditional();
    }

    protected virtual void ConfigureAdditional() { }
}

public sealed class ClayBoomBotAbilityDescriptor : ClayActorDescriptor
{
    public override string Path => ClayPaths.BoomBotAbility;
    protected override bool IsAbility => true;
}
public sealed class ClaySatchelAbilityDescriptor : ClayActorDescriptor
{
    public override string Path => ClayPaths.SatchelAbility;
    protected override bool IsAbility => true;
}
public sealed class ClayPaintShellsAbilityDescriptor : ClayActorDescriptor
{
    public override string Path => ClayPaths.PaintShellsAbility;
    protected override bool IsAbility => true;
}
public sealed class ClayShowstopperAbilityDescriptor : ClayActorDescriptor
{
    public override string Path => ClayPaths.ShowstopperAbility;
    protected override bool IsAbility => true;
}
public sealed class ClayBoomBotPawnDescriptor : ClayActorDescriptor
{
    public override string Path => ClayPaths.BoomBotPawn;
    public bool? ReplicateMovement { get; set; }
    public uint? Controller { get; set; }
    public float? ReplayLastTransformUpdateTimeStamp { get; set; }
    public FVector? ReplicatedGravityDirection { get; set; }
    public byte? ReplicatedMovementMode { get; set; }
    public bool? AIControlled { get; set; }
    protected override void ConfigureAdditional()
    {
        AddPropertyHandle(0, "bReplicateMovement", x => ((ClayBoomBotPawnDescriptor)x).ReplicateMovement).Bool();
        AddPropertyHandle(15, x => ((ClayBoomBotPawnDescriptor)x).Controller).ObjectNetGuid();
        AddPropertyHandle(26, x => ((ClayBoomBotPawnDescriptor)x).ReplayLastTransformUpdateTimeStamp).Float();
        AddPropertyHandle(27, x => ((ClayBoomBotPawnDescriptor)x).ReplicatedGravityDirection).FVectorNetQuantizeNormal();
        AddPropertyHandle(30, x => ((ClayBoomBotPawnDescriptor)x).ReplicatedMovementMode).Byte();
        AddPropertyHandle(54, "bAIControlled", x => ((ClayBoomBotPawnDescriptor)x).AIControlled).Bool();
    }
}
public sealed class ClaySatchelProjectileDescriptor : ClayActorDescriptor
{
    public override string Path => ClayPaths.SatchelProjectile;
}
public sealed class ClaySatchelExplosionDescriptor : ClayActorDescriptor
{
    public override string Path => ClayPaths.SatchelExplosion;
    protected override bool IsMoving => false;
}
public sealed class ClayPaintShellsPrimaryDescriptor : ClayActorDescriptor
{
    public override string Path => ClayPaths.PaintShellsPrimary;
}
public sealed class ClayPaintShellsSecondaryDescriptor : ClayActorDescriptor
{
    public override string Path => ClayPaths.PaintShellsSecondary;
}
public sealed class ClayPaintShellsSpawnerDescriptor : ClayActorDescriptor
{
    public override string Path => ClayPaths.PaintShellsSpawner;
}
public sealed class ClayRocketDescriptor : ClayActorDescriptor
{
    public override string Path => ClayPaths.Rocket;
}

public static class ClayPaths
{
    public const string BoomBotAbility = "/Game/Characters/Clay/S0/Ability_E/Ability_Clay_E_Boomba.Ability_Clay_E_Boomba_C";
    public const string BoomBotPawn = "/Game/Characters/Clay/S0/Ability_E/Pawn_Clay_E_Boomba.Pawn_Clay_E_Boomba_C";
    public const string SatchelAbility = "/Game/Characters/Clay/S0/Ability_Q/Ability_Clay_Q_Satchel.Ability_Clay_Q_Satchel_C";
    public const string SatchelProjectile = "/Game/Characters/Clay/S0/Ability_Q/Projectile_Clay_Q_Satchel_Arming.Projectile_Clay_Q_Satchel_Arming_C";
    public const string SatchelExplosion = "/Game/Characters/Clay/S0/Ability_Q/GameObject_Clay_Q_Explosion.GameObject_Clay_Q_Explosion_C";
    public const string PaintShellsAbility = "/Game/Characters/Clay/S0/Ability_4/Ability_Clay_4_ClusterGrenade.Ability_Clay_4_ClusterGrenade_C";
    public const string PaintShellsPrimary = "/Game/Characters/Clay/S0/Ability_4/Projectile_Clay_4_Projectile_Primary.Projectile_Clay_4_Projectile_Primary_C";
    public const string PaintShellsSecondary = "/Game/Characters/Clay/S0/Ability_4/Projectile_Clay_4_Projectile_Secondary.Projectile_Clay_4_Projectile_Secondary_C";
    public const string PaintShellsSpawner = "/Game/Characters/Clay/S0/Ability_4/Projectile_Clay_4_Projectile_SecondarySpawner.Projectile_Clay_4_Projectile_SecondarySpawner_C";
    public const string ShowstopperAbility = "/Game/Characters/Clay/S0/Ability_X/Ability_Clay_X_RocketLauncher.Ability_Clay_X_RocketLauncher_C";
    public const string Rocket = "/Game/Characters/Clay/S0/Ability_X/Projectile_Clay_X_Rocket.Projectile_Clay_X_Rocket_C";
}
