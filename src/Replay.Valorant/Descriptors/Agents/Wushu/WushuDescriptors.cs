using Replay.Models.Descriptors;

namespace Replay.Valorant.Descriptors.Agents.Wushu;

public static class WushuDescriptors
{
    public static List<ExportGroupDescriptor> CreateDescriptors()
    {
        return
        [
            new WushuAgentDescriptor(),
        ];
    }
}