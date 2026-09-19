using Replay.Models.Descriptors;
using Replay.Valorant.Smokes.Descriptors;

namespace Replay.Valorant.Descriptors.Agents.Sarge;

public static class SargeDescriptors
{
    public static List<ExportGroupDescriptor> CreateDescriptors()
    {
        return
        [
            new SargeAgentDescriptor(),
            new SargeSmokeAbilityDescriptor(),
            new SargeSmokeManagerDescriptor(),
            new SargeSmokeDescriptor(),
        ];
    }

    public static IReadOnlyList<ClassNetCacheDescriptor> CreateClassNetCacheDescriptors() =>
    [
        SmokeClassNetCacheDescriptors.CreateMovedToPersistentData(SargeSmokePaths.Ability, 0),
    ];
}
