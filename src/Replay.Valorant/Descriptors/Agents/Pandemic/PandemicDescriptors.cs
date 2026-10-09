using Replay.Models.Descriptors;
using Replay.Valorant.Descriptors.Agents.Pandemic.SmokeScreen;

namespace Replay.Valorant.Descriptors.Agents.Pandemic;

public static class PandemicDescriptors
{
    public static List<ExportGroupDescriptor> CreateDescriptors()
    {
        return
        [
            new PandemicAgentDescriptor(),
            new ViperPitActorDescriptor(),
            new ProjectileSmokeScreenDescriptor(),
            new SmokeScreenManagerDescriptor(),
        ];
    }

    public static List<ClassNetCacheDescriptor> CreateClassNetCacheDescriptors()
    {
        return
        [
            new SmokeScreenManagerClassNetCacheDescriptor(),
        ];
    }
}