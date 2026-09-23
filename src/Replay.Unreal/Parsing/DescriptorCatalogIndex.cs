using Replay.Models.Descriptors;
using Replay.Models.Replay;

namespace Replay.Unreal.Parsing;

internal sealed class DescriptorCatalogIndex
{
    private static readonly StringComparer PathComparer = StringComparer.Ordinal;

    private readonly Dictionary<string, ExportGroupDescriptor> _exportDescriptorsByPath = new(PathComparer);
    private readonly Dictionary<string, ExportGroupDescriptor> _exportDescriptorAliasesByPath = new(PathComparer);
    private readonly Dictionary<string, ExportGroupKind> _exportKindsByDefaultObjectName = new(PathComparer);
    private readonly Dictionary<string, ClassNetCacheDescriptor> _cacheDescriptorsByPath = new(PathComparer);
    private readonly Dictionary<string, string> _subobjectClassPaths = new(PathComparer);
    private IReplayPathAliasProvider? _pathAliasProvider;
    private ReplayReleaseVersion? _releaseVersion;

    public void SetCatalog(
        DescriptorCatalog descriptorCatalog,
        ReplayReleaseVersion? releaseVersion = null)
    {
        Clear();
        _releaseVersion = releaseVersion;
        _pathAliasProvider = descriptorCatalog.PathAliasProvider;

        foreach (var (objectName, classPath) in descriptorCatalog.SubobjectClassPaths)
        {
            _subobjectClassPaths.Add(objectName, classPath);
        }

        foreach (var definition in descriptorCatalog.ExportGroupDefinitions)
        {
            IndexExportDescriptor(definition.Resolve(releaseVersion, "export-group descriptor"));
        }

        foreach (var definition in descriptorCatalog.ClassNetCacheDefinitions)
        {
            var descriptor = definition.Resolve(releaseVersion, "class-net-cache descriptor");
            IndexClassNetCacheDescriptor(descriptor);
            IndexRpcParameterDescriptors(descriptor);
        }
    }

    public void Clear()
    {
        _exportDescriptorsByPath.Clear();
        _exportDescriptorAliasesByPath.Clear();
        _exportKindsByDefaultObjectName.Clear();
        _cacheDescriptorsByPath.Clear();
        _subobjectClassPaths.Clear();
        _pathAliasProvider = null;
        _releaseVersion = null;
    }

    public string? GetSubobjectClassPath(string objectName) =>
        _subobjectClassPaths.GetValueOrDefault(objectName);

    public IReplayPathAliasProvider? PathAliasProvider => _pathAliasProvider;

    public bool TryGetExportDescriptor(string path, out ExportGroupDescriptor descriptor) =>
        TryGetExportByLookup(ReplayPath.LookupKeys(path, _pathAliasProvider), out descriptor!);

    // Export-table binding must not alias a property table to its class's RPC table.
    // Suffixless class-path lookup is only appropriate when resolving an RPC payload.
    public bool TryGetClassNetCacheExportDescriptor(string path, out ClassNetCacheDescriptor descriptor) =>
        TryGetByLookup(_cacheDescriptorsByPath, ReplayPath.LookupKeys(path, _pathAliasProvider), out descriptor!);

    public bool TryGetClassNetCacheDescriptor(string path, out ClassNetCacheDescriptor descriptor) =>
        TryGetByLookup(_cacheDescriptorsByPath, ReplayPath.ClassNetCacheLookupKeys(path, _pathAliasProvider), out descriptor!);

    public ExportGroupKind GetExportGroupKind(string path) =>
        TryGetExportDescriptor(path, out var descriptor)
            ? descriptor.Kind
            : _exportKindsByDefaultObjectName.GetValueOrDefault(path, ExportGroupKind.Unknown);

    public void CollectFields(ExportGroupDescriptor descriptor, List<FieldDescriptor> fields)
    {
        if (descriptor.BaseDescriptor is not null)
        {
            var baseDescriptor = TryGetExportDescriptor(descriptor.BaseDescriptor.Path, out var selectedBase)
                ? selectedBase
                : descriptor.BaseDescriptor;
            CollectFields(baseDescriptor, fields);
        }
        else if (descriptor.BasePath is not null && TryGetExportDescriptor(descriptor.BasePath, out var baseDescriptor))
        {
            CollectFields(baseDescriptor, fields);
        }

        fields.AddRange(descriptor.Fields);
    }

    private void IndexExportDescriptor(ExportGroupDescriptor descriptor)
    {
        _exportDescriptorsByPath[descriptor.Path] = descriptor;

        if (ReplayPath.GetDefaultObjectName(descriptor.Path) is { } defaultObjectName)
        {
            _exportDescriptorAliasesByPath[defaultObjectName] = descriptor;
            _exportKindsByDefaultObjectName[defaultObjectName] = descriptor.Kind;
        }
    }

    private void IndexClassNetCacheDescriptor(ClassNetCacheDescriptor descriptor)
    {
        _cacheDescriptorsByPath[descriptor.Path] = descriptor;
    }

    private void IndexRpcParameterDescriptors(ClassNetCacheDescriptor descriptor)
    {
        foreach (var rpcDescriptor in descriptor.FunctionFields)
        {
            if (_exportDescriptorsByPath.ContainsKey(rpcDescriptor.FunctionExportPath))
            {
                continue;
            }

            var parameterDescriptor = ResolveParameterDescriptor(rpcDescriptor);
            if (parameterDescriptor is not null)
            {
                IndexExportDescriptor(parameterDescriptor);
                continue;
            }

            if (rpcDescriptor.Fields.Count == 0)
            {
                continue;
            }

            var descriptorFromRpc = new ExportGroupDescriptor
            (
                rpcDescriptor.FunctionExportPath,
                rpcDescriptor.Categories,
                ExportGroupKind.ClassNetCache,
                FieldStreamGrammar.FunctionParameters,
                fields: rpcDescriptor.Fields);
            IndexExportDescriptor(descriptorFromRpc);
        }
    }

    public ExportGroupDescriptor? ResolveParameterDescriptor(RpcDescriptor descriptor) =>
        descriptor.ParameterDescriptorDefinition?.Resolve(_releaseVersion, "RPC parameter descriptor")
        ?? descriptor.ParameterDescriptor;

    private static bool TryGetByLookup<TValue>(
        Dictionary<string, TValue> valuesByPath,
        IEnumerable<string> keys,
        out TValue value)
    {
        foreach (var key in keys)
        {
            if (valuesByPath.TryGetValue(key, out value!))
            {
                return true;
            }
        }

        value = default!;
        return false;
    }

    private bool TryGetExportByLookup(
        IEnumerable<string> keys,
        out ExportGroupDescriptor descriptor)
    {
        foreach (var key in keys)
        {
            if (_exportDescriptorsByPath.TryGetValue(key, out descriptor!) ||
                _exportDescriptorAliasesByPath.TryGetValue(key, out descriptor!))
            {
                return true;
            }
        }

        descriptor = default!;
        return false;
    }
}
