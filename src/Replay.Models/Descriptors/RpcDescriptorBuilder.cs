namespace Replay.Models.Descriptors;

using global::Replay.Models.Replay;

public sealed class RpcDescriptorBuilder
{
    private readonly List<FieldDescriptorBuilder> _fieldBuilders = [];
    private readonly string _name;
    private readonly string _functionExportPath;
    private readonly uint? _handle;
    private ExportGroupDescriptor? _parameterDescriptor;
    private VersionedDefinition<ExportGroupDescriptor>? _parameterDescriptorDefinition;

    internal RpcDescriptorBuilder(
        string name,
        string functionExportPath,
        uint? handle,
        ExportCategory categories,
        ExportGroupDescriptor? parameterDescriptor = null)
    {
        _name = name;
        _functionExportPath = functionExportPath;
        _handle = handle;
        _parameterDescriptor = parameterDescriptor;
        Categories = categories;
    }

    private ExportCategory Categories { get; set; }

    private IRpcDecoderDescriptor? Decoder { get; set; }

    private VersionedDefinition<IRpcDecoderDescriptor>? DecoderDefinition { get; set; }

    public void Decode(IRpcDecoderDescriptor decoder)
    {
        Decoder = decoder ?? throw new ArgumentNullException(nameof(decoder));
        DecoderDefinition = null;
    }

    public void Decode<TDecoder>(VersionedDefinition<TDecoder> decoderDefinition)
        where TDecoder : IRpcDecoderDescriptor
    {
        ArgumentNullException.ThrowIfNull(decoderDefinition);
        DecoderDefinition = decoderDefinition.Select(static decoder => (IRpcDecoderDescriptor)decoder);
        Decoder = DecoderDefinition.Baseline;
    }

    public RpcDescriptorBuilder WithParameters<TDescriptor>(VersionedDefinition<TDescriptor> descriptorDefinition)
        where TDescriptor : ExportGroupDescriptor
    {
        ArgumentNullException.ThrowIfNull(descriptorDefinition);
        var expectedPath = descriptorDefinition.Baseline.Path;
        _parameterDescriptorDefinition = descriptorDefinition.Select(descriptor => descriptor.Path == expectedPath
            ? (ExportGroupDescriptor)descriptor
            : throw new ArgumentException(
                $"Versioned RPC parameter descriptors must use the same path. Expected '{expectedPath}', got '{descriptor.Path}'.",
                nameof(descriptorDefinition)));
        _parameterDescriptor = _parameterDescriptorDefinition.Baseline;
        return this;
    }

    public FieldDescriptorBuilder AddField(string exportName,
        string propertyName,
        ExportCategory categories = ExportCategory.None)
    {
        var builder = new FieldDescriptorBuilder(exportName, propertyName, targetProperty: null, handle: null, categories);
        _fieldBuilders.Add(builder);
        return builder;
    }

    public FieldDescriptorBuilder AddFieldHandle(
        uint handle,
        string propertyName,
        ExportCategory categories = ExportCategory.None)
    {
        var builder = new FieldDescriptorBuilder(exportName: null, propertyName, targetProperty: null, handle, categories);
        _fieldBuilders.Add(builder);
        return builder;
    }

    internal RpcDescriptor Build()
    {
        FieldDescriptor[] fields;
        if (_fieldBuilders.Count == 0 && _parameterDescriptor is not null)
        {
            fields = _parameterDescriptor.Fields.ToArray();
        }
        else
        {
            fields = new FieldDescriptor[_fieldBuilders.Count];
            for (var i = 0; i < fields.Length; i++)
            {
                fields[i] = _fieldBuilders[i].Build();
            }
        }

        return new RpcDescriptor
        {
            Name = _name,
            FunctionExportPath = _functionExportPath,
            Handle = _handle,
            Categories = Categories,
            ParameterDescriptor = _parameterDescriptor,
            ParameterDescriptorDefinition = _parameterDescriptorDefinition,
            Decoder = Decoder,
            DecoderDefinition = DecoderDefinition,
            Fields = fields,
        };
    }
}
