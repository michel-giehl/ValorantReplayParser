using Replay.Models.Descriptors;

namespace Replay.Valorant.Descriptors.Agents.Cashew;

public static class CashewDescriptors
{
    public static List<ExportGroupDescriptor> CreateDescriptors()
    {
        return
        [
            new CashewAgentDescriptor(),
        ];
    }
}