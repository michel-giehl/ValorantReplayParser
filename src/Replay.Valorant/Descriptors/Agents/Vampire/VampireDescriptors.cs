using Replay.Models.Descriptors;
using Replay.Valorant.Nearsights.Descriptors;

namespace Replay.Valorant.Descriptors.Agents.Vampire;

public static class VampireDescriptors
{
    public static List<ExportGroupDescriptor> CreateDescriptors()
    {
        return
        [
            new VampireAgentDescriptor(),
            new ReynaNearsightProjectileDescriptor(),
            new ReynaNearsightSourceDescriptor(),
        ];
    }

    public static IReadOnlyList<ClassNetCacheDescriptor> CreateClassNetCacheDescriptors() =>
    [
        NearsightProjectileClassNetCacheDescriptors.CreateStopProjectile(NearsightPaths.ReynaProjectile, 4),
    ];
}
