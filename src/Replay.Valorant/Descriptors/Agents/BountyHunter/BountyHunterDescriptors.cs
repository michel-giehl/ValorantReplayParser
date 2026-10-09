using Replay.Models.Descriptors;
using Replay.Valorant.Reveals.Descriptors;
using static Replay.Valorant.Descriptors.Agents.AbilityDescriptorRegistration;

namespace Replay.Valorant.Descriptors.Agents.BountyHunter;

public static class BountyHunterDescriptors
{
    public static List<ExportGroupDescriptor> CreateDescriptors()
    {
        return
        [
            new BountyHunterAgentDescriptor(),
            new FadeRevealProjectileDescriptor(),
            new FadeRevealDeviceDescriptor(),
            Actor("/Game/Characters/BountyHunter/S0/Ability_Q/GameObject_Q_BountyHunter_Tether_SphereExpansion.GameObject_Q_BountyHunter_Tether_SphereExpansion_C",
                ["Owner", "Instigator"]),
        ];
    }
}
