using Replay.Models.Descriptors;
using Replay.Valorant.Flashes.Descriptors;

namespace Replay.Valorant.Descriptors.Agents.Phoenix;

public static class PhoenixDescriptors
{
    public static List<ExportGroupDescriptor> CreateDescriptors()
    {
        return
        [
            new PhoenixAgentDescriptor(),
            new FlameWallDescriptor(),
            new PhoenixLeftFlashProjectileDescriptor(),
            new PhoenixRightFlashProjectileDescriptor(),
        ];
    }

    public static IReadOnlyList<ClassNetCacheDescriptor> CreateClassNetCacheDescriptors() =>
    [
        FlashProjectileClassNetCacheDescriptors.CreateStopProjectile(FlashPaths.PhoenixLeftProjectile),
        FlashProjectileClassNetCacheDescriptors.CreateStopProjectile(FlashPaths.PhoenixRightProjectile),
    ];
}
