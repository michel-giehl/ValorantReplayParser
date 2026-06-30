using Replay.Models.Descriptors;

namespace Replay.Valorant.Descriptors.Agents.Sequoia;

public static class SequoiaDescriptors
{
    public static List<ExportGroupDescriptor> CreateDescriptors()
    {
        return
        [
            new SequoiaAgentDescriptor(),
        ];
    }
}