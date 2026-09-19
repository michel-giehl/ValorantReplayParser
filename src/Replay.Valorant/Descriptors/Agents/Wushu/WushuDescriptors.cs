using Replay.Models.Descriptors;
using Replay.Valorant.Smokes.Descriptors;

namespace Replay.Valorant.Descriptors.Agents.Wushu;

public static class WushuDescriptors
{
    public static List<ExportGroupDescriptor> CreateDescriptors()
    {
        return
        [
            new WushuAgentDescriptor(),
            new WushuSmokeAbilityDescriptor(),
            new WushuSmokeZoneDescriptor(),
            new WushuSmokeProjectileDescriptor(),
        ];
    }

    public static IReadOnlyList<ClassNetCacheDescriptor> CreateClassNetCacheDescriptors() =>
    [
        SmokeClassNetCacheDescriptors.CreateMovedToPersistentData(WushuSmokePaths.Ability, 1),
        SmokeClassNetCacheDescriptors.CreateStopProjectile(WushuSmokePaths.Projectile, 3),
    ];
}
