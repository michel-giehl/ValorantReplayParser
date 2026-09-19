using Replay.Valorant.Smokes.Descriptors;

namespace Replay.Valorant.Descriptors.Agents.Wushu;

internal static class WushuSmokePaths
{
    public const string Ability =
        "/Game/Characters/Wushu/S0/Ability_4/Ability_Wushu_4_Smoke.Ability_Wushu_4_Smoke_C";
    public const string SmokeZone =
        "/Game/Characters/Wushu/S0/Ability_4/GameObject_Wushu_4_SmokeZone.GameObject_Wushu_4_SmokeZone_C";
    public const string Projectile =
        "/Game/Characters/Wushu/S0/Ability_4/Projectile_Wushu_4_Smoke.Projectile_Wushu_4_Smoke_C";
}

public sealed class WushuSmokeAbilityDescriptor : SmokeAbilityDescriptor<WushuSmokeAbilityDescriptor>
{
    public override string Path => WushuSmokePaths.Ability;
}

public sealed class WushuSmokeZoneDescriptor : SmokeOwnedActorDescriptor<WushuSmokeZoneDescriptor>
{
    public override string Path => WushuSmokePaths.SmokeZone;
}

public sealed class WushuSmokeProjectileDescriptor : MovingSmokeActorDescriptor<WushuSmokeProjectileDescriptor>
{
    public override string Path => WushuSmokePaths.Projectile;
}
