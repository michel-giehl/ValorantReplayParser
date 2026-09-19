using Replay.Models.Descriptors;
using Replay.Valorant.Smokes.Descriptors;

namespace Replay.Valorant.Descriptors.Agents.Smonk;

public static class SmonkDescriptors
{
    public static List<ExportGroupDescriptor> CreateDescriptors()
    {
        return
        [
            new SmonkAgentDescriptor(),
            new SmonkSmokeAbilityDescriptor(),
            new SmonkPostDeathSmokeAbilityDescriptor(),
            new SmonkSmokeDescriptor(),
            new SmonkPersistentSmokeDescriptor(),
        ];
    }

    public static IReadOnlyList<ClassNetCacheDescriptor> CreateClassNetCacheDescriptors() =>
    [
        SmokeClassNetCacheDescriptors.CreateMovedToPersistentData(SmonkSmokePaths.Ability, 0),
    ];
}
