using Replay.Models.Descriptors;
using Replay.Unreal.Parsing;

namespace Replay.Valorant.Descriptors;

internal sealed class TripWireAbilityDescriptor : ExportGroupDescriptor<TripWireAbilityDescriptor>
{
    public override string Path => "/Game/Characters/Gumshoe/S0/Ability_E/Ability_Gumshoe_E_TripWire.Ability_Gumshoe_E_TripWire_C";
    public override ExportCategory Categories => ExportCategory.Ability;
    public override ExportGroupKind Kind => ExportGroupKind.Actor;

    public uint Owner { get; set; }
    public uint Instigator { get; set; }
    public uint CreatedByCharacter { get; set; }

    protected override void Configure()
    {
        AddProperty(x => x.Owner).ObjectNetGuid();
        AddProperty(x => x.Instigator).ObjectNetGuid();
        AddProperty(x => x.CreatedByCharacter).ObjectNetGuid();
    }
}