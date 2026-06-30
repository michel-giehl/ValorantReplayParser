using Replay.Models.Descriptors;

namespace Replay.Valorant.Descriptors.Agents.Clay;

public static class ClayDescriptors
{
    public static List<ExportGroupDescriptor> CreateDescriptors()
    {
        return
        [
            new ClayAgentDescriptor(),
        ];
    }
}