using Replay.Models.Descriptors;
using Replay.Valorant.Flashes.Descriptors;

namespace Replay.Valorant.Descriptors.Agents.Stealth;

public static class StealthDescriptors
{
    public static List<ExportGroupDescriptor> CreateDescriptors()
    {
        return
        [
            new StealthAgentDescriptor(),
            new YoruFlashProjectileDescriptor(),
        ];
    }

    public static IReadOnlyList<ClassNetCacheDescriptor> CreateClassNetCacheDescriptors() =>
    [
        FlashProjectileClassNetCacheDescriptors.CreateStopProjectile(FlashPaths.YoruProjectile),
    ];
}
