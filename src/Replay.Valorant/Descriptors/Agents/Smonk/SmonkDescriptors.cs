using Replay.Models.Descriptors;

namespace Replay.Valorant.Descriptors.Agents.Smonk;

public static class SmonkDescriptors
{
    public static List<ExportGroupDescriptor> CreateDescriptors()
    {
        return
        [
            new SmonkAgentDescriptor(),
        ];
    }
}