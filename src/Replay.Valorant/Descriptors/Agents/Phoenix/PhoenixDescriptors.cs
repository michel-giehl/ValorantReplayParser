using Replay.Models.Descriptors;

namespace Replay.Valorant.Descriptors.Agents.Phoenix;

public static class PhoenixDescriptors
{
    public static List<ExportGroupDescriptor> CreateDescriptors()
    {
        return
        [
            new PhoenixAgentDescriptor(),
        ];
    }
}