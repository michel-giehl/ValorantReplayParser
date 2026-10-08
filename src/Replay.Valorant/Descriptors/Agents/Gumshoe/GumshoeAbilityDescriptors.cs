using Replay.Models.Descriptors;
using Replay.Models.Unreal;
using Replay.Unreal.Parsing;

namespace Replay.Valorant.Descriptors.Agents.Gumshoe;

/// <summary>Named wire fields from the 13.06 replay 3f0a3366; no gameplay interpretation.</summary>
public abstract class GumshoeActorDescriptor : ExportGroupDescriptor<GumshoeActorDescriptor>
{
    public override ExportCategory Categories => ExportCategory.Ability | ExportCategory.Effects;
    public override ExportGroupKind Kind => ExportGroupKind.Actor;
    protected virtual bool IsEquipment => false;
    protected virtual bool IsMoving => false;
    public uint? Owner { get; set; }
    public uint? Instigator { get; set; }
    public uint? CreatedByCharacter { get; set; }
    public uint? AttachParent { get; set; }
    public uint? AttachComponent { get; set; }
    public bool? IsInPersistentData { get; set; }
    public FRepMovement? ReplicatedMovement { get; set; }
    protected override void Configure()
    {
        AddProperty(x => x.Owner).ObjectNetGuid();
        AddProperty(x => x.Instigator).ObjectNetGuid();
        if (IsEquipment)
        {
            AddProperty(x => x.CreatedByCharacter).ObjectNetGuid();
            AddProperty("bInPersistentData", x => x.IsInPersistentData).Bool();
            AddProperty(x => x.AttachParent).ObjectNetGuid();
            AddProperty(x => x.AttachComponent).ObjectNetGuid();
        }
        if (IsMoving)
            AddProperty(x => x.ReplicatedMovement, ExportCategory.Movement).ReplicatedMovement(ERotatorQuantization.ByteComponents);
        ConfigureAdditional();
    }
    protected virtual void ConfigureAdditional() { }
}

public sealed class CameraAbilityDescriptor : GumshoeActorDescriptor
{
    public override string Path => GumshoePaths.CameraAbility;
    protected override bool IsEquipment => true;
}
public sealed class CameraDartAbilityDescriptor : GumshoeActorDescriptor
{
    public override string Path => GumshoePaths.DartAbility;
    protected override bool IsEquipment => true;
}
public sealed class UnpossessCameraAbilityDescriptor : GumshoeActorDescriptor
{
    public override string Path => GumshoePaths.UnpossessAbility;
    protected override bool IsEquipment => true;
}
public sealed class CameraPawnDescriptor : GumshoeActorDescriptor
{
    public override string Path => GumshoePaths.Camera;
    public bool? Possessed { get; set; }
    public bool? IsDeployed { get; set; }
    protected override void ConfigureAdditional()
    {
        AddProperty(x => ((CameraPawnDescriptor)x).Possessed).Bool();
        AddProperty(x => ((CameraPawnDescriptor)x).IsDeployed).Bool();
    }
}
public sealed class CameraDartProjectileDescriptor : GumshoeActorDescriptor
{
    public override string Path => GumshoePaths.Dart;
    protected override bool IsMoving => true;
}
public sealed class CameraTrackingDartDescriptor : GumshoeActorDescriptor
{
    public override string Path => GumshoePaths.TrackingDart;
    public uint? Target { get; set; }
    protected override void ConfigureAdditional() => AddProperty(x => ((CameraTrackingDartDescriptor)x).Target).ObjectNetGuid();
}
public sealed class CageTrapGameObjectDescriptor : GumshoeActorDescriptor
{
    public override string Path => GumshoePaths.CageDevice;
}
public sealed class CageZoneDescriptor : GumshoeActorDescriptor
{
    public override string Path => GumshoePaths.CageZone;
    protected override bool IsMoving => true;
}
public sealed class InterrogateAbilityDescriptor : GumshoeActorDescriptor
{
    public override string Path => GumshoePaths.UltimateAbility;
    protected override bool IsEquipment => true;
}
public sealed class InterrogateHatDescriptor : GumshoeActorDescriptor
{
    public override string Path => GumshoePaths.Hat;
}

public static class GumshoePaths
{
    public const string TripwireAbility = "/Game/Characters/Gumshoe/S0/Ability_4/Ability_Gumshoe_4_TripWire.Ability_Gumshoe_4_TripWire_C";
    public const string Tripwire = "/Game/Characters/Gumshoe/S0/Ability_4/GameObject_Gumshoe_4_TripWire.GameObject_Gumshoe_4_TripWire_C";
    public const string SecondWire = "/Game/Characters/Gumshoe/S0/Ability_4/GameObject_Gumshoe_4_TripWire_SecondWire.GameObject_Gumshoe_4_TripWire_SecondWire_C";
    public const string CameraAbility = "/Game/Characters/Gumshoe/S0/Ability_E/Ability_Gumshoe_E_Camera.Ability_Gumshoe_E_Camera_C";
    public const string Camera = "/Game/Characters/Gumshoe/S0/Ability_E/Pawn_Gumshoe_E_PossessableCamera.Pawn_Gumshoe_E_PossessableCamera_C";
    public const string Dart = "/Game/Characters/Gumshoe/S0/Ability_E/Projectile_Gumshoe_E_CameraTrackingDart.Projectile_Gumshoe_E_CameraTrackingDart_C";
    public const string TrackingDart = "/Game/Characters/Gumshoe/S0/Ability_E/GameObject_RemovableObject_GumshoeTrackingDart.GameObject_RemovableObject_GumshoeTrackingDart_C";
    public const string DartAbility = "/Game/Characters/Gumshoe/S0/Ability_E/Ability_Gumshoe_E_Camera_Dart.Ability_Gumshoe_E_Camera_Dart_C";
    public const string UnpossessAbility = "/Game/Characters/Gumshoe/S0/Ability_E/Ability_Gumshoe_E_UnpossessCamera.Ability_Gumshoe_E_UnpossessCamera_C";
    public const string CageAbility = "/Game/Characters/Gumshoe/S0/Ability_Q/Ability_Gumshoe_Q_CageTrap.Ability_Gumshoe_Q_CageTrap_C";
    public const string CageProjectile = "/Game/Characters/Gumshoe/S0/Ability_Q/Projectile_Gumshoe_Q_CageTrap.Projectile_Gumshoe_Q_CageTrap_C";
    public const string CageDevice = "/Game/Characters/Gumshoe/S0/Ability_Q/GameObject_Gumshoe_Q_CageTrap.GameObject_Gumshoe_Q_CageTrap_C";
    public const string CageZone = "/Game/Characters/Gumshoe/S0/Ability_Q/Zone_Gumshoe_Q_Cage.Zone_Gumshoe_Q_Cage_C";
    public const string UltimateAbility = "/Game/Characters/Gumshoe/S0/Ability_X/Ability_Gumshoe_X_InterrogateV2.Ability_Gumshoe_X_InterrogateV2_C";
    public const string Hat = "/Game/Characters/Gumshoe/S0/Ability_X/GameObject_Gumshoe_X_InterrogateHat.GameObject_Gumshoe_X_InterrogateHat_C";
}
