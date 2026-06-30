using Replay.Models.Descriptors;

namespace Replay.Valorant.Descriptors.Agents.Cable;

public static class CableDescriptors
{
    public static List<ExportGroupDescriptor> CreateDescriptors()
    {
        return
        [
            new CableAgentDescriptor(),
        ];
    }
}