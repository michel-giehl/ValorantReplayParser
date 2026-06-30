using Replay.Models.Descriptors;

namespace Replay.Valorant.Descriptors.Agents.Thorne;

public static class ThorneDescriptors
{
    public static List<ExportGroupDescriptor> CreateDescriptors()
    {
        return
        [
            new ThorneAgentDescriptor(),
        ];
    }
}