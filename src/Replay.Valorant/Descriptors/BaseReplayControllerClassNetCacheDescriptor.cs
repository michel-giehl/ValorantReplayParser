using Replay.Models.Descriptors;
using Replay.Valorant.Movement;

namespace Replay.Valorant.Descriptors;

internal sealed class BaseReplayControllerClassNetCacheDescriptor : ClassNetCacheDescriptor<BaseReplayControllerClassNetCacheDescriptor>
{
    public override string Path => "/Game/Characters/BaseReplayController.BaseReplayController_C_ClassNetCache";

    protected override void Configure()
    {
        AddFunction(
                "ReplaysClientReceiveRemoteCharacterUpdatesSingleArrayNoAutonomous",
                "/Script/ShooterGame.ReplayPlayerController:ReplaysClientReceiveRemoteCharacterUpdatesSingleArrayNoAutonomous",
                ExportCategory.Movement)
            .Decode(RemoteCharacterUpdatesRpcDecoder.Instance);
    }
}