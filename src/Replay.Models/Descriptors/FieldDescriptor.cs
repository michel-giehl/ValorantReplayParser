namespace Replay.Models.Descriptors;

public sealed class FieldDescriptor
{
    public string? ExportName { get; init; }
    public string? PropertyName { get; init; }
    public uint? Handle { get; init; }
    public ExportCategory Categories { get; init; }
    public IFieldDecoderDescriptor? Decoder { get; init; }
}

public sealed class FieldDescriptorBuilder
{
    internal FieldDescriptorBuilder(string? exportName, string? propertyName, uint? handle, ExportCategory categories)
    {
        ExportName = exportName;
        PropertyName = propertyName;
        Handle = handle;
        Categories = categories;
    }

    internal string? ExportName { get; }

    internal string? PropertyName { get; }

    internal uint? Handle { get; }

    internal ExportCategory Categories { get; private set; }

    internal IFieldDecoderDescriptor? Decoder { get; private set; }

    public FieldDescriptorBuilder WithCategories(ExportCategory categories)
    {
        Categories = categories;
        return this;
    }

    public FieldDescriptorBuilder Decode(IFieldDecoderDescriptor decoder)
    {
        ArgumentNullException.ThrowIfNull(decoder);
        Decoder = decoder;
        return this;
    }

    internal FieldDescriptor Build() => new()
    {
        ExportName = ExportName,
        PropertyName = PropertyName,
        Handle = Handle,
        Categories = Categories,
        Decoder = Decoder,
    };
}

public interface IFieldDecoderDescriptor
{
}
