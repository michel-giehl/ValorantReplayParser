using Replay.Models.Descriptors;
using static Replay.Valorant.Descriptors.Agents.AbilityDescriptorRegistration;

namespace Replay.Valorant.Descriptors.Agents.Cable;

public static class CableDescriptors
{
    public static List<ExportGroupDescriptor> CreateDescriptors()
    {
        return
        [
            new CableAgentDescriptor(),
            Actor("/Game/Characters/Cable/S0/Ability_4/Ability_Cable_4_NetToss.Ability_Cable_4_NetToss_C", ["Owner", "Instigator", "CreatedByCharacter", "AttachParent", "bInPersistentData"]),
            Actor("/Game/Characters/Cable/S0/Ability_4/GameObject_Cable_4_RemovableNet.GameObject_Cable_4_RemovableNet_C", ["Owner", "Instigator", "AttachParent", "Target"]),
            Actor("/Game/Characters/Cable/S0/Ability_4/Patch_Cable_4_NetToss.Patch_Cable_4_NetToss_C", ["Owner", "Instigator"]),
            Actor("/Game/Characters/Cable/S0/Ability_4/Projectile_Cable_4_NetToss.Projectile_Cable_4_NetToss_C", ["Owner", "Instigator", "ReplicatedMovement"]),
            Actor("/Game/Characters/Cable/S0/Ability_E/Ability_Cable_E_CableJam.Ability_Cable_E_CableJam_C", ["Owner", "Instigator", "CreatedByCharacter", "AttachParent", "bInPersistentData"]),
            Actor("/Game/Characters/Cable/S0/Ability_E/GameObject_CableJamRoot.GameObject_CableJamRoot_C", ["Owner", "Instigator"]),
            Actor("/Game/Characters/Cable/S0/Ability_E/GameObject_CableJam_CableDeployer_Precomputed.GameObject_CableJam_CableDeployer_Precomputed_C", ["Owner", "Instigator"]),
            Actor("/Game/Characters/Cable/S0/Ability_E/Projectile_CableJam_InAir.Projectile_CableJam_InAir_C", ["Owner", "Instigator", "ReplicatedMovement"]),
            Actor("/Game/Characters/Cable/S0/Ability_Q/Ability_Cable_Q_SoundSensor.Ability_Cable_Q_SoundSensor_C", ["Owner", "Instigator", "CreatedByCharacter", "AttachParent", "bInPersistentData"]),
            Actor("/Game/Characters/Cable/S0/Ability_Q/GameObject_SoundSensor_SweetSpotFissure.GameObject_SoundSensor_SweetSpotFissure_C", ["Owner", "Instigator"]),
            Actor("/Game/Characters/Cable/S0/Ability_Q/GameObject_StealthingTrap_SoundSensor.GameObject_StealthingTrap_SoundSensor_C", ["Owner", "Instigator"]),
            Actor("/Game/Characters/Cable/S0/Ability_X/Ability_Cable_X_FishingHook.Ability_Cable_X_FishingHook_C", ["Owner", "Instigator", "CreatedByCharacter", "AttachParent", "bInPersistentData"]),
            Actor("/Game/Characters/Cable/S0/Ability_X/Actor_FishingHook.Actor_FishingHook_C", ["Owner", "Instigator", "Mother Node", "Active Tether"]),
            Actor("/Game/Characters/Cable/S0/Ability_X/GameObject_FishingHook_BouncingTrajectoryWarning.GameObject_FishingHook_BouncingTrajectoryWarning_C", ["Owner", "Instigator"]),
            Actor("/Game/Characters/Cable/S0/Ability_X/GameObject_FishingHook_CageSphere.GameObject_FishingHook_CageSphere_C", ["Owner", "Instigator", "AttachParent", "Spline Object"]),
            Actor("/Game/Characters/Cable/S0/Ability_X/GameObject_FishingHook_EndOfTrajectoryWarning.GameObject_FishingHook_EndOfTrajectoryWarning_C", ["Owner", "Instigator"]),
            Actor("/Game/Characters/Cable/S0/Ability_X/GameObject_MotherNode.GameObject_MotherNode_C", ["Owner", "Instigator", "Cocoon ", "Game Object Spline"]),
            Actor("/Game/Characters/Cable/S0/Ability_X/GameObject_Spline.GameObject_Spline_C", ["Owner", "Instigator"]),
        ];
    }

    public static IReadOnlyList<ClassNetCacheDescriptor> CreateClassNetCacheDescriptors() => MergeClassNetCaches(
    [
        Rpc("/Game/Characters/Cable/S0/Ability_4/Patch_Cable_4_NetToss.Patch_Cable_4_NetToss_C", "BeginFadeOut", 0),
        Rpc("/Game/Characters/Cable/S0/Ability_4/Projectile_Cable_4_NetToss.Projectile_Cable_4_NetToss_C", "MulticastStopProjectile", 3),
        Rpc("/Game/Characters/Cable/S0/Ability_E/GameObject_CableJam_CableDeployer_Precomputed.GameObject_CableJam_CableDeployer_Precomputed_C", "EnableNodeCollision", 0),
        Rpc("/Game/Characters/Cable/S0/Ability_E/GameObject_CableJam_CableDeployer_Precomputed.GameObject_CableJam_CableDeployer_Precomputed_C", "SolidifyCableEvent", 2),
        Rpc("/Game/Characters/Cable/S0/Ability_E/Projectile_CableJam_InAir.Projectile_CableJam_InAir_C", "MulticastStopProjectile", 3),
        ObjectRpc("/Game/Characters/Cable/S0/Ability_Q/Ability_Cable_Q_SoundSensor.Ability_Cable_Q_SoundSensor_C", "OnRecalled", 0, "RecalledActor"),
        Rpc("/Game/Characters/Cable/S0/Ability_Q/GameObject_SoundSensor_SweetSpotFissure.GameObject_SoundSensor_SweetSpotFissure_C", "DetonateZone", 0),
        Rpc("/Game/Characters/Cable/S0/Ability_Q/GameObject_StealthingTrap_SoundSensor.GameObject_StealthingTrap_SoundSensor_C", "MulticastBeginStealthTrap", 1),
        Rpc("/Game/Characters/Cable/S0/Ability_Q/GameObject_StealthingTrap_SoundSensor.GameObject_StealthingTrap_SoundSensor_C", "MulticastDeployed", 2),
        Rpc("/Game/Characters/Cable/S0/Ability_Q/GameObject_StealthingTrap_SoundSensor.GameObject_StealthingTrap_SoundSensor_C", "ReplDetectedValidTarget", 0),
        Rpc("/Game/Characters/Cable/S0/Ability_X/Actor_FishingHook.Actor_FishingHook_C", "Multicast Mother Node Spawned", 0),
        Rpc("/Game/Characters/Cable/S0/Ability_X/GameObject_FishingHook_BouncingTrajectoryWarning.GameObject_FishingHook_BouncingTrajectoryWarning_C", "Multicast Target Captured", 2),
        ObjectRpc("/Game/Characters/Cable/S0/Ability_X/GameObject_FishingHook_CageSphere.GameObject_FishingHook_CageSphere_C", "StartRescueMission", 2, "CagedPlayer"),
        Rpc("/Game/Characters/Cable/S0/Ability_X/GameObject_FishingHook_EndOfTrajectoryWarning.GameObject_FishingHook_EndOfTrajectoryWarning_C", "Multicast Target Captured", 2),
        GeometryRpc("/Game/Characters/Cable/S0/Ability_E/GameObject_CableJam_CableDeployer_Precomputed.GameObject_CableJam_CableDeployer_Precomputed_C", "MulticastInitialize", 1, "wall"),
        GeometryRpc("/Game/Characters/Cable/S0/Ability_X/GameObject_FishingHook_BouncingTrajectoryWarning.GameObject_FishingHook_BouncingTrajectoryWarning_C", "Multicast Initialize Segment", 0, "segment"),
        GeometryRpc("/Game/Characters/Cable/S0/Ability_X/Actor_FishingHook.Actor_FishingHook_C", "Multicast Stop Collider", 1, "pulling"),
        GeometryRpc("/Game/Characters/Cable/S0/Ability_X/GameObject_Spline.GameObject_Spline_C", "Multicast_InitializeSpline", 0, "spline"),
    ]);

    private static ClassNetCacheDescriptor GeometryRpc(string path, string name, uint handle, string kind) =>
        new(path + "_ClassNetCache", [new RpcDescriptor
        {
            Name = name, FunctionExportPath = path + ":" + name, Handle = handle,
            Categories = ExportCategory.Ability, ParameterDescriptor = new CableGeometryParameters(path + ":" + name, kind),
        }]);
}
