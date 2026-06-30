using Replay.Models.Descriptors;

namespace Replay.Valorant.Descriptors.Agents.Rift;

public static class RiftDescriptors
{
    public static List<ExportGroupDescriptor> CreateDescriptors()
    {
        return
        [
            new RiftAgentDescriptor(),
        ];
    }
}