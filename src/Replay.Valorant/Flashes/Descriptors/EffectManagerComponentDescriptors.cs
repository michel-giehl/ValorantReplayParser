using Replay.Models.Descriptors;
using Replay.Models.Unreal;
using Replay.Unreal.Parsing;
using Replay.Valorant.Combat;

namespace Replay.Valorant.Flashes.Descriptors;

public sealed class ActiveEffectInfoDescriptor : ExportGroupDescriptor<ActiveEffectInfoDescriptor>,
    IEffectDataPayload
{
    public ulong? EffectId { get; set; }
    public string? SourceId { get; set; }
    public bool? IsLocalEffect { get; set; }
    public bool? IsTransient { get; set; }
    public uint? EffectType { get; set; }
    public uint? WaitOnReplicationActor { get; set; }
    public ActiveEffectFloatValue[]? ActiveFloatValues { get; set; }
    public ActiveEffectObjectValue[]? ActiveObjectValues { get; set; }
    public FVector? Translation { get; set; }
    public FVector? Scale3D { get; set; }
    public string? Socket { get; set; }
    public float? StartTimeStamp { get; set; }
    public EAresAlliance? AllianceFilter { get; set; }

    public IReadOnlyList<EffectManagerFloatValue> FloatValues =>
        EffectManagerValueMapper.Map(ActiveFloatValues);

    public IReadOnlyList<EffectManagerObjectValue> ObjectValues =>
        EffectManagerValueMapper.Map(ActiveObjectValues);

    protected override void Configure()
    {
        AddPropertyHandle(3, "EffectID", x => x.EffectId).UInt64();
        AddPropertyHandle(4, "SourceID", x => x.SourceId).FName();
        AddPropertyHandle(5, "bLocalEffect", x => x.IsLocalEffect).Bool();
        AddPropertyHandle(6, "bTransient", x => x.IsTransient).Bool();
        AddPropertyHandle(7, x => x.EffectType).ObjectNetGuid();
        AddPropertyHandle(8, x => x.WaitOnReplicationActor).ObjectNetGuid();
        AddPropertyHandle(9, "FloatValues", x => x.ActiveFloatValues)
            .RepLayoutDynamicArray<ActiveEffectFloatValue>();
        AddPropertyHandle(17, "ObjectValues", x => x.ActiveObjectValues)
            .RepLayoutDynamicArray<ActiveEffectObjectValue>();
        AddPropertyHandle(30, x => x.Translation).FVector();
        AddPropertyHandle(31, x => x.Scale3D).FVector();
        AddPropertyHandle(32, x => x.Socket).FName();
        AddPropertyHandle(33, x => x.StartTimeStamp).Float();
        AddPropertyHandle(34, x => x.AllianceFilter).EnumRemainingBits();
    }
}

public sealed class EffectManagerComponentDescriptor
    : ExportGroupDescriptor<EffectManagerComponentDescriptor>
{
    public override string Path => "/Script/ShooterGame.EffectManagerComponent";
    public override ExportCategory Categories => ExportCategory.Effects;
    public override ExportGroupKind Kind => ExportGroupKind.Component;

    public ActiveEffectInfoDescriptor[]? ServerActiveEffects { get; set; }

    protected override void Configure()
    {
        AddPropertyHandle(2, x => x.ServerActiveEffects)
            .RepLayoutDynamicArray<ActiveEffectInfoDescriptor>();
    }
}
