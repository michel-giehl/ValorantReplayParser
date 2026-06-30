using Replay.Models.Descriptors;

namespace Replay.Valorant.Descriptors.Agents.BountyHunter;

public static class BountyHunterDescriptors
{
    public static List<ExportGroupDescriptor> CreateDescriptors()
    {
        return
        [
            new BountyHunterAgentDescriptor(),
        ];
    }
}