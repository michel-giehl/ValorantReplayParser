using Replay.Models.Descriptors;

namespace Replay.Valorant.Descriptors.Agents.Gumshoe;

public static class GumshoeDescriptors
{
    public static List<ExportGroupDescriptor> CreateDescriptors()
    {
        return
        [
            new GumshoeAgentDescriptor(),
            new TripWireAbilityDescriptor(),
            new TripWireGameObjectDescriptor(),
            new SecondTripWireGameObjectDescriptor(),
            new CageTrapProjectileDescriptor(),
            new CageTrapAbilityDescriptor(),
        ];
    }
}