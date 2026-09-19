using Replay.Models.Descriptors;
using Replay.Valorant.Flashes.Descriptors;

namespace Replay.Valorant.Descriptors.Agents.Grenadier;

public static class GrenadierDescriptors
{
    public static List<ExportGroupDescriptor> CreateDescriptors()
    {
        return
        [
            new GrenadierAgentDescriptor(),
            new KayoOverhandFlashProjectileDescriptor(),
            new KayoUnderhandFlashProjectileDescriptor(),
        ];
    }

    public static IReadOnlyList<ClassNetCacheDescriptor> CreateClassNetCacheDescriptors() =>
    [
        FlashProjectileClassNetCacheDescriptors.CreateStopProjectile(FlashPaths.KayoOverhandProjectile),
        FlashProjectileClassNetCacheDescriptors.CreateStopProjectile(FlashPaths.KayoUnderhandProjectile),
    ];
}
