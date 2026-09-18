using Replay.Models.Descriptors;
using Replay.Models.Unreal;
using Replay.Unreal.Parsing;
using Replay.Valorant.Combat;

namespace Replay.Valorant.Flashes.Descriptors;

public sealed class EffectManagerPlayContinuousParameters
    : ExportGroupDescriptor<EffectManagerPlayContinuousParameters>, IEffectDataPayload
{
    public override string Path => "/Script/ShooterGame.EffectManagerComponent:MulticastPlayContinuousEffect";
    public override ExportCategory Categories => ExportCategory.Effects;
    public override ExportGroupKind Kind => ExportGroupKind.ClassNetCache;
    public override FieldStreamGrammar Grammar => FieldStreamGrammar.FunctionParameters;

    public uint? EffectContainer { get; set; }
    public uint? WaitOnReplicationActor { get; set; }
    public EffectManagerFunctionFloatValue[]? FunctionFloatValues { get; set; }
    public EffectManagerFunctionVectorValue[]? FunctionVectorValues { get; set; }
    public EffectManagerFunctionObjectValue[]? FunctionObjectValues { get; set; }
    public FVector? Translation { get; set; }
    public FVector? Scale3D { get; set; }
    public string? AttachSocket { get; set; }
    public ulong? EffectId { get; set; }
    public string? SourceId { get; set; }
    public bool? IsLocalEffect { get; set; }
    public bool? IsTransient { get; set; }
    public uint? ClientControllerThatTriggered { get; set; }
    public float? StartMovementTime { get; set; }
    public EAresAlliance? AllianceFilter { get; set; }

    public IReadOnlyList<EffectManagerFloatValue> FloatValues =>
        EffectManagerValueMapper.Map(FunctionFloatValues);

    public IReadOnlyList<EffectManagerObjectValue> ObjectValues =>
        EffectManagerValueMapper.Map(FunctionObjectValues);

    public IReadOnlyList<EffectManagerVectorValue> VectorValues =>
        EffectManagerValueMapper.Map(FunctionVectorValues);

    protected override void Configure()
    {
        AddPropertyHandle(0, x => x.EffectContainer).ObjectNetGuid();
        AddPropertyHandle(1, x => x.WaitOnReplicationActor).ObjectNetGuid();
        AddPropertyHandle(2, "FloatValues", x => x.FunctionFloatValues)
            .RepLayoutDynamicArray<EffectManagerFunctionFloatValue>();
        AddPropertyHandle(6, "VectorValues", x => x.FunctionVectorValues)
            .RepLayoutDynamicArray<EffectManagerFunctionVectorValue>();
        AddPropertyHandle(10, "ObjectValues", x => x.FunctionObjectValues)
            .RepLayoutDynamicArray<EffectManagerFunctionObjectValue>();
        AddPropertyHandle(23, x => x.Translation).FVector();
        AddPropertyHandle(24, x => x.Scale3D).FVector();
        AddPropertyHandle(25, x => x.AttachSocket).FName();
        AddPropertyHandle(26, "EffectID", x => x.EffectId).UInt64();
        AddPropertyHandle(27, "SourceID", x => x.SourceId).FName();
        AddPropertyHandle(28, "bLocalEffect", x => x.IsLocalEffect).Bool();
        AddPropertyHandle(29, "bTransient", x => x.IsTransient).Bool();
        AddPropertyHandle(30, x => x.ClientControllerThatTriggered).ObjectNetGuid();
        AddPropertyHandle(31, x => x.StartMovementTime).Float();
        AddPropertyHandle(32, x => x.AllianceFilter).EnumRemainingBits();
    }
}

public sealed class EffectManagerPlayOneShotParameters
    : ExportGroupDescriptor<EffectManagerPlayOneShotParameters>, IEffectDataPayload
{
    public override string Path => "/Script/ShooterGame.EffectManagerComponent:MulticastPlayOneShotEffect";
    public override ExportCategory Categories => ExportCategory.Effects;
    public override ExportGroupKind Kind => ExportGroupKind.ClassNetCache;
    public override FieldStreamGrammar Grammar => FieldStreamGrammar.FunctionParameters;

    public uint? EffectContainer { get; set; }
    public uint? WaitOnReplicationActor { get; set; }
    public EffectManagerFunctionFloatValue[]? FunctionFloatValues { get; set; }
    public EffectManagerFunctionObjectValue[]? FunctionObjectValues { get; set; }
    public FVector? Translation { get; set; }
    public FVector? Scale3D { get; set; }
    public uint? ClientControllerThatTriggered { get; set; }
    public float? StartMovementTime { get; set; }
    public EAresAlliance? AllianceFilter { get; set; }

    public IReadOnlyList<EffectManagerFloatValue> FloatValues =>
        EffectManagerValueMapper.Map(FunctionFloatValues);

    public IReadOnlyList<EffectManagerObjectValue> ObjectValues =>
        EffectManagerValueMapper.Map(FunctionObjectValues);

    protected override void Configure()
    {
        AddPropertyHandle(0, x => x.EffectContainer).ObjectNetGuid();
        AddPropertyHandle(1, x => x.WaitOnReplicationActor).ObjectNetGuid();
        AddPropertyHandle(2, "FloatValues", x => x.FunctionFloatValues)
            .RepLayoutDynamicArray<EffectManagerFunctionFloatValue>();
        AddPropertyHandle(10, "ObjectValues", x => x.FunctionObjectValues)
            .RepLayoutDynamicArray<EffectManagerFunctionObjectValue>();
        AddPropertyHandle(23, x => x.Translation).FVector();
        AddPropertyHandle(24, x => x.Scale3D).FVector();
        AddPropertyHandle(26, x => x.ClientControllerThatTriggered).ObjectNetGuid();
        AddPropertyHandle(27, x => x.StartMovementTime).Float();
        AddPropertyHandle(28, x => x.AllianceFilter).EnumRemainingBits();
    }
}

public sealed class EffectManagerStopContinuousParameters
    : ExportGroupDescriptor<EffectManagerStopContinuousParameters>
{
    public override string Path => "/Script/ShooterGame.EffectManagerComponent:MulticastStopContinuousEffect";
    public override ExportCategory Categories => ExportCategory.Effects;
    public override ExportGroupKind Kind => ExportGroupKind.ClassNetCache;
    public override FieldStreamGrammar Grammar => FieldStreamGrammar.FunctionParameters;

    public ulong? EffectId { get; set; }
    public string? SourceId { get; set; }
    public bool? IsLocalEffect { get; set; }
    public bool? IsTransient { get; set; }
    public uint? StopEffectType { get; set; }
    public float? StopMovementTime { get; set; }

    protected override void Configure()
    {
        AddPropertyHandle(0, "EffectID", x => x.EffectId).UInt64();
        AddPropertyHandle(1, "SourceID", x => x.SourceId).FName();
        AddPropertyHandle(2, "bLocalEffect", x => x.IsLocalEffect).Bool();
        AddPropertyHandle(3, "bTransient", x => x.IsTransient).Bool();
        AddPropertyHandle(5, x => x.StopEffectType).EnumRemainingBits();
        AddPropertyHandle(6, x => x.StopMovementTime).Float();
    }
}

public sealed class EffectManagerUpdateContinuousParameters
    : ExportGroupDescriptor<EffectManagerUpdateContinuousParameters>, IEffectDataPayload
{
    public override string Path => "/Script/ShooterGame.EffectManagerComponent:MulticastUpdateContinuousEffect";
    public override ExportCategory Categories => ExportCategory.Effects;
    public override ExportGroupKind Kind => ExportGroupKind.ClassNetCache;
    public override FieldStreamGrammar Grammar => FieldStreamGrammar.FunctionParameters;

    public ulong? EffectId { get; set; }
    public string? SourceId { get; set; }
    public bool? IsLocalEffect { get; set; }
    public bool? IsTransient { get; set; }
    public uint? WaitOnReplicationActor { get; set; }
    public EffectManagerUpdateFloatValue[]? UpdateFloatValues { get; set; }

    public IReadOnlyList<EffectManagerFloatValue> FloatValues =>
        EffectManagerValueMapper.Map(UpdateFloatValues);

    public IReadOnlyList<EffectManagerObjectValue> ObjectValues => [];

    protected override void Configure()
    {
        AddPropertyHandle(0, "EffectID", x => x.EffectId).UInt64();
        AddPropertyHandle(1, "SourceID", x => x.SourceId).FName();
        AddPropertyHandle(2, "bLocalEffect", x => x.IsLocalEffect).Bool();
        AddPropertyHandle(3, "bTransient", x => x.IsTransient).Bool();
        AddPropertyHandle(4, x => x.WaitOnReplicationActor).ObjectNetGuid();
        AddPropertyHandle(5, "FloatValues", x => x.UpdateFloatValues)
            .RepLayoutDynamicArray<EffectManagerUpdateFloatValue>();
    }
}

public sealed class EffectManagerComponentClassNetCacheDescriptor
    : ClassNetCacheDescriptor<EffectManagerComponentClassNetCacheDescriptor>
{
    public override string Path => "/Script/ShooterGame.EffectManagerComponent_ClassNetCache";

    protected override void Configure()
    {
        AddFunctionHandle<EffectManagerPlayContinuousParameters>(
            0,
            "MulticastPlayContinuousEffect",
            "/Script/ShooterGame.EffectManagerComponent:MulticastPlayContinuousEffect",
            ExportCategory.Effects);
        AddFunctionHandle<EffectManagerPlayOneShotParameters>(
            1,
            "MulticastPlayOneShotEffect",
            "/Script/ShooterGame.EffectManagerComponent:MulticastPlayOneShotEffect",
            ExportCategory.Effects);
        AddFunctionHandle<EffectManagerStopContinuousParameters>(
            2,
            "MulticastStopContinuousEffect",
            "/Script/ShooterGame.EffectManagerComponent:MulticastStopContinuousEffect",
            ExportCategory.Effects);
        AddFunctionHandle<EffectManagerUpdateContinuousParameters>(
            3,
            "MulticastUpdateContinuousEffect",
            "/Script/ShooterGame.EffectManagerComponent:MulticastUpdateContinuousEffect",
            ExportCategory.Effects);
    }
}
