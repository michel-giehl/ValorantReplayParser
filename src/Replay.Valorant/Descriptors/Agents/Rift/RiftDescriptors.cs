using Replay.Models.Descriptors;
using Replay.Valorant.Smokes.Descriptors;

namespace Replay.Valorant.Descriptors.Agents.Rift;

public static class RiftDescriptors
{
    public static List<ExportGroupDescriptor> CreateDescriptors()
    {
        return
        [
            new RiftAgentDescriptor(),
            new RiftSmokeAbilityDescriptor(),
            new RiftWorldTargetingSmokeAbilityDescriptor(),
            new RiftSmokeZoneDescriptor(),
            new RiftFakeSmokeZoneDescriptor(),
        ];
    }

    public static IReadOnlyList<ClassNetCacheDescriptor> CreateClassNetCacheDescriptors() =>
    [
        SmokeClassNetCacheDescriptors.CreateMovedToPersistentData(RiftSmokePaths.Ability, 0),
    ];
}
