using Replay.Models.Descriptors;
using Replay.Models.Unreal;
using Replay.Unreal.Parsing;

namespace Replay.Valorant.Descriptors;

public sealed class BaseReplayControllerDescriptor : ExportGroupDescriptor<BaseReplayControllerDescriptor>
{
    public override string Path => "/Game/Characters/_Core/BaseReplayController.BaseReplayController_C";
    public override ExportCategory Categories => ExportCategory.Movement | ExportCategory.Economy;
    public override ExportGroupKind Kind => ExportGroupKind.PlayerController;

    public uint PlayerState { get; set; }
    public uint RemoteCharacterUpdatesArray { get; set; }
    public FVector SpawnLocation { get; set; }

    protected override void Configure()
    {
        AddProperty(x => x.PlayerState, ExportCategory.Movement | ExportCategory.Economy).ObjectNetGuid();
        AddProperty(x => x.RemoteCharacterUpdatesArray, ExportCategory.Movement);
        AddProperty(x => x.SpawnLocation, ExportCategory.Movement).FVector();
    }
}
