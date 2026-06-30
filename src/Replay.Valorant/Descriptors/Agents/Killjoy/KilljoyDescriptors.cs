using Replay.Models.Descriptors;

namespace Replay.Valorant.Descriptors.Agents.Killjoy;

public static class KilljoyDescriptors
{
    public static List<ExportGroupDescriptor> CreateDescriptors()
    {
        return
        [
            new KilljoyAgentDescriptor(),
        ];
    }
}