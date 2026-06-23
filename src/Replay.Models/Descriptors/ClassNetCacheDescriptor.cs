namespace Replay.Models.Descriptors;

public class ClassNetCacheDescriptor
{
    private readonly string? _path;
    private readonly FieldStreamGrammar _grammar;
    private readonly IReadOnlyList<RpcDescriptor>? _functionFields;

    protected ClassNetCacheDescriptor()
    {
        _grammar = FieldStreamGrammar.ClassNetCache;
    }

    public ClassNetCacheDescriptor(
        string path,
        IReadOnlyList<RpcDescriptor>? functionFields = null,
        FieldStreamGrammar grammar = FieldStreamGrammar.ClassNetCache)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        _path = path;
        _grammar = grammar;
        _functionFields = functionFields ?? [];
    }

    public virtual string Path => _path ?? string.Empty;

    public virtual FieldStreamGrammar Grammar => _grammar;

    public virtual IReadOnlyList<RpcDescriptor> FunctionFields => _functionFields ?? [];
}

public abstract class ClassNetCacheDescriptor<TDescriptor> : ClassNetCacheDescriptor
    where TDescriptor : ClassNetCacheDescriptor<TDescriptor>
{
    private readonly List<RpcDescriptorBuilder> _functionBuilders = [];
    private IReadOnlyList<RpcDescriptor>? _functionFields;
    private bool _isConfiguring;

    public sealed override IReadOnlyList<RpcDescriptor> FunctionFields
    {
        get
        {
            EnsureConfigured();
            return _functionFields!;
        }
    }

    protected abstract void Configure();

    protected RpcDescriptorBuilder AddFunction(
        string name,
        string functionExportPath,
        ExportCategory categories = ExportCategory.None)
    {
        if (!_isConfiguring)
        {
            throw new InvalidOperationException(
                $"Class-net-cache functions for '{GetType().Name}' can only be added from Configure().");
        }

        var builder = new RpcDescriptorBuilder(name, functionExportPath, categories);
        _functionBuilders.Add(builder);
        return builder;
    }

    private void EnsureConfigured()
    {
        if (_functionFields is not null)
        {
            return;
        }

        if (_isConfiguring)
        {
            throw new InvalidOperationException($"Descriptor '{GetType().Name}' recursively requested its functions.");
        }

        _functionBuilders.Clear();
        _isConfiguring = true;
        try
        {
            Configure();
            var functions = new RpcDescriptor[_functionBuilders.Count];
            for (var i = 0; i < functions.Length; i++)
            {
                functions[i] = _functionBuilders[i].Build();
            }

            _functionFields = functions;
        }
        finally
        {
            _isConfiguring = false;
        }
    }
}
