using Replay.Models.Descriptors;
using Replay.Models.Unreal;
using Replay.Unreal.Parsing;

namespace Replay.Valorant.Descriptors.Agents.Gumshoe;

internal sealed class CageTrapAbilityDescriptor : ExportGroupDescriptor<CageTrapAbilityDescriptor>
{
    public override string Path => "/Game/Characters/Gumshoe/S0/Ability_4/Ability_Gumshoe_4_CageTrap.Ability_Gumshoe_4_CageTrap_C";
    public override ExportCategory Categories => ExportCategory.Ability;
    public override ExportGroupKind Kind => ExportGroupKind.Actor;

    public uint RemoteRole { get; set; }
    public uint Role { get; set; }
    public uint CosmeticRandomSeed { get; set; }
    public uint Owner { get; set; }
    public uint Instigator { get; set; }
    public FVector RelativeScale3D { get; set; }
    public uint AttachComponent { get; set; }
    public uint CreatedByCharacter { get; set; }

    protected override void Configure()
    {
        AddProperty(x => x.RemoteRole).Ignore();
        AddProperty(x => x.Role).Ignore();
        AddProperty(x => x.CosmeticRandomSeed).Ignore();
        AddProperty(x => x.RelativeScale3D).FVector();
        AddProperty(x => x.Owner).ObjectNetGuid();
        AddProperty(x => x.Instigator).ObjectNetGuid();
        AddProperty(x => x.AttachComponent).Bool();
        AddProperty(x => x.CreatedByCharacter).ObjectNetGuid();
    }
}