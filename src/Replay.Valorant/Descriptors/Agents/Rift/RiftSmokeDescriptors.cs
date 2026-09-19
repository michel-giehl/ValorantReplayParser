using Replay.Models.Descriptors;
using Replay.Unreal.Parsing;
using Replay.Valorant.Smokes.Descriptors;

namespace Replay.Valorant.Descriptors.Agents.Rift;

internal static class RiftSmokePaths
{
    public const string Ability =
        "/Game/Characters/Rift/S0/Ability_E/Ability_Rift_E_TransformRift_Smoke.Ability_Rift_E_TransformRift_Smoke_C";
    public const string WorldTargetingAbility =
        "/Game/Characters/Rift/S0/Ability_E/Ability_Rift_E_TransformRift_Smoke_WorldTargeting.Ability_Rift_E_TransformRift_Smoke_WorldTargeting_C";
    public const string SmokeZone =
        "/Game/Characters/Rift/S0/Ability_E/GameObject_Rift_E_SmokeZone.GameObject_Rift_E_SmokeZone_C";
    public const string FakeSmokeZone =
        "/Game/Characters/Rift/S0/Ability_E/GameObject_Rift_E_SmokeZone_Fake.GameObject_Rift_E_SmokeZone_Fake_C";
}

public sealed class RiftSmokeAbilityDescriptor : SmokeAbilityDescriptor<RiftSmokeAbilityDescriptor>
{
    public override string Path => RiftSmokePaths.Ability;
}

public sealed class RiftWorldTargetingSmokeAbilityDescriptor
    : SmokeAbilityDescriptor<RiftWorldTargetingSmokeAbilityDescriptor>
{
    public override string Path => RiftSmokePaths.WorldTargetingAbility;

    protected override void ConfigureAfterOwnership() =>
        AddPropertyHandle(58, x => x.CreatedByCharacter, ExportCategory.Ability).ObjectNetGuid();
}

public sealed class RiftSmokeZoneDescriptor : SmokeOwnedActorDescriptor<RiftSmokeZoneDescriptor>
{
    public override string Path => RiftSmokePaths.SmokeZone;
}

public sealed class RiftFakeSmokeZoneDescriptor : SmokeOwnedActorDescriptor<RiftFakeSmokeZoneDescriptor>
{
    public override string Path => RiftSmokePaths.FakeSmokeZone;
}
