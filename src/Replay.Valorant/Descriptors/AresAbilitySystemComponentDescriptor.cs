using Replay.Models.Descriptors;
using Replay.Unreal.Parsing;

namespace Replay.Valorant.Descriptors;

internal sealed class AresAbilitySystemComponentDescriptor : ExportGroupDescriptor<AresAbilitySystemComponentDescriptor>
{
    public override string Path => "/Script/ShooterGame.AresAbilitySystemComponent";
    public override ExportCategory Categories => ExportCategory.Ability;
    public override ExportGroupKind Kind => ExportGroupKind.Component;

    public uint Owner { get; set; }
    public uint Instigator { get; set; }
    public uint AresAttributeSet { get; set; }

    protected override void Configure()
    {
        AddProperty(x => x.Owner).ObjectNetGuid();
        AddProperty(x => x.Instigator).ObjectNetGuid();
        AddProperty(x => x.AresAttributeSet);
    }
}