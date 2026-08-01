using Replay.Models.Descriptors;
using Replay.Valorant.Descriptors.Agents.Mage.TidalWave;

namespace Replay.Valorant.Descriptors.Agents.Mage;

public static class MageDescriptors
{
    public static List<ExportGroupDescriptor> CreateDescriptors()
    {
        return
        [
            new MageAgentDescriptor(),
            new MageWallDescriptor(),
            new CoveAbilityDescriptor(),
            .. TidalWaveDescriptors.CreateExportDescriptors(),
        ];
    }

    public static IReadOnlyList<ClassNetCacheDescriptor> CreateClassNetCacheDescriptors() =>
        TidalWaveDescriptors.CreateClassNetCacheDescriptors();
}
