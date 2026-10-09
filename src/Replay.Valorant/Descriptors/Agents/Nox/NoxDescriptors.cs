using Replay.Models.Descriptors;
using static Replay.Valorant.Descriptors.Agents.AbilityDescriptorRegistration;
using Replay.Valorant.Flashes.Descriptors;
using Replay.Valorant.Walls.Descriptors;

namespace Replay.Valorant.Descriptors.Agents.Nox;

public static class NoxDescriptors
{
    public static List<ExportGroupDescriptor> CreateDescriptors()
    {
        return
        [
            new NoxAgentDescriptor(),
            Actor("/Game/Characters/Nox/S0/Ability_4/Ability_Nox_BarbedWire.Ability_Nox_BarbedWire_C", ["Owner", "Instigator", "CreatedByCharacter", "AttachParent", "bInPersistentData"]),
            Actor("/Game/Characters/Nox/S0/Ability_4/GameObject_Nox_BarbedWire.GameObject_Nox_BarbedWire_C", ["Owner", "Instigator"]),
            Actor("/Game/Characters/Nox/S0/Ability_4/Patch_Nox_BarbedWire.Patch_Nox_BarbedWire_C", ["Owner", "Instigator"]),
            Actor("/Game/Characters/Nox/S0/Ability_4/Projectile_Nox_BarbedWire.Projectile_Nox_BarbedWire_C", ["Owner", "Instigator", "bHidden", "ReplicatedMovement"]),
            Actor("/Game/Characters/Nox/S0/Ability_E/Ability_Nox_FlashTrap.Ability_Nox_FlashTrap_C", ["Owner", "Instigator", "CreatedByCharacter", "AttachParent", "FlashTrap", "bInPersistentData"]),
            Actor("/Game/Characters/Nox/S0/Ability_Q/Ability_Nox_Wall.Ability_Nox_Wall_C", ["Owner", "Instigator", "CreatedByCharacter", "AttachParent", "bInPersistentData"]),
            Actor("/Game/Characters/Nox/S0/Ability_X/Ability_Nox_DisarmPulse.Ability_Nox_DisarmPulse_C", ["Owner", "Instigator", "CreatedByCharacter", "AttachParent", "bInPersistentData"]),
            Actor("/Game/Characters/Nox/S0/Ability_X/Gameobject_Nox_DisarmPulse.Gameobject_Nox_DisarmPulse_C", ["Owner", "Instigator"]),
            new VyseFlashSourceDescriptor(),
            new VyseWallTrapDescriptor(),
            new VyseWallDescriptor(),
        ];
    }

    public static IReadOnlyList<ClassNetCacheDescriptor> CreateClassNetCacheDescriptors() => MergeClassNetCaches(
    [
        ObjectRpc("/Game/Characters/Nox/S0/Ability_4/Ability_Nox_BarbedWire.Ability_Nox_BarbedWire_C", "AuthOnRecalled", 0, "RecalledActor"),
        Rpc("/Game/Characters/Nox/S0/Ability_4/Patch_Nox_BarbedWire.Patch_Nox_BarbedWire_C", "BeginFadeOut", 1),
        Rpc("/Game/Characters/Nox/S0/Ability_4/Projectile_Nox_BarbedWire.Projectile_Nox_BarbedWire_C", "MulticastStopProjectile", 3),
        ObjectRpc("/Game/Characters/Nox/S0/Ability_Q/Ability_Nox_Wall.Ability_Nox_Wall_C", "OnRecalled", 0, "RecalledActor"),
        Rpc("/Game/Characters/Nox/S0/Ability_Q/GameObject_Nox_WallTrap.GameObject_Nox_WallTrap_C", "MulticastStartTrapOutroEffects", 1),
        new VyseFlashSourceClassNetCacheDescriptor(),
        new VyseWallTrapClassNetCacheDescriptor(),
        new VyseWallClassNetCacheDescriptor(),
    ]);
}
