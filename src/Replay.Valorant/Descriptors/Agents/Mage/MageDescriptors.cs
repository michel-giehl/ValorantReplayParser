using Replay.Models.Descriptors;
using Replay.Valorant.Descriptors.Agents.Mage.TidalWave;
using Replay.Valorant.Nearsights.Descriptors;

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
            new HarborNearsightProjectileDescriptor(),
            new HarborNearsightSourceDescriptor(),
            .. TidalWaveDescriptors.CreateExportDescriptors(),
        ];
    }

    public static IReadOnlyList<ClassNetCacheDescriptor> CreateClassNetCacheDescriptors() =>
    [
        NearsightProjectileClassNetCacheDescriptors.CreateStopProjectile(NearsightPaths.HarborProjectile, 3),
        .. TidalWaveDescriptors.CreateClassNetCacheDescriptors(),
    ];
}
