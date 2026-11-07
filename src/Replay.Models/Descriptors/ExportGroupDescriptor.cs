using System.Linq.Expressions;
using System.Reflection;
using Replay.Models.Net;

namespace Replay.Models.Descriptors;

public class ExportGroupDescriptor : IDecodedPayload
{
    private readonly string? _path;
    private readonly ExportCategory _categories;
    private readonly ExportGroupKind _kind;
    private readonly FieldStreamGrammar _grammar;
    private readonly string? _basePath;
    private readonly ExportGroupDescriptor? _baseDescriptor;
    private readonly IReadOnlyList<FieldDescriptor>? _fields;
    private readonly HashSet<string> _decodedProperties = new(StringComparer.Ordinal);

    protected ExportGroupDescriptor()
    {
        _grammar = FieldStreamGrammar.RepLayoutProperties;
    }

    public ExportGroupDescriptor(
        string path,
        ExportCategory categories = ExportCategory.None,
        ExportGroupKind kind = ExportGroupKind.Unknown,
        FieldStreamGrammar grammar = FieldStreamGrammar.RepLayoutProperties,
        string? basePath = null,
        ExportGroupDescriptor? baseDescriptor = null,
        IReadOnlyList<FieldDescriptor>? fields = null)
    {
        _path = path;
        _categories = categories;
        _kind = kind;
        _grammar = grammar;
        _basePath = basePath;
        _baseDescriptor = baseDescriptor;
        _fields = fields ?? [];
    }

    public virtual string Path => _path ?? string.Empty;

    public virtual ExportCategory Categories => _categories;

    public virtual ExportGroupKind Kind => _kind;

    public virtual FieldStreamGrammar Grammar => _grammar;

    public virtual string? BasePath => _basePath;

    public virtual ExportGroupDescriptor? BaseDescriptor => _baseDescriptor;

    public virtual IReadOnlyList<FieldDescriptor> Fields => _fields ?? [];

    public IReadOnlySet<string> DecodedProperties => _decodedProperties;

    public bool HasDecoded(string propertyName) => _decodedProperties.Contains(propertyName);

    public virtual object CreatePayloadInstance()
    {
        var descriptorType = GetType();
        return descriptorType == typeof(ExportGroupDescriptor)
            ? new ExportGroupDescriptor(Path, Categories, Kind, Grammar, BasePath, BaseDescriptor, Fields)
            : Activator.CreateInstance(descriptorType, nonPublic: true)
              ?? throw new InvalidOperationException(
                  $"Could not create payload instance for descriptor '{descriptorType.FullName}'.");
    }

    public void MarkDecoded(string propertyName)
    {
        if (!string.IsNullOrWhiteSpace(propertyName))
        {
            _decodedProperties.Add(propertyName);
        }
    }
}

public abstract class ExportGroupDescriptor<TDescriptor> : ExportGroupDescriptor
    where TDescriptor : ExportGroupDescriptor<TDescriptor>
{
    private readonly DescriptorConfiguration<FieldDescriptorBuilder, FieldDescriptor> _fields = new();

    public sealed override IReadOnlyList<FieldDescriptor> Fields =>
        _fields.GetOrConfigure(GetType().Name, "fields", Configure, static builder => builder.Build());

    protected abstract void Configure();

    protected FieldDescriptorBuilder AddProperty<TValue>(
        Expression<Func<TDescriptor, TValue>> property,
        ExportCategory categories = ExportCategory.None)
    {
        var propertyInfo = GetProperty(property);
        return AddField(propertyInfo.Name, propertyInfo, exportName: propertyInfo.Name, handle: null, categories);
    }

    protected FieldDescriptorBuilder AddProperty<TValue>(
        string exportName,
        Expression<Func<TDescriptor, TValue>> property,
        ExportCategory categories = ExportCategory.None)
    {
        var propertyInfo = GetProperty(property);
        return AddField(propertyInfo.Name, propertyInfo, exportName, handle: null, categories);
    }

    protected FieldDescriptorBuilder AddPropertyHandle<TValue>(
        uint handle,
        Expression<Func<TDescriptor, TValue>> property,
        ExportCategory categories = ExportCategory.None)
    {
        var propertyInfo = GetProperty(property);
        return AddField(propertyInfo.Name, propertyInfo, exportName: null, handle, categories);
    }

    protected FieldDescriptorBuilder AddPropertyHandle<TValue>(
        uint handle,
        string exportName,
        Expression<Func<TDescriptor, TValue>> property,
        ExportCategory categories = ExportCategory.None)
    {
        var propertyInfo = GetProperty(property);
        return AddField(propertyInfo.Name, propertyInfo, exportName, handle, categories);
    }

    /// <summary>
    /// Adds a field with no backing CLR property: the handle comes from <paramref name="resolveHandle"/> and the
    /// decoded value goes to <paramref name="assign"/>. <paramref name="decodedMarker"/> is the property name that
    /// <c>HasDecoded</c> reports once the field was assigned. <paramref name="bindingState"/> computes per-replay
    /// state when the field is bound; it reaches <paramref name="assign"/> as its last argument.
    /// </summary>
    protected FieldDescriptorBuilder AddResolvedField(
        string name,
        string exportName,
        Func<NetFieldExportGroup, uint?> resolveHandle,
        Action<TDescriptor, DecodedFieldValue, object?> assign,
        string decodedMarker,
        ExportCategory categories = ExportCategory.None,
        Func<NetFieldExportGroup, object?>? bindingState = null)
    {
        var builder = AddField(name, targetProperty: null, exportName, handle: null, categories);
        builder.ResolveHandle(resolveHandle)
            .AssignWith((payload, value, state) => assign((TDescriptor)payload, value, state), decodedMarker);
        return bindingState is null ? builder : builder.WithBindingState(bindingState);
    }

    private FieldDescriptorBuilder AddField(
        string? propertyName,
        PropertyInfo? targetProperty,
        string? exportName,
        uint? handle,
        ExportCategory categories)
    {
        var builder = new FieldDescriptorBuilder(exportName, propertyName, targetProperty, handle, categories);
        _fields.Add(builder, GetType().Name, "Descriptor fields");
        return builder;
    }

    private static PropertyInfo GetProperty<TValue>(Expression<Func<TDescriptor, TValue>> property)
    {
        var expression = property.Body;
        if (expression is UnaryExpression { NodeType: ExpressionType.Convert or ExpressionType.ConvertChecked } unary)
        {
            expression = unary.Operand;
        }

        if (expression is MemberExpression { Member: PropertyInfo propertyInfo })
        {
            return propertyInfo;
        }

        throw new ArgumentException(
            $"Expression '{property}' must select a property on '{typeof(TDescriptor).Name}'.",
            nameof(property));
    }
}
