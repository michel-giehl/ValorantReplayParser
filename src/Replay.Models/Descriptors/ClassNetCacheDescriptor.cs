namespace Replay.Models.Descriptors;

using global::Replay.Models.Replay;

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
        _path = path;
        _grammar = grammar;
        _functionFields = functionFields ?? [];
    }

    public virtual string Path => _path ?? string.Empty;

    public FieldStreamGrammar Grammar => _grammar;

    public virtual IReadOnlyList<RpcDescriptor> FunctionFields => _functionFields ?? [];
}

public abstract class ClassNetCacheDescriptor<TDescriptor> : ClassNetCacheDescriptor
    where TDescriptor : ClassNetCacheDescriptor<TDescriptor>
{
    private readonly DescriptorConfiguration<RpcDescriptorBuilder, RpcDescriptor> _functionFields = new();

    public sealed override IReadOnlyList<RpcDescriptor> FunctionFields =>
        _functionFields.GetOrConfigure(GetType().Name, "functions", Configure, static builder => builder.Build());

    protected abstract void Configure();

    protected RpcDescriptorBuilder AddFunction(
        string name,
        string functionExportPath,
        ExportCategory categories = ExportCategory.None)
    {
        var builder = new RpcDescriptorBuilder(name, functionExportPath, null, categories);
        _functionFields.Add(builder, GetType().Name, "Class-net-cache functions");
        return builder;
    }

    protected RpcDescriptorBuilder AddFunction<TParameters>(
        string name,
        string functionExportPath,
        ExportCategory categories = ExportCategory.None)
        where TParameters : ExportGroupDescriptor, new()
    {
        var builder = new RpcDescriptorBuilder(name, functionExportPath, null, categories, new TParameters());
        _functionFields.Add(builder, GetType().Name, "Class-net-cache functions");
        return builder;
    }

    protected RpcDescriptorBuilder AddFunction<TParameters>(
        string name,
        string functionExportPath,
        VersionedDefinition<TParameters> parameterDescriptors,
        ExportCategory categories = ExportCategory.None)
        where TParameters : ExportGroupDescriptor
    {
        var builder = new RpcDescriptorBuilder(name, functionExportPath, null, categories)
            .WithParameters(parameterDescriptors);
        _functionFields.Add(builder, GetType().Name, "Class-net-cache functions");
        return builder;
    }

    protected RpcDescriptorBuilder AddFunctionHandle(
        uint handle,
        string name,
        string functionExportPath,
        ExportCategory categories = ExportCategory.None)
    {
        var builder = new RpcDescriptorBuilder(name, functionExportPath, handle, categories);
        _functionFields.Add(builder, GetType().Name, "Class-net-cache functions");
        return builder;
    }

    protected RpcDescriptorBuilder AddFunctionHandle<TParameters>(
        uint handle,
        string name,
        string functionExportPath,
        ExportCategory categories = ExportCategory.None)
        where TParameters : ExportGroupDescriptor, new()
    {
        var builder = new RpcDescriptorBuilder(name, functionExportPath, handle, categories, new TParameters());
        _functionFields.Add(builder, GetType().Name, "Class-net-cache functions");
        return builder;
    }

    protected RpcDescriptorBuilder AddFunctionHandle<TParameters>(
        uint handle,
        string name,
        string functionExportPath,
        VersionedDefinition<TParameters> parameterDescriptors,
        ExportCategory categories = ExportCategory.None)
        where TParameters : ExportGroupDescriptor
    {
        var builder = new RpcDescriptorBuilder(name, functionExportPath, handle, categories)
            .WithParameters(parameterDescriptors);
        _functionFields.Add(builder, GetType().Name, "Class-net-cache functions");
        return builder;
    }
}
