using Replay.Models.Descriptors;
using static Replay.Valorant.Descriptors.Agents.AbilityDescriptorRegistration;

namespace Replay.Valorant.Descriptors.Agents.Deadeye;

public static class DeadeyeDescriptors
{
    public static List<ExportGroupDescriptor> CreateDescriptors()
    {
        return
        [
            new DeadeyeAgentDescriptor(),
            Actor("/Game/Characters/Deadeye/S0/Ability_4/Ability_Deadeye_4_Trap.Ability_Deadeye_4_Trap_C", ["Owner", "Instigator", "CreatedByCharacter", "AttachParent", "bInPersistentData"]),
            Actor("/Game/Characters/Deadeye/S0/Ability_4/Patch_Deadeye_E_Slow_Large.Patch_Deadeye_E_Slow_Large_C", ["Owner", "Instigator"]),
            Actor("/Game/Characters/Deadeye/S0/Ability_4/Projectile_Deadeye_4_Trap_Dart.Projectile_Deadeye_4_Trap_Dart_C", ["Owner", "Instigator", "ReplicatedMovement"]),
            Actor("/Game/Characters/Deadeye/S0/Ability_E/Ability_Deadeye_E_Teleporter_Tethers.Ability_Deadeye_E_Teleporter_Tethers_C", ["Owner", "Instigator", "CreatedByCharacter", "AttachParent", "bInPersistentData"]),
            Actor("/Game/Characters/Deadeye/S0/Ability_Q/Ability_Deadeye_Q_Pistol.Ability_Deadeye_Q_Pistol_C", ["Owner", "Instigator", "CreatedByCharacter", "AttachParent", "bInPersistentData"]),
            Actor("/Game/Characters/Deadeye/S0/Ability_Q/Gun/Gun_Deadeye_Q_Pistol.Gun_Deadeye_Q_Pistol_C", ["Owner", "Instigator", "CreatedByCharacter", "AttachParent"]),
            Actor("/Game/Characters/Deadeye/S0/Ability_X/Ability_Deadeye_X_Giantslayer_Prototype_DanPrototype.Ability_Deadeye_X_Giantslayer_Prototype_DanPrototype_C", ["Owner", "Instigator", "CreatedByCharacter", "AttachParent", "bInPersistentData"]),
            Actor("/Game/Characters/Deadeye/S0/Ability_X/Gun_Giantslayer/Gun_Deadeye_X_Giantslayer_Prototype_FIreRatePrototype.Gun_Deadeye_X_Giantslayer_Prototype_FireRatePrototype_C", ["Owner", "Instigator", "CreatedByCharacter", "AttachParent"]),
            new ChamberPresenceDescriptor(ChamberPresenceDescriptor.Trademark),
            new ChamberPresenceDescriptor(ChamberPresenceDescriptor.Rendezvous),
        ];
    }

    public static IReadOnlyList<ClassNetCacheDescriptor> CreateClassNetCacheDescriptors() => MergeClassNetCaches(
    [
        Rpc("/Game/Characters/Deadeye/S0/Ability_4/Patch_Deadeye_E_Slow_Large.Patch_Deadeye_E_Slow_Large_C", "BeginFadeOut", 0),
        Rpc("/Game/Characters/Deadeye/S0/Ability_E/Ability_Deadeye_E_Teleporter_Tethers.Ability_Deadeye_E_Teleporter_Tethers_C", "MulticastRecallTeleport", 2),
        Rpc("/Game/Characters/Deadeye/S0/Ability_E/GameObject_Deadeye_E_Teleporter_Tether.GameObject_Deadeye_E_Teleporter_Tether_C", "TeleporterUsed", 0),
    ]);
}
