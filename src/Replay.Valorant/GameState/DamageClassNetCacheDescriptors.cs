using Replay.Models.Descriptors;
using Replay.Unreal.Parsing;

namespace Replay.Valorant.GameState;

internal sealed class ChildDamageSectionClassNetCacheDescriptor
    : ClassNetCacheDescriptor<ChildDamageSectionClassNetCacheDescriptor>
{
    public override string Path => "/Script/ShooterGame.ChildDamageSectionComponent_ClassNetCache";

    protected override void Configure()
    {
        AddFunction<MulticastNotifySetLifeParameters>(
            "MulticastNotifySetLife",
            "/Script/ShooterGame.DamageSectionComponent:MulticastNotifySetLife",
            ExportCategory.Gunplay);
    }
}

internal sealed class AttachedDamageSectionClassNetCacheDescriptor
    : ClassNetCacheDescriptor<AttachedDamageSectionClassNetCacheDescriptor>
{
    public override string Path => "/Script/ShooterGame.AttachedDamageSectionComponent_ClassNetCache";

    protected override void Configure()
    {
        AddFunction<MulticastNotifySetLifeParameters>(
            "MulticastNotifySetLife",
            "/Script/ShooterGame.DamageSectionComponent:MulticastNotifySetLife",
            ExportCategory.Gunplay);
    }
}

internal sealed class ArmorDamageSectionClassNetCacheDescriptor
    : ClassNetCacheDescriptor<ArmorDamageSectionClassNetCacheDescriptor>
{
    public override string Path =>
        "/Game/Gear/BasicArmorAttachedDamageSection.BasicArmorAttachedDamageSection_C_ClassNetCache";

    protected override void Configure()
    {
        AddFunction<MulticastNotifySetLifeParameters>(
            "MulticastNotifySetLife",
            "/Script/ShooterGame.DamageSectionComponent:MulticastNotifySetLife",
            ExportCategory.Gunplay);
    }
}

public sealed class MulticastNotifySetLifeParameters
    : ExportGroupDescriptor<MulticastNotifySetLifeParameters>
{
    public override string Path =>
        "/Script/ShooterGame.DamageSectionComponent:MulticastNotifySetLife";
    public override ExportCategory Categories => ExportCategory.Gunplay;
    public override ExportGroupKind Kind => ExportGroupKind.ClassNetCache;
    public override FieldStreamGrammar Grammar => FieldStreamGrammar.FunctionParameters;

    public float NewLife { get; set; }
    public bool NewAlive { get; set; }

    protected override void Configure()
    {
        AddProperty(x => x.NewLife).Float();
        AddProperty("bNewAlive", x => x.NewAlive).Bool();
    }
}
