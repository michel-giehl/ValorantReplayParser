using Replay.Models.Descriptors;

namespace Replay.Valorant.Descriptors.Agents.Pandemic;

public static class PandemicDescriptors
{
    public static List<ExportGroupDescriptor> CreateDescriptors()
    {
        return
        [
            new PandemicAgentDescriptor(),
        ];
    }
}