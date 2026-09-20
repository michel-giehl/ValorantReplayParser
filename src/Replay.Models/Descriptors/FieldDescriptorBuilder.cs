using System.Reflection;
using Replay.Models.Replay;

namespace Replay.Models.Descriptors;

public sealed class FieldDescriptorBuilder
{
    internal FieldDescriptorBuilder(
        string? exportName,
        string? propertyName,
        PropertyInfo? targetProperty,
        uint? handle,
        ExportCategory categories)
    {
        ExportName = exportName;
        PropertyName = propertyName;
        TargetProperty = targetProperty;
        Handle = handle;
        Categories = categories;
    }

    private string? ExportName { get; }

    private string? PropertyName { get; }

    private PropertyInfo? TargetProperty { get; }

    private uint? Handle { get; }

    private ExportCategory Categories { get; set; }

    private IFieldDecoderDescriptor? Decoder { get; set; }

    private VersionedDefinition<IFieldDecoderDescriptor>? DecoderDefinition { get; set; }

    public FieldDescriptorBuilder WithCategories(ExportCategory categories)
    {
        Categories = categories;
        return this;
    }

    public FieldDescriptorBuilder Decode(IFieldDecoderDescriptor decoder)
    {
        Decoder = decoder ?? throw new ArgumentNullException(nameof(decoder));
        DecoderDefinition = null;
        return this;
    }

    public FieldDescriptorBuilder Decode<TDecoder>(VersionedDefinition<TDecoder> decoderDefinition)
        where TDecoder : IFieldDecoderDescriptor
    {
        ArgumentNullException.ThrowIfNull(decoderDefinition);
        DecoderDefinition = decoderDefinition.Select(static decoder => (IFieldDecoderDescriptor)decoder);
        Decoder = DecoderDefinition.Baseline;
        return this;
    }

    internal FieldDescriptor Build() => new()
    {
        ExportName = ExportName,
        PropertyName = PropertyName,
        TargetProperty = TargetProperty,
        Handle = Handle,
        Categories = Categories,
        Decoder = Decoder,
        DecoderDefinition = DecoderDefinition,
    };
}
