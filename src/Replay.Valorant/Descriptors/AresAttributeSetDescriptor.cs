using Replay.Models.Descriptors;
using Replay.Unreal.Parsing;

namespace Replay.Valorant.Descriptors;

internal sealed class AresAttributeSetDescriptor : ExportGroupDescriptor<AresAttributeSetDescriptor>
{
    public override string Path => "/Script/ShooterGame.AresAttributeSet";
    public override ExportCategory Categories => ExportCategory.Ability;
    public override ExportGroupKind Kind => ExportGroupKind.AttributeSet;

    public float Health { get; set; }
    public float MaxHealth { get; set; }
    public float Shield { get; set; }
    public float MaxShield { get; set; }
    public float Damage { get; set; }

    protected override void Configure()
    {
        AddProperty(x => x.Health, ExportCategory.Ability).Float();
        AddProperty(x => x.MaxHealth, ExportCategory.Ability).Float();
        AddProperty(x => x.Shield, ExportCategory.Ability).Float();
        AddProperty(x => x.MaxShield, ExportCategory.Ability).Float();
        AddProperty(x => x.Damage, ExportCategory.Ability).Float();
    }
}