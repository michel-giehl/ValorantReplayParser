using Replay.Valorant.Smokes.Descriptors;

namespace Replay.Valorant.Descriptors.Agents.Sarge;

internal static class SargeSmokePaths
{
    public const string Ability =
        "/Game/Characters/Sarge/S0/Ability_MapTargetSmoke/Ability_Sarge_4_MapTargetSmoke_Production.Ability_Sarge_4_MapTargetSmoke_Production_C";
    public const string SmokeManager =
        "/Game/Characters/Sarge/S0/Ability_MapTargetSmoke/GameObject_Sarge_4_SmokeManager_Production.GameObject_Sarge_4_SmokeManager_Production_C";
    public const string Smoke =
        "/Game/Characters/Sarge/S0/Ability_MapTargetSmoke/GameObject_Sarge_4_Smoke_ProductionNEW.GameObject_Sarge_4_Smoke_ProductionNEW_C";
}

public sealed class SargeSmokeAbilityDescriptor : SmokeAbilityDescriptor<SargeSmokeAbilityDescriptor>
{
    public override string Path => SargeSmokePaths.Ability;
}

public sealed class SargeSmokeManagerDescriptor : SmokeOwnedActorDescriptor<SargeSmokeManagerDescriptor>
{
    public override string Path => SargeSmokePaths.SmokeManager;
}

public sealed class SargeSmokeDescriptor : SmokeOwnedActorDescriptor<SargeSmokeDescriptor>
{
    public override string Path => SargeSmokePaths.Smoke;
}
