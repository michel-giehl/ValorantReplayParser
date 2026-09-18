using Replay.Models.Descriptors;
using Replay.Models.Unreal;
using Replay.Unreal.Parsing;

namespace Replay.Valorant.Flashes.Descriptors;

public interface IEffectDataPayload
{
    IReadOnlyList<EffectManagerFloatValue>? FloatValues { get; }
    IReadOnlyList<EffectManagerObjectValue>? ObjectValues { get; }
}

public sealed record EffectManagerFloatValue(FGameplayTag? Name, float? Value);

public sealed record EffectManagerVectorValue(FGameplayTag? Name, FVector? Value);

public sealed record EffectManagerObjectValue(FGameplayTag? Name, uint? Value);

public sealed class EffectManagerFunctionFloatValue
    : ExportGroupDescriptor<EffectManagerFunctionFloatValue>
{
    public FGameplayTag? Name { get; set; }
    public float? Value { get; set; }

    protected override void Configure()
    {
        AddPropertyHandle(3, "58", x => x.Name).FGameplayTag();
        AddPropertyHandle(4, "Float", x => x.Value).Float();
    }
}

public sealed class EffectManagerFunctionVectorValue
    : ExportGroupDescriptor<EffectManagerFunctionVectorValue>
{
    public FGameplayTag? Name { get; set; }
    public FVector? Value { get; set; }

    protected override void Configure()
    {
        AddPropertyHandle(7, "58", x => x.Name).FGameplayTag();
        AddPropertyHandle(8, "59", x => x.Value).FVector();
    }
}

public sealed class EffectManagerFunctionObjectValue
    : ExportGroupDescriptor<EffectManagerFunctionObjectValue>
{
    public FGameplayTag? Name { get; set; }
    public uint? Value { get; set; }

    protected override void Configure()
    {
        AddPropertyHandle(11, "58", x => x.Name).FGameplayTag();
        AddPropertyHandle(12, "100", x => x.Value).ObjectNetGuid();
    }
}

public sealed class EffectManagerUpdateFloatValue
    : ExportGroupDescriptor<EffectManagerUpdateFloatValue>
{
    public FGameplayTag? Name { get; set; }
    public float? Value { get; set; }

    protected override void Configure()
    {
        AddPropertyHandle(6, "58", x => x.Name).FGameplayTag();
        AddPropertyHandle(7, "Float", x => x.Value).Float();
    }
}

public sealed class ActiveEffectFloatValue : ExportGroupDescriptor<ActiveEffectFloatValue>
{
    public FGameplayTag? Name { get; set; }
    public float? Value { get; set; }

    protected override void Configure()
    {
        AddPropertyHandle(10, "58", x => x.Name).FGameplayTag();
        AddPropertyHandle(11, "Float", x => x.Value).Float();
    }
}

public sealed class ActiveEffectObjectValue : ExportGroupDescriptor<ActiveEffectObjectValue>
{
    public FGameplayTag? Name { get; set; }
    public uint? Value { get; set; }

    protected override void Configure()
    {
        AddPropertyHandle(18, "58", x => x.Name).FGameplayTag();
        AddPropertyHandle(19, "100", x => x.Value).ObjectNetGuid();
    }
}

internal static class EffectManagerValueMapper
{
    public static EffectManagerFloatValue[] Map(EffectManagerFunctionFloatValue[]? values) =>
        values?.OfType<EffectManagerFunctionFloatValue>()
            .Select(value => new EffectManagerFloatValue(value.Name, value.Value)).ToArray() ?? [];

    public static EffectManagerFloatValue[] Map(EffectManagerUpdateFloatValue[]? values) =>
        values?.OfType<EffectManagerUpdateFloatValue>()
            .Select(value => new EffectManagerFloatValue(value.Name, value.Value)).ToArray() ?? [];

    public static EffectManagerFloatValue[] Map(ActiveEffectFloatValue[]? values) =>
        values?.OfType<ActiveEffectFloatValue>()
            .Select(value => new EffectManagerFloatValue(value.Name, value.Value)).ToArray() ?? [];

    public static EffectManagerVectorValue[] Map(EffectManagerFunctionVectorValue[]? values) =>
        values?.OfType<EffectManagerFunctionVectorValue>()
            .Select(value => new EffectManagerVectorValue(value.Name, value.Value)).ToArray() ?? [];

    public static EffectManagerObjectValue[] Map(EffectManagerFunctionObjectValue[]? values) =>
        values?.OfType<EffectManagerFunctionObjectValue>()
            .Select(value => new EffectManagerObjectValue(value.Name, value.Value)).ToArray() ?? [];

    public static EffectManagerObjectValue[] Map(ActiveEffectObjectValue[]? values) =>
        values?.OfType<ActiveEffectObjectValue>()
            .Select(value => new EffectManagerObjectValue(value.Name, value.Value)).ToArray() ?? [];
}
