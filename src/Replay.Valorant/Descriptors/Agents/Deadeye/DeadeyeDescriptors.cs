using Replay.Models.Descriptors;

namespace Replay.Valorant.Descriptors.Agents.Deadeye;

public static class DeadeyeDescriptors
{
    public static List<ExportGroupDescriptor> CreateDescriptors()
    {
        return
        [
            new DeadeyeAgentDescriptor(),
        ];
    }
}