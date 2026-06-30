using Replay.Models.Descriptors;

namespace Replay.Valorant.Descriptors.Agents.Vampire;

public static class VampireDescriptors
{
    public static List<ExportGroupDescriptor> CreateDescriptors()
    {
        return
        [
            new VampireAgentDescriptor(),
        ];
    }
}