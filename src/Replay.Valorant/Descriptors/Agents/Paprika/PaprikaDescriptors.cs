using Replay.Models.Descriptors;
using static Replay.Valorant.Descriptors.Agents.AbilityDescriptorRegistration;

namespace Replay.Valorant.Descriptors.Agents.Paprika;

public static class PaprikaDescriptors
{
    private const string PullBlastAbilityPath = "/Game/Characters/Paprika/S0/Ability_4/Ability_Paprika_4_PullBlast.Ability_Paprika_4_PullBlast_C";
    private const string RedirectPullAbilityPath = "/Game/Characters/Paprika/S0/Ability_E/Ability_Paprika_E_RedirectPull.Ability_Paprika_E_RedirectPull_C";
    private const string RedirectPullProjectilePath = "/Game/Characters/Paprika/S0/Ability_E/Projectile_Paprika_E_RedirectPull.Projectile_Paprika_E_RedirectPull_C";
    private const string RedirectFlashAbilityPath = "/Game/Characters/Paprika/S0/Ability_Q/Ability_Paprika_Q_RedirectFlash.Ability_Paprika_Q_RedirectFlash_C";
    private const string RedirectFlashProjectilePath = "/Game/Characters/Paprika/S0/Ability_Q/Projectile_Paprika_Q_RedirectFlash.Projectile_Paprika_Q_RedirectFlash_C";
    private const string FullBendPopAbilityPath = "/Game/Characters/Paprika/S0/Ability_X/Ability_Paprika_X_FullBendPop.Ability_Paprika_X_FullBendPop_C";
    private const string TelekinesisPassivePath = "/Game/Characters/Paprika/S0/Passive/Ability_Paprika_Passive_Telekinesis.Ability_Paprika_Passive_Telekinesis_C";

    public static List<ExportGroupDescriptor> CreateDescriptors()
    {
        return
        [
            new PaprikaAgentDescriptor(),
            new PaprikaRangedUsableComponentDescriptor(),
            new PaprikaForceProxyManagerUserDescriptor(),
            Equipment(PullBlastAbilityPath, "Spawned Blast"),
            Owned("/Game/Characters/Paprika/S0/Ability_4/GameObject_Paprika_4_PullBlast.GameObject_Paprika_4_PullBlast_C"),
            Equipment(RedirectPullAbilityPath, "Projectile"),
            Owned("/Game/Characters/Paprika/S0/Ability_E/GameObject_Paprika_E_RedirectPullArea.GameObject_Paprika_E_RedirectPullArea_C"),
            Projectile(RedirectPullProjectilePath, "Selected Point"),
            Equipment(RedirectFlashAbilityPath, "Projectile"),
            Projectile(RedirectFlashProjectilePath, "Selected Point"),
            Equipment(FullBendPopAbilityPath),
            Owned("/Game/Characters/Paprika/S0/Ability_X/Actor_Paprika_X_ForceModuleProxy.Actor_Paprika_X_ForceModuleProxy_C"),
            Actor("/Game/Characters/Paprika/S0/Ability_X/GameObject_Paprika_X_ForceProxyManager.GameObject_Paprika_X_ForceProxyManager_C", ["Proxies"]),
            Equipment(TelekinesisPassivePath),
        ];
    }

    public static IReadOnlyList<ClassNetCacheDescriptor> CreateClassNetCacheDescriptors() => MergeClassNetCaches(
    [
        Rpc(PaprikaRangedUsableComponentDescriptor.ExportPath, "Multi_StartHUDOnRoundStart", 0),
        Rpc(PullBlastAbilityPath, MulticastOnItemMovedToPersistentData, 0),
        Rpc(RedirectPullAbilityPath, MulticastOnItemMovedToPersistentData, 1),
        .. RedirectProjectileRpcs(RedirectPullProjectilePath),
        Rpc(RedirectFlashAbilityPath, MulticastOnItemMovedToPersistentData, 1),
        .. RedirectProjectileRpcs(RedirectFlashProjectilePath),
        Rpc(FullBendPopAbilityPath, MulticastOnItemMovedToPersistentData, 2),
        Rpc(TelekinesisPassivePath, MulticastOnItemMovedToPersistentData, 0),
    ]);

    private static ClassNetCacheDescriptor[] RedirectProjectileRpcs(string path) =>
    [
        Rpc(path, "DetachProjectileForRedirect", 0),
        Rpc(path, "HideRedirectGuides", 1),
        Rpc(path, "MulticastStartPostRedirectCosmeticTrail", 2),
        Rpc(path, "MulticastStopInitalFuseTimerFXC", 3),
        Rpc(path, "MulticastUnhideFromHolster", 4),
        Rpc(path, "MulticastStopProjectile", 8),
    ];
}
