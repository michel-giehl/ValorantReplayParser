using Replay.Models.Descriptors;
using Replay.Valorant.Flashes.Descriptors;
using static Replay.Valorant.Descriptors.Agents.AbilityDescriptorRegistration;

namespace Replay.Valorant.Descriptors.Agents.Paprika;

public static class PaprikaDescriptors
{
    private const string PullBlastAbilityPath = "/Game/Characters/Paprika/S0/Ability_4/Ability_Paprika_4_PullBlast.Ability_Paprika_4_PullBlast_C";
    private const string RedirectPullAbilityPath = "/Game/Characters/Paprika/S0/Ability_E/Ability_Paprika_E_RedirectPull.Ability_Paprika_E_RedirectPull_C";
    private const string RedirectPullProjectilePath = "/Game/Characters/Paprika/S0/Ability_E/Projectile_Paprika_E_RedirectPull.Projectile_Paprika_E_RedirectPull_C";
    private const string RedirectPullUnderhandProjectilePath = "/Game/Characters/Paprika/S0/Ability_E/Projectile_Paprika_E_RedirectPull_Underhand.Projectile_Paprika_E_RedirectPull_Underhand_C";
    private const string RedirectFlashAbilityPath = "/Game/Characters/Paprika/S0/Ability_Q/Ability_Paprika_Q_RedirectFlash.Ability_Paprika_Q_RedirectFlash_C";
    private const string FullBendPopAbilityPath = "/Game/Characters/Paprika/S0/Ability_X/Ability_Paprika_X_FullBendPop.Ability_Paprika_X_FullBendPop_C";
    private const string FullBendPopProjectilePath = "/Game/Characters/Paprika/S0/Ability_X/Projectile_Paprika_X_FullBendPop.Projectile_Paprika_X_FullBendPop_C";
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
            Projectile(RedirectPullUnderhandProjectilePath, "Selected Point"),
            Equipment(RedirectFlashAbilityPath, "Projectile"),
            // Flashbulb's projectiles carry the flash identity: the flash enricher reads their owner and movement.
            Projectile(FlashPaths.HushOverhandProjectile, "Selected Point"),
            Projectile(FlashPaths.HushUnderhandProjectile, "Selected Point"),
            Equipment(FullBendPopAbilityPath),
            Projectile(FullBendPopProjectilePath),
            // Suspension's levitation field; each levitated player's effect names it as context.
            Owned("/Game/Characters/Paprika/S0/Ability_X/GameObject_Paprika_X_LevitateZone.GameObject_Paprika_X_LevitateZone_C"),
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
        .. UnderhandRedirectProjectileRpcs(RedirectPullUnderhandProjectilePath),
        Rpc(RedirectFlashAbilityPath, MulticastOnItemMovedToPersistentData, 1),
        .. RedirectProjectileRpcs(FlashPaths.HushOverhandProjectile),
        .. UnderhandRedirectProjectileRpcs(FlashPaths.HushUnderhandProjectile),
        Rpc(FullBendPopAbilityPath, MulticastOnItemMovedToPersistentData, 2),
        Rpc(FullBendPopProjectilePath, "MulticastStopProjectile", 3),
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

    /// <summary>Underhand variants add a cosmetic lerp at handle 0, shifting the shared redirect RPCs by one.</summary>
    private static ClassNetCacheDescriptor[] UnderhandRedirectProjectileRpcs(string path) =>
    [
        Rpc(path, "MulticastStartCosmeticLerp", 0),
        Rpc(path, "DetachProjectileForRedirect", 1),
        Rpc(path, "HideRedirectGuides", 2),
        Rpc(path, "MulticastStartPostRedirectCosmeticTrail", 3),
        Rpc(path, "MulticastStopInitalFuseTimerFXC", 4),
        Rpc(path, "MulticastUnhideFromHolster", 5),
        Rpc(path, "MulticastStopProjectile", 9),
    ];
}
