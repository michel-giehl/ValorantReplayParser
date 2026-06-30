using Replay.Models.Descriptors;

namespace Replay.Valorant.Descriptors.Agents.Nox;

public static class NoxDescriptors
{
    public static List<ExportGroupDescriptor> CreateDescriptors()
    {
        return
        [
            new NoxAgentDescriptor(),
        ];
    }
}