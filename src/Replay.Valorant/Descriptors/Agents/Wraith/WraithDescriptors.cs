using Replay.Models.Descriptors;
using Replay.Valorant.Nearsights.Descriptors;

namespace Replay.Valorant.Descriptors.Agents.Wraith;

public static class WraithDescriptors
{
    public static List<ExportGroupDescriptor> CreateDescriptors()
    {
        return
        [
            new WraithAgentDescriptor(),
            new DarkCoverAbilityDescriptor(),
            new OmenNearsightProjectileDescriptor(),
        ];
    }

    public static IReadOnlyList<ClassNetCacheDescriptor> CreateClassNetCacheDescriptors() =>
    [
        NearsightProjectileClassNetCacheDescriptors.CreateStopProjectile(NearsightPaths.OmenProjectile, 3),
    ];
}
