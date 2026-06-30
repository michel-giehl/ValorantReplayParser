using Replay.Models.Descriptors;

namespace Replay.Valorant.Descriptors.Agents.Breach;

public static class BreachDescriptors
{
    public static List<ExportGroupDescriptor> CreateDescriptors()
    {
        return
        [
            new BreachAgentDescriptor(),
        ];
    }
}