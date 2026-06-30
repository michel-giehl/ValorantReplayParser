using Replay.Models.Descriptors;

namespace Replay.Valorant.Descriptors.Agents.Hunter;

/// <summary>
/// Sova
/// </summary>
public static class HunterDescriptors
{
    public static List<ExportGroupDescriptor> CreateDescriptors()
    {
        return
        [
            new HunterAgentDescriptor(),
        ];
    }
}