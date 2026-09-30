using Replay.Models.Descriptors;
using Replay.Unreal.Parsing;

namespace Replay.Valorant.GameState;

/// <summary>Wingman ownership for attribution of plants and defuses.</summary>
public sealed class WingmanDescriptor : ExportGroupDescriptor<WingmanDescriptor>
{
    public const string ExportPath = "/Game/Characters/AggroBot/S0/Ability_Q/Pawn_Aggrobot_SeekerNade.Pawn_Aggrobot_SeekerNade_C";
    public override string Path => ExportPath;
    public override ExportCategory Categories => ExportCategory.GameState;
    public override ExportGroupKind Kind => ExportGroupKind.Actor;
    public uint Owner { get; set; }
    public uint Instigator { get; set; }
    protected override void Configure()
    {
        AddProperty(x => x.Owner).ObjectNetGuid();
        AddProperty(x => x.Instigator).ObjectNetGuid();
    }
}
