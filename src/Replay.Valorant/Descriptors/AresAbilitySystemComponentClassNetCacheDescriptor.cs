using Replay.Models.Descriptors;

namespace Replay.Valorant.Descriptors;

internal sealed class AresAbilitySystemComponentClassNetCacheDescriptor : ClassNetCacheDescriptor<AresAbilitySystemComponentClassNetCacheDescriptor>
{
    public override string Path => "/Script/ShooterGame.AresAbilitySystemComponent_ClassNetCache";

    protected override void Configure()
    {
        AddFunction(
            "ClientActivateAbility",
            "/Script/ShooterGame.AresAbilitySystemComponent:ClientActivateAbility",
            ExportCategory.Ability);
    }
}