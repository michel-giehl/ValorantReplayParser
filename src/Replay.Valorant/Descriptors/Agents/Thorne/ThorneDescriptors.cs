using Replay.Models.Descriptors;
using Replay.Valorant.Walls.Descriptors;

namespace Replay.Valorant.Descriptors.Agents.Thorne;

public static class ThorneDescriptors
{
    public static List<ExportGroupDescriptor> CreateDescriptors()
    {
        return
        [
            new ThorneAgentDescriptor(),
            new SageWallDescriptor(),
            new SageWallSegmentDescriptor(),
        ];
    }

    public static IReadOnlyList<ClassNetCacheDescriptor> CreateClassNetCacheDescriptors() =>
    [
        new SageWallSegmentClassNetCacheDescriptor(),
    ];
}
