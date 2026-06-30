using Replay.Models.Descriptors;

namespace Replay.Valorant.Descriptors.Agents.Wraith;

public static class WraithDescriptors
{
    public static List<ExportGroupDescriptor> CreateDescriptors()
    {
        return
        [
            new WraithAgentDescriptor(),
        ];
    }
}