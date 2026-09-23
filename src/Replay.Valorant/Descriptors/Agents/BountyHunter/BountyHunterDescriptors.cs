using Replay.Models.Descriptors;
using Replay.Valorant.Reveals.Descriptors;

namespace Replay.Valorant.Descriptors.Agents.BountyHunter;

public static class BountyHunterDescriptors
{
    public static List<ExportGroupDescriptor> CreateDescriptors()
    {
        return
        [
            new BountyHunterAgentDescriptor(),
            new FadeRevealProjectileDescriptor(),
            new FadeRevealDeviceDescriptor(),
        ];
    }
}
