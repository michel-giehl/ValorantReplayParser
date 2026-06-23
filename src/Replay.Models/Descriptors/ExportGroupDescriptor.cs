using System.Linq.Expressions;
using System.Reflection;

namespace Replay.Models.Descriptors;

public class ExportGroupDescriptor
{
    private readonly string? _path;
    private readonly ExportCategory _categories;
    private readonly ExportGroupKind _kind;
    private readonly FieldStreamGrammar _grammar;
    private readonly string? _basePath;
    private readonly ExportGroupDescriptor? _baseDescriptor;
    private readonly IReadOnlyList<FieldDescriptor>? _fields;

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
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
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
}

public abstract class ExportGroupDescriptor<TDescriptor> : ExportGroupDescriptor
    where TDescriptor : ExportGroupDescriptor<TDescriptor>
{
    private readonly List<FieldDescriptorBuilder> _fieldBuilders = [];
    private IReadOnlyList<FieldDescriptor>? _fields;
    private bool _isConfiguring;

    public sealed override IReadOnlyList<FieldDescriptor> Fields
    {
        get
        {
            EnsureConfigured();
            return _fields!;
        }
    }

    protected abstract void Configure();

    protected FieldDescriptorBuilder AddProperty<TValue>(
        Expression<Func<TDescriptor, TValue>> property,
        ExportCategory categories = ExportCategory.None)
    {
        var propertyName = GetPropertyName(property);
        return AddField(propertyName, propertyName, handle: null, categories);
    }

    protected FieldDescriptorBuilder AddProperty<TValue>(
        string exportName,
        Expression<Func<TDescriptor, TValue>> property,
        ExportCategory categories = ExportCategory.None)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(exportName);
        return AddField(exportName, GetPropertyName(property), handle: null, categories);
    }

    protected FieldDescriptorBuilder AddPropertyHandle<TValue>(
        uint handle,
        Expression<Func<TDescriptor, TValue>> property,
        ExportCategory categories = ExportCategory.None)
    {
        return AddField(exportName: null, GetPropertyName(property), handle, categories);
    }

    protected FieldDescriptorBuilder AddPropertyHandle<TValue>(
        uint handle,
        string exportName,
        Expression<Func<TDescriptor, TValue>> property,
        ExportCategory categories = ExportCategory.None)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(exportName);
        return AddField(exportName, GetPropertyName(property), handle, categories);
    }

    private FieldDescriptorBuilder AddField(
        string? exportName,
        string? propertyName,
        uint? handle,
        ExportCategory categories)
    {
        if (!_isConfiguring)
        {
            throw new InvalidOperationException(
                $"Descriptor fields for '{GetType().Name}' can only be added from Configure().");
        }

        var builder = new FieldDescriptorBuilder(exportName, propertyName, handle, categories);
        _fieldBuilders.Add(builder);
        return builder;
    }

    private void EnsureConfigured()
    {
        if (_fields is not null)
        {
            return;
        }

        if (_isConfiguring)
        {
            throw new InvalidOperationException($"Descriptor '{GetType().Name}' recursively requested its fields.");
        }

        _fieldBuilders.Clear();
        _isConfiguring = true;
        try
        {
            Configure();
            var fields = new FieldDescriptor[_fieldBuilders.Count];
            for (var i = 0; i < fields.Length; i++)
            {
                fields[i] = _fieldBuilders[i].Build();
            }

            _fields = fields;
        }
        finally
        {
            _isConfiguring = false;
        }
    }

    private static string GetPropertyName<TValue>(Expression<Func<TDescriptor, TValue>> property)
    {
        ArgumentNullException.ThrowIfNull(property);

        var expression = property.Body;
        if (expression is UnaryExpression { NodeType: ExpressionType.Convert or ExpressionType.ConvertChecked } unary)
        {
            expression = unary.Operand;
        }

        if (expression is MemberExpression { Member: PropertyInfo propertyInfo })
        {
            return propertyInfo.Name;
        }

        throw new ArgumentException(
            $"Expression '{property}' must select a property on '{typeof(TDescriptor).Name}'.",
            nameof(property));
    }
}
