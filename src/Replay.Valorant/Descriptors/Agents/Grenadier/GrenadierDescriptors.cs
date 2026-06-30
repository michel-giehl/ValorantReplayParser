using Replay.Models.Descriptors;

namespace Replay.Valorant.Descriptors.Agents.Grenadier;

public static class GrenadierDescriptors
{
    public static List<ExportGroupDescriptor> CreateDescriptors()
    {
        return
        [
            new GrenadierAgentDescriptor(),
        ];
    }
}