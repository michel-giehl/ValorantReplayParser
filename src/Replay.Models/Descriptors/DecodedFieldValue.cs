using Replay.Models.Unreal;

namespace Replay.Models.Descriptors;

public readonly record struct DecodedFieldValue
{
    private DecodedFieldValue(
        DecodedFieldValueKind kind,
        bool boolValue = default,
        byte byteValue = default,
        int int32Value = default,
        uint uint32Value = default,
        float floatValue = default,
        uint netGuidValue = default,
        FVector vectorValue = default,
        FRotator rotatorValue = default,
        object? objectValue = null)
    {
        Kind = kind;
        BoolValue = boolValue;
        ByteValue = byteValue;
        Int32Value = int32Value;
        UInt32Value = uint32Value;
        FloatValue = floatValue;
        NetGuidValue = netGuidValue;
        VectorValue = vectorValue;
        RotatorValue = rotatorValue;
        ObjectValue = objectValue;
    }

    public static DecodedFieldValue None { get; } = new(DecodedFieldValueKind.None);

    public DecodedFieldValueKind Kind { get; }

    public bool BoolValue { get; }

    public byte ByteValue { get; }

    public int Int32Value { get; }

    public uint UInt32Value { get; }

    public float FloatValue { get; }

    public uint NetGuidValue { get; }

    public FVector VectorValue { get; }

    public FRotator RotatorValue { get; }

    public object? ObjectValue { get; }

    public bool HasValue => Kind != DecodedFieldValueKind.None;

    public static DecodedFieldValue FromBool(bool value) => new(DecodedFieldValueKind.Bool, boolValue: value);

    public static DecodedFieldValue FromByte(byte value) => new(DecodedFieldValueKind.Byte, byteValue: value);

    public static DecodedFieldValue FromInt32(int value) => new(DecodedFieldValueKind.Int32, int32Value: value);

    public static DecodedFieldValue FromUInt32(uint value) => new(DecodedFieldValueKind.UInt32, uint32Value: value);

    public static DecodedFieldValue FromFloat(float value) => new(DecodedFieldValueKind.Float, floatValue: value);

    public static DecodedFieldValue FromNetGuid(uint value) => new(DecodedFieldValueKind.NetGuid, netGuidValue: value);

    public static DecodedFieldValue FromVector(FVector value) => new(DecodedFieldValueKind.Vector, vectorValue: value);

    public static DecodedFieldValue FromRotator(FRotator value) => new(DecodedFieldValueKind.Rotator, rotatorValue: value);

    public static DecodedFieldValue FromObject(object value) => new(DecodedFieldValueKind.Object, objectValue: value);
}

public enum DecodedFieldValueKind
{
    None,
    Bool,
    Byte,
    Int32,
    UInt32,
    Float,
    NetGuid,
    Vector,
    Rotator,
    Object,
}
