using Replay.Valorant.Smokes.Descriptors;

namespace Replay.Valorant.Descriptors.Agents.Iris;

internal static class IrisSmokePaths
{
    public const string Ability =
        "/Game/Characters/Iris/S0/Ability_E/Ability_Iris_E_MT_Smoke_Production.Ability_Iris_E_MT_Smoke_Production_C";
    public const string Smoke =
        "/Game/Characters/Iris/S0/Ability_E/GameObject_Iris_E_Smoke.GameObject_Iris_E_Smoke_C";
}

public sealed class IrisSmokeAbilityDescriptor : SmokeAbilityDescriptor<IrisSmokeAbilityDescriptor>
{
    public override string Path => IrisSmokePaths.Ability;
}

public sealed class IrisSmokeDescriptor : MovingSmokeActorDescriptor<IrisSmokeDescriptor>
{
    public override string Path => IrisSmokePaths.Smoke;
}
