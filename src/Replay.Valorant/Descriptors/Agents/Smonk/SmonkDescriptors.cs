using Replay.Models.Descriptors;
using Replay.Valorant.Smokes.Descriptors;
using static Replay.Valorant.Descriptors.Agents.AbilityDescriptorRegistration;

namespace Replay.Valorant.Descriptors.Agents.Smonk;

public static class SmonkDescriptors
{
    public static List<ExportGroupDescriptor> CreateDescriptors()
    {
        return
        [
            new SmonkAgentDescriptor(),
            new SmonkSmokeAbilityDescriptor(),
            new SmonkPostDeathSmokeAbilityDescriptor(),
            new SmonkSmokeDescriptor(),
            new SmonkPersistentSmokeDescriptor(),
            Actor("/Game/Characters/Smonk/S0/Ability_Q/DebuffKnife/DecayLauncher/GameObject_Smonk_Q_DecayExplosion.GameObject_Smonk_Q_DecayExplosion_C",
                ["Owner", "Instigator"]),
        ];
    }

    public static IReadOnlyList<ClassNetCacheDescriptor> CreateClassNetCacheDescriptors() =>
    [
        SmokeClassNetCacheDescriptors.CreateMovedToPersistentData(SmonkSmokePaths.Ability, 0),
    ];
}
