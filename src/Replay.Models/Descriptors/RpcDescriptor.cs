namespace Replay.Models.Descriptors;

public sealed class RpcDescriptor
{
    public required string Name { get; init; }
    public required string FunctionExportPath { get; init; }
    public ExportCategory Categories { get; init; }
    public IReadOnlyList<FieldDescriptor> Fields { get; init; } = [];
    public IRpcDecoderDescriptor? Decoder { get; init; }
}

public sealed class RpcDescriptorBuilder
{
    private readonly List<FieldDescriptorBuilder> _fieldBuilders = [];
    private readonly string _name;
    private readonly string _functionExportPath;

    internal RpcDescriptorBuilder(string name, string functionExportPath, ExportCategory categories)
    {
        _name = name;
        _functionExportPath = functionExportPath;
        Categories = categories;
    }

    internal ExportCategory Categories { get; private set; }

    internal IRpcDecoderDescriptor? Decoder { get; private set; }

    public RpcDescriptorBuilder WithCategories(ExportCategory categories)
    {
        Categories = categories;
        return this;
    }

    public RpcDescriptorBuilder Decode(IRpcDecoderDescriptor decoder)
    {
        Decoder = decoder;
        return this;
    }

    public RpcDescriptorBuilder AddField(
        string exportName,
        string propertyName,
        ExportCategory categories = ExportCategory.None)
    {
        _fieldBuilders.Add(new FieldDescriptorBuilder(exportName, propertyName, handle: null, categories));
        return this;
    }

    public RpcDescriptorBuilder AddFieldHandle(
        uint handle,
        string propertyName,
        ExportCategory categories = ExportCategory.None)
    {
        _fieldBuilders.Add(new FieldDescriptorBuilder(exportName: null, propertyName, handle, categories));
        return this;
    }

    internal RpcDescriptor Build()
    {
        var fields = new FieldDescriptor[_fieldBuilders.Count];
        for (var i = 0; i < fields.Length; i++)
        {
            fields[i] = _fieldBuilders[i].Build();
        }

        return new RpcDescriptor
        {
            Name = _name,
            FunctionExportPath = _functionExportPath,
            Categories = Categories,
            Decoder = Decoder,
            Fields = fields,
        };
    }
}

public interface IRpcDecoderDescriptor
{
}
