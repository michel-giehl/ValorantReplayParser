using Replay.Models.Descriptors;
using Replay.Valorant.Flashes.Descriptors;

namespace Replay.Valorant.Descriptors.Agents.Breach;

public static class BreachDescriptors
{
    public static List<ExportGroupDescriptor> CreateDescriptors()
    {
        return
        [
            new BreachAgentDescriptor(),
            new BreachFlashProjectileDescriptor(),
        ];
    }

    public static IReadOnlyList<ClassNetCacheDescriptor> CreateClassNetCacheDescriptors() =>
    [
        FlashProjectileClassNetCacheDescriptors.CreateStopProjectile(FlashPaths.BreachProjectile),
    ];
}
