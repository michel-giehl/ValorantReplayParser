using Replay.Models.Descriptors;
using Replay.Unreal.Parsing;

namespace Replay.Valorant.Descriptors;

internal sealed class BaseReplayPlayerState : ExportGroupDescriptor<BaseReplayPlayerState>
{
    public override string Path => "/Game/GameModes/Common/BaseReplayPlayerState.BaseReplayPlayerState_C";
    public override ExportCategory Categories => ExportCategory.Movement;
    public override ExportGroupKind Kind => ExportGroupKind.Unknown;

    public uint RemoteRole { get; set; }
    public uint Owner { get; set; }
    public bool OnlySpectator { get; set; }

    protected override void Configure()
    {
        AddProperty(x => x.RemoteRole).ObjectNetGuid();
        AddProperty(x => x.Owner).ObjectNetGuid();
        AddProperty("bOnlySpectator", x => x.OnlySpectator).Bool();
    }
}