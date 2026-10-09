using Replay.Models.Descriptors;
using static Replay.Valorant.Descriptors.Agents.AbilityDescriptorRegistration;

namespace Replay.Valorant.Descriptors.Agents.Killjoy;

public static class KilljoyDescriptors
{
    public static List<ExportGroupDescriptor> CreateDescriptors()
    {
        return
        [
            new KilljoyAgentDescriptor(),
            Actor("/Game/Characters/Killjoy/S0/Ability_4/Ability_Killjoy_4_RemoteBees_MultiDetonate.Ability_Killjoy_4_RemoteBees_MultiDetonate_C", ["Owner", "Instigator", "CreatedByCharacter", "AttachParent", "bInPersistentData"]),
            Actor("/Game/Characters/Killjoy/S0/Ability_4/GameObject_Killjoy_4_BeeSwarm_Damage.GameObject_Killjoy_4_BeeSwarm_Damage_C", ["Owner", "Instigator"]),
            Actor("/Game/Characters/Killjoy/S0/Ability_4/Projectile_Killjoy_4_RemoteBees_MultiDetonate.Projectile_Killjoy_4_RemoteBees_MultiDetonate_C", ["Owner", "Instigator", "ReplicatedMovement"]),
            Actor("/Game/Characters/Killjoy/S0/Ability_E/Ability_Killjoy_E_Turret.Ability_Killjoy_E_Turret_C", ["Owner", "Instigator", "CreatedByCharacter", "AttachParent", "DeployedActor", "bInPersistentData"]),
            Actor("/Game/Characters/Killjoy/S0/Ability_E/Ability_Killjoy_E_TurretAttack.Ability_Killjoy_E_TurretAttack_C", ["Owner", "Instigator", "AttachParent"]),
            Actor("/Game/Characters/Killjoy/S0/Ability_E/Pawn_Killjoy_E_Turret.Pawn_Killjoy_E_Turret_C", ["Owner", "Instigator", "ReplicatedMovement"]),
            Actor("/Game/Characters/Killjoy/S0/Ability_Q/Ability_Killjoy_Q_Alarmbot.Ability_Killjoy_Q_Alarmbot_C", ["Owner", "Instigator", "CreatedByCharacter", "AttachParent", "DeployedActor", "bInPersistentData"]),
            Actor("/Game/Characters/Killjoy/S0/Ability_Q/Pawn_Killjoy_Q_StealthAlarmbot.Pawn_Killjoy_Q_StealthAlarmbot_C", ["Owner", "Instigator", "IsBurrowed", "ReplicatedMovement"]),
            Actor("/Game/Characters/Killjoy/S0/Ability_X/Ability_Killjoy_X_Bomb.Ability_Killjoy_X_Bomb_C", ["Owner", "Instigator", "CreatedByCharacter", "AttachParent", "bInPersistentData"]),
            Actor("/Game/Characters/Killjoy/S0/Ability_X/GameObject_Killjoy_X_Bomb.GameObject_Killjoy_X_Bomb_C", ["Owner", "Instigator"]),
            Actor("/Game/Characters/Killjoy/S0/Ability_X/GameObject_Killjoy_X_Shockwave.GameObject_Killjoy_X_Shockwave_C", ["Owner", "Instigator"]),
        ];
    }

    public static IReadOnlyList<ClassNetCacheDescriptor> CreateClassNetCacheDescriptors() => MergeClassNetCaches(
    [
        Rpc("/Game/Characters/Killjoy/S0/Ability_4/Projectile_Killjoy_4_RemoteBees_MultiDetonate.Projectile_Killjoy_4_RemoteBees_MultiDetonate_C", "ClientExplode", 0),
        Rpc("/Game/Characters/Killjoy/S0/Ability_4/Projectile_Killjoy_4_RemoteBees_MultiDetonate.Projectile_Killjoy_4_RemoteBees_MultiDetonate_C", "MulticastStopProjectile", 4),
    ]);
}
