using Replay.Models.Descriptors;

namespace Replay.Valorant.Descriptors.Agents.Sarge;

public static class SargeDescriptors
{
    public static List<ExportGroupDescriptor> CreateDescriptors()
    {
        return
        [
            new SargeAgentDescriptor(),
        ];
    }
}