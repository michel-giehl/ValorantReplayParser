using Replay.Models.Descriptors;

namespace Replay.Valorant.Descriptors;

internal sealed class BaseReplayControllerClassNetCacheDescriptor : ClassNetCacheDescriptor<BaseReplayControllerClassNetCacheDescriptor>
{
    public override string Path => "/Game/Characters/BaseReplayController.BaseReplayController_C_ClassNetCache";

    protected override void Configure()
    {
        AddFunction(
            "ReplaysClientReceiveRemoteCharacterUpdatesSingleArrayNoAutonomous",
            "/Script/ShooterGame.ReplayPlayerController:ReplaysClientReceiveRemoteCharacterUpdatesSingleArrayNoAutonomous",
            ExportCategory.Movement);
    }
}

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
