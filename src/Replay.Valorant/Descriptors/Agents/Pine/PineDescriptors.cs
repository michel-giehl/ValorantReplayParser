using Replay.Models.Descriptors;

namespace Replay.Valorant.Descriptors.Agents.Pine;

public static class PineDescriptors
{
    public static List<ExportGroupDescriptor> CreateDescriptors()
    {
        return
        [
            new PineAgentDescriptor(),
        ];
    }
}