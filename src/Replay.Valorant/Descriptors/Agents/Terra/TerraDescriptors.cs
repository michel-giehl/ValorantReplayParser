using Replay.Models.Descriptors;

namespace Replay.Valorant.Descriptors.Agents.Terra;

public static class TerraDescriptors
{
    public static List<ExportGroupDescriptor> CreateDescriptors()
    {
        return
        [
            new TerraAgentDescriptor(),
        ];
    }
}