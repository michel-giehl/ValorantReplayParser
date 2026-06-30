using Replay.Models.Descriptors;

namespace Replay.Unreal.Parsing;

public static class DescriptorBuilderDecoderExtensions
{
    public static FieldDescriptorBuilder Int32(this FieldDescriptorBuilder builder) =>
        builder.Decode(PrimitiveDecoders.Int32);

    public static FieldDescriptorBuilder UInt32(this FieldDescriptorBuilder builder) =>
        builder.Decode(PrimitiveDecoders.UInt32);

    public static FieldDescriptorBuilder Float(this FieldDescriptorBuilder builder) =>
        builder.Decode(PrimitiveDecoders.Float);

    public static FieldDescriptorBuilder Bool(this FieldDescriptorBuilder builder) =>
        builder.Decode(PrimitiveDecoders.Bool);

    public static FieldDescriptorBuilder Byte(this FieldDescriptorBuilder builder) =>
        builder.Decode(PrimitiveDecoders.Byte);

    public static FieldDescriptorBuilder ObjectNetGuid(this FieldDescriptorBuilder builder) =>
        builder.Decode(PrimitiveDecoders.ObjectNetGuid);

    public static FieldDescriptorBuilder FVector(this FieldDescriptorBuilder builder) =>
        builder.Decode(PrimitiveDecoders.Vector);

    public static FieldDescriptorBuilder FVectorNetQuantize(this FieldDescriptorBuilder builder) =>
        builder.Decode(PrimitiveDecoders.VectorNetQuantize);

    public static FieldDescriptorBuilder FVectorNetQuantize10(this FieldDescriptorBuilder builder) =>
        builder.Decode(PrimitiveDecoders.VectorNetQuantize10);

    public static FieldDescriptorBuilder FVectorNetQuantize100(this FieldDescriptorBuilder builder) =>
        builder.Decode(PrimitiveDecoders.VectorNetQuantize100);

    public static FieldDescriptorBuilder FVectorNetQuantizeNormal(this FieldDescriptorBuilder builder) =>
        builder.Decode(PrimitiveDecoders.VectorNetQuantizeNormal);

    public static FieldDescriptorBuilder Ignore(this FieldDescriptorBuilder builder) =>
        builder.Decode(PrimitiveDecoders.Skip);
}
