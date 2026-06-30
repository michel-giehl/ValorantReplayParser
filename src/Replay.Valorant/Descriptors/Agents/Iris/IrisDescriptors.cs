using Replay.Models.Descriptors;

namespace Replay.Valorant.Descriptors.Agents.Iris;

public static class IrisDescriptors
{
    public static List<ExportGroupDescriptor> CreateDescriptors()
    {
        return
        [
            new IrisAgentDescriptor(),
        ];
    }
}