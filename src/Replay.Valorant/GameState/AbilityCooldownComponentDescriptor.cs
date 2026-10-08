using Replay.Models.Descriptors;
using Replay.Unreal.Parsing;

namespace Replay.Valorant.GameState;

public sealed class AbilityCooldownComponentDescriptor
    : ExportGroupDescriptor<AbilityCooldownComponentDescriptor>
{
    public override string Path =>
        "/Game/Characters/Components/Comp_Ability_CooldownComponent.Comp_Ability_CooldownComponent_C";
    public override ExportCategory Categories => ExportCategory.Ability;
    public override ExportGroupKind Kind => ExportGroupKind.Component;

    public double CooldownSeconds { get; set; }
    public double StartTimeStamp { get; set; }
    public bool CooldownActive { get; set; }

    protected override void Configure()
    {
        AddProperty(x => x.CooldownSeconds).Double();
        AddProperty(x => x.StartTimeStamp).Double();
        AddProperty(x => x.CooldownActive).Bool();
    }
}
