using Replay.Models.Descriptors;
using Replay.Valorant.Smokes.Descriptors;

namespace Replay.Valorant.Descriptors.Agents.Iris;

public static class IrisDescriptors
{
    public static List<ExportGroupDescriptor> CreateDescriptors()
    {
        return
        [
            new IrisAgentDescriptor(),
            new IrisSmokeAbilityDescriptor(),
            new IrisSmokeDescriptor(),
        ];
    }

    public static IReadOnlyList<ClassNetCacheDescriptor> CreateClassNetCacheDescriptors() =>
    [
        SmokeClassNetCacheDescriptors.CreateMovedToPersistentData(IrisSmokePaths.Ability, 0),
    ];
}
