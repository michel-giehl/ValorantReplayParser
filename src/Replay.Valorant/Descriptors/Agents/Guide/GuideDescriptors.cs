using Replay.Models.Descriptors;
using Replay.Valorant.Flashes.Descriptors;

namespace Replay.Valorant.Descriptors.Agents.Guide;

public static class GuideDescriptors
{
    public static List<ExportGroupDescriptor> CreateDescriptors()
    {
        return
        [
            new GuideAgentDescriptor(),
            new SkyeFlashProjectileDescriptor(),
            new SkyeFlashSourceDescriptor(),
        ];
    }

    public static IReadOnlyList<ClassNetCacheDescriptor> CreateClassNetCacheDescriptors() =>
    [
        new SkyeFlashSourceClassNetCacheDescriptor(),
    ];
}
