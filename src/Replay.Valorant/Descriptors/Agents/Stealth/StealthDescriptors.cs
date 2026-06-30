using Replay.Models.Descriptors;

namespace Replay.Valorant.Descriptors.Agents.Stealth;

public static class StealthDescriptors
{
    public static List<ExportGroupDescriptor> CreateDescriptors()
    {
        return
        [
            new StealthAgentDescriptor(),
        ];
    }
}