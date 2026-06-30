using Replay.Models.Descriptors;

namespace Replay.Valorant.Descriptors.Agents.Guide;

public static class GuideDescriptors
{
    public static List<ExportGroupDescriptor> CreateDescriptors()
    {
        return
        [
            new GuideAgentDescriptor(),
        ];
    }
}