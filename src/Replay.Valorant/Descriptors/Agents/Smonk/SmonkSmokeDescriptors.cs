using Replay.Models.Descriptors;
using Replay.Unreal.Parsing;
using Replay.Valorant.Smokes.Descriptors;

namespace Replay.Valorant.Descriptors.Agents.Smonk;

internal static class SmonkSmokePaths
{
    public const string Ability =
        "/Game/Characters/Smonk/S0/Ability_E/MapTargetSmoke/Ability_Smonk_E_MapTargetSmokeV2.Ability_Smonk_E_MapTargetSmokeV2_C";
    public const string PostDeathAbility =
        "/Game/Characters/Smonk/S0/Ability_E/MapTargetSmoke/Ability_Smonk_E_PostDeath.Ability_Smonk_E_PostDeath_C";
    public const string Smoke =
        "/Game/Characters/Smonk/S0/Ability_E/MapTargetSmoke/GameObject_Smonk_NewSmoke.GameObject_Smonk_NewSmoke_C";
    public const string PersistentSmoke =
        "/Game/Characters/Smonk/S0/Ability_E/MapTargetSmoke/GameObject_Smonk_NewSmoke_PDS.GameObject_Smonk_NewSmoke_PDS_C";
}

public sealed class SmonkSmokeAbilityDescriptor : SmokeAbilityDescriptor<SmonkSmokeAbilityDescriptor>
{
    public override string Path => SmonkSmokePaths.Ability;
}

public sealed class SmonkPostDeathSmokeAbilityDescriptor
    : SmokeAbilityDescriptor<SmonkPostDeathSmokeAbilityDescriptor>
{
    public override string Path => SmonkSmokePaths.PostDeathAbility;

    protected override void ConfigureAfterOwnership() =>
        AddPropertyHandle(58, x => x.CreatedByCharacter, ExportCategory.Ability).ObjectNetGuid();
}

public sealed class SmonkSmokeDescriptor : MovingSmokeActorDescriptor<SmonkSmokeDescriptor>
{
    public override string Path => SmonkSmokePaths.Smoke;
}

public sealed class SmonkPersistentSmokeDescriptor : MovingSmokeActorDescriptor<SmonkPersistentSmokeDescriptor>
{
    public override string Path => SmonkSmokePaths.PersistentSmoke;
}
