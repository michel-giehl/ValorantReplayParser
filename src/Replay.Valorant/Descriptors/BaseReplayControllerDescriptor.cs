using Replay.Models.Descriptors;
using Replay.Models.Unreal;
using Replay.Unreal.Parsing;

namespace Replay.Valorant.Descriptors;

internal sealed class BaseReplayControllerDescriptor : ExportGroupDescriptor<BaseReplayControllerDescriptor>
{
    public override string Path => "/Game/Characters/_Core/BaseReplayController.BaseReplayController_C";
    public override ExportCategory Categories => ExportCategory.Movement;
    public override ExportGroupKind Kind => ExportGroupKind.PlayerController;

    public uint RemoteCharacterUpdatesArray { get; set; }
    public FVector SpawnLocation { get; set; }

    protected override void Configure()
    {
        AddProperty(x => x.RemoteCharacterUpdatesArray);
        AddProperty(x => x.SpawnLocation).FVector();
    }
}