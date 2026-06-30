using Replay.Models.Descriptors;

namespace Replay.Valorant.Descriptors.Agents.Mage;

public static class MageDescriptors
{
    public static List<ExportGroupDescriptor> CreateDescriptors()
    {
        return
        [
            new MageAgentDescriptor(),
        ];
    }
}