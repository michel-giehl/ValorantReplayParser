using Replay.Models.Descriptors;
using Replay.Valorant.Flashes.Descriptors;
using Replay.Valorant.Walls.Descriptors;

namespace Replay.Valorant.Descriptors.Agents.Nox;

public static class NoxDescriptors
{
    public static List<ExportGroupDescriptor> CreateDescriptors()
    {
        return
        [
            new NoxAgentDescriptor(),
            new VyseFlashSourceDescriptor(),
            new VyseWallTrapDescriptor(),
            new VyseWallDescriptor(),
        ];
    }

    public static IReadOnlyList<ClassNetCacheDescriptor> CreateClassNetCacheDescriptors() =>
    [
        new VyseFlashSourceClassNetCacheDescriptor(),
        new VyseWallTrapClassNetCacheDescriptor(),
        new VyseWallClassNetCacheDescriptor(),
    ];
}
