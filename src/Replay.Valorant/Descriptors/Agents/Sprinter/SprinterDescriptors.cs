using Replay.Models.Descriptors;

namespace Replay.Valorant.Descriptors.Agents.Sprinter;

public static class SprinterDescriptors
{
    public static List<ExportGroupDescriptor> CreateDescriptors()
    {
        return
        [
            new SprinterAgentDescriptor(),
        ];
    }
}