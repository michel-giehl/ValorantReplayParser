using Replay.Encoding.Net;
using Replay.Models.Descriptors;

namespace Replay.Unreal.Parsing;

public sealed class ExportBindingRegistry
{
    private const string ClassNetCacheSuffix = "_ClassNetCache";
    private static readonly StringComparer PathComparer = StringComparer.Ordinal;

    private readonly Dictionary<string, BoundExportGroup> _boundGroupsByPath = new(PathComparer);
    private readonly Dictionary<string, BoundClassNetCache> _boundCachesByPath = new(PathComparer);
    private readonly Dictionary<string, ExportGroupDescriptor> _exportDescriptorsByPath = new(PathComparer);
    private readonly Dictionary<string, ExportGroupKind> _exportKindsByDefaultObjectName = new(PathComparer);
    private readonly Dictionary<string, ClassNetCacheDescriptor> _cacheDescriptorsByPath = new(PathComparer);
    private readonly Dictionary<string, List<BoundRpcFunction>> _pendingRpcFunctionsByExportPath = new(PathComparer);
    private readonly Dictionary<uint, string> _pathIndexToPath = new();
    private readonly ParseProfile _parseProfile;

    public ExportBindingRegistry(
        DescriptorCatalog? descriptorCatalog = null,
        ParseProfile? parseProfile = null)
    {
        _parseProfile = parseProfile ?? ParseProfile.Default;
        if (descriptorCatalog is not null)
        {
            SetCatalog(descriptorCatalog);
        }
    }

    public void SetCatalog(DescriptorCatalog descriptorCatalog)
    {
        ArgumentNullException.ThrowIfNull(descriptorCatalog);

        Clear();
        _exportDescriptorsByPath.Clear();
        _exportKindsByDefaultObjectName.Clear();
        _cacheDescriptorsByPath.Clear();

        foreach (var descriptor in descriptorCatalog.ExportGroupDescriptors)
        {
            _exportDescriptorsByPath[descriptor.Path] = descriptor;
            IndexDefaultObjectKind(descriptor);
        }

        foreach (var descriptor in descriptorCatalog.ClassNetCacheDescriptors)
        {
            _cacheDescriptorsByPath[descriptor.Path] = descriptor;
            IndexInlineRpcFieldDescriptors(descriptor);
        }
    }

    public void OnExportGroupAdded(NetFieldExportGroup replayGroup) =>
        OnExportGroupChanged(replayGroup);

    public void OnExportGroupChanged(NetFieldExportGroup replayGroup)
    {
        var path = replayGroup.PathName;
        _pathIndexToPath[replayGroup.PathNameIndex] = path;

        if (_exportDescriptorsByPath.TryGetValue(path, out var exportDescriptor))
        {
            var bound = BindExportGroup(replayGroup, exportDescriptor);
            _boundGroupsByPath[path] = bound;
            ResolvePendingRpcFunctions(path, bound);
            return;
        }

        if (_cacheDescriptorsByPath.TryGetValue(path, out var cacheDescriptor))
        {
            var bound = BindClassNetCache(replayGroup, cacheDescriptor);
            IndexBoundClassNetCache(path, bound);
        }
    }

    public BoundExportGroup? GetBoundGroup(string path)
    {
        return _boundGroupsByPath.GetValueOrDefault(path);
    }

    public BoundExportGroup? GetBoundGroupByIndex(uint pathNameIndex)
    {
        if (_pathIndexToPath.TryGetValue(pathNameIndex, out var path))
        {
            return GetBoundGroup(path);
        }

        return null;
    }

    public BoundClassNetCache? GetBoundCache(string path)
    {
        return _boundCachesByPath.GetValueOrDefault(path);
    }

    public ExportGroupKind GetExportGroupKind(string path)
    {
        if (_boundGroupsByPath.TryGetValue(path, out var boundGroup))
        {
            return boundGroup.SourceDescriptor.Kind;
        }

        return _exportDescriptorsByPath.TryGetValue(path, out var descriptor)
            ? descriptor.Kind
            : _exportKindsByDefaultObjectName.GetValueOrDefault(path, ExportGroupKind.Unknown);
    }

    public BoundClassNetCache? GetBoundCacheByIndex(uint pathNameIndex)
    {
        if (_pathIndexToPath.TryGetValue(pathNameIndex, out var path))
        {
            return GetBoundCache(path);
        }

        return null;
    }

    public bool HasBinding(string path) =>
        _boundGroupsByPath.ContainsKey(path) || _boundCachesByPath.ContainsKey(path);

    public void Clear()
    {
        _boundGroupsByPath.Clear();
        _boundCachesByPath.Clear();
        _pathIndexToPath.Clear();
        _pendingRpcFunctionsByExportPath.Clear();
    }

    private void IndexInlineRpcFieldDescriptors(ClassNetCacheDescriptor descriptor)
    {
        foreach (var rpcDescriptor in descriptor.FunctionFields)
        {
            if (rpcDescriptor.Fields.Count == 0 || _exportDescriptorsByPath.ContainsKey(rpcDescriptor.FunctionExportPath))
            {
                continue;
            }

            _exportDescriptorsByPath[rpcDescriptor.FunctionExportPath] = new ExportGroupDescriptor
            (
                rpcDescriptor.FunctionExportPath,
                rpcDescriptor.Categories,
                ExportGroupKind.ClassNetCache,
                FieldStreamGrammar.FunctionParameters,
                fields: rpcDescriptor.Fields);
        }
    }

    private BoundExportGroup BindExportGroup(
        NetFieldExportGroup replayGroup,
        ExportGroupDescriptor descriptor)
    {
        var maxHandle = checked((int)replayGroup.NetFieldExportsLength);
        var fields = new FieldBinding[maxHandle];
        var groupEnabled = IsPathSelected(descriptor.Path) && IsCategorySelected(descriptor.Categories);

        var allFields = new List<FieldDescriptor>();
        CollectFields(descriptor, allFields);

        foreach (var fieldDesc in allFields)
        {
            var handle = ResolveHandle(fieldDesc, replayGroup);
            if (handle is null || handle.Value >= maxHandle)
            {
                continue;
            }

            var h = handle.Value;
            var exportName = fieldDesc.ExportName ?? replayGroup.NetFieldExports[h]?.Name;
            var propertyName = fieldDesc.PropertyName ?? exportName;
            var categories = fieldDesc.Categories == ExportCategory.None
                ? descriptor.Categories
                : fieldDesc.Categories;
            var decoder = ResolveFieldDecoder(fieldDesc);
            var enabled = decoder is not null
                && groupEnabled
                && IsFieldSelected(descriptor.Path, propertyName, exportName, categories);

            fields[h] = new FieldBinding
            {
                Enabled = enabled,
                Categories = categories,
                Decoder = enabled ? decoder : null,
                Name = propertyName,
                ExportName = exportName,
            };
        }

        for (var i = 0; i < maxHandle; i++)
        {
            if (fields[i].Name is null && fields[i].Decoder is null)
            {
                fields[i] = new FieldBinding
                {
                    Enabled = false,
                    Name = replayGroup.NetFieldExports[i]?.Name,
                    ExportName = replayGroup.NetFieldExports[i]?.Name,
                };
            }
        }

        return new BoundExportGroup
        {
            SourceDescriptor = descriptor,
            Categories = descriptor.Categories,
            Grammar = descriptor.Grammar,
            Enabled = groupEnabled,
            FieldsByHandle = fields,
        };
    }

    private BoundClassNetCache BindClassNetCache(
        NetFieldExportGroup replayGroup,
        ClassNetCacheDescriptor descriptor)
    {
        var maxHandle = checked((int)replayGroup.NetFieldExportsLength);
        var functions = new BoundRpcFunction[maxHandle];
        var cacheEnabled = IsPathSelected(descriptor.Path);

        foreach (var rpcDesc in descriptor.FunctionFields)
        {
            var handle = ResolveFunctionHandle(rpcDesc, replayGroup);
            if (handle is null || handle.Value >= maxHandle)
            {
                continue;
            }

            var categories = rpcDesc.Categories;
            var decoder = ResolveRpcDecoder(rpcDesc);
            var functionGroup = _boundGroupsByPath.GetValueOrDefault(rpcDesc.FunctionExportPath);
            var hasFunctionDescriptor = rpcDesc.Fields.Count > 0 || _exportDescriptorsByPath.ContainsKey(rpcDesc.FunctionExportPath);
            var enabled = cacheEnabled
                && IsFieldSelected(descriptor.Path, rpcDesc.Name, rpcDesc.Name, categories)
                && (decoder is not null || hasFunctionDescriptor);

            var function = new BoundRpcFunction
            {
                Name = rpcDesc.Name,
                FunctionExportPath = rpcDesc.FunctionExportPath,
                Categories = categories,
                Enabled = enabled,
                FunctionGroup = functionGroup,
                Decoder = enabled ? decoder : null,
            };

            functions[handle.Value] = function;
            if (functionGroup is null && hasFunctionDescriptor)
            {
                AddPendingRpcFunction(rpcDesc.FunctionExportPath, function);
            }
        }

        return new BoundClassNetCache
        {
            Path = descriptor.Path,
            SourceDescriptor = descriptor,
            Grammar = descriptor.Grammar,
            Enabled = cacheEnabled,
            FunctionsByHandle = functions,
        };
    }

    private void IndexBoundClassNetCache(string path, BoundClassNetCache bound)
    {
        _boundCachesByPath[path] = bound;

        var aliasLength = path.Length - ClassNetCacheSuffix.Length;
        if (aliasLength <= 0 || !path.EndsWith(ClassNetCacheSuffix, StringComparison.Ordinal))
        {
            return;
        }

        var alias = path[..aliasLength];
        if (!_boundCachesByPath.TryGetValue(alias, out var existing) || existing.Path == path)
        {
            _boundCachesByPath[alias] = bound;
        }
    }

    private void IndexDefaultObjectKind(ExportGroupDescriptor descriptor)
    {
        var leafStart = descriptor.Path.LastIndexOfAny(['/', '.', ':']);
        var leaf = leafStart >= 0 ? descriptor.Path[(leafStart + 1)..] : descriptor.Path;
        if (leaf.Length == 0)
        {
            return;
        }

        _exportKindsByDefaultObjectName["Default__" + leaf] = descriptor.Kind;
    }

    private void AddPendingRpcFunction(string functionExportPath, BoundRpcFunction function)
    {
        if (!_pendingRpcFunctionsByExportPath.TryGetValue(functionExportPath, out var pendingFunctions))
        {
            pendingFunctions = [];
            _pendingRpcFunctionsByExportPath.Add(functionExportPath, pendingFunctions);
        }

        pendingFunctions.Add(function);
    }

    private void ResolvePendingRpcFunctions(string functionExportPath, BoundExportGroup functionGroup)
    {
        if (!_pendingRpcFunctionsByExportPath.Remove(functionExportPath, out var pendingFunctions))
        {
            return;
        }

        foreach (var pendingFunction in pendingFunctions)
        {
            pendingFunction.FunctionGroup = functionGroup;
        }
    }

    private void CollectFields(ExportGroupDescriptor descriptor, List<FieldDescriptor> fields)
    {
        if (descriptor.BaseDescriptor is not null)
        {
            CollectFields(descriptor.BaseDescriptor, fields);
        }
        else if (descriptor.BasePath is not null && _exportDescriptorsByPath.TryGetValue(descriptor.BasePath, out var baseDescriptor))
        {
            CollectFields(baseDescriptor, fields);
        }

        fields.AddRange(descriptor.Fields);
    }

    private static uint? ResolveHandle(FieldDescriptor fieldDesc, NetFieldExportGroup replayGroup)
    {
        if (fieldDesc.Handle.HasValue)
        {
            return fieldDesc.Handle.Value;
        }

        if (fieldDesc.ExportName is not null)
        {
            for (uint i = 0; i < replayGroup.NetFieldExportsLength; i++)
            {
                if (replayGroup.NetFieldExports[i]?.Name == fieldDesc.ExportName)
                {
                    return i;
                }
            }
        }

        return null;
    }

    private static uint? ResolveFunctionHandle(RpcDescriptor rpcDesc, NetFieldExportGroup replayGroup)
    {
        for (uint i = 0; i < replayGroup.NetFieldExportsLength; i++)
        {
            if (replayGroup.NetFieldExports[i]?.Name == rpcDesc.Name)
            {
                return i;
            }
        }

        return null;
    }

    private static IFieldDecoder? ResolveFieldDecoder(FieldDescriptor fieldDesc)
    {
        if (fieldDesc.Decoder is null)
        {
            return null;
        }

        return fieldDesc.Decoder as IFieldDecoder
            ?? throw new InvalidOperationException(
                $"Field descriptor '{fieldDesc.PropertyName ?? fieldDesc.ExportName ?? fieldDesc.Handle?.ToString() ?? "<unnamed>"}' uses an incompatible decoder type.");
    }

    private static IRpcDecoder? ResolveRpcDecoder(RpcDescriptor rpcDesc)
    {
        if (rpcDesc.Decoder is null)
        {
            return null;
        }

        return rpcDesc.Decoder as IRpcDecoder
            ?? throw new InvalidOperationException(
                $"RPC descriptor '{rpcDesc.Name}' uses an incompatible decoder type.");
    }

    private bool IsPathSelected(string path)
    {
        if (_parseProfile.IncludedPaths is not null && !_parseProfile.IncludedPaths.Contains(path))
        {
            return false;
        }

        return _parseProfile.ExcludedPaths is null || !_parseProfile.ExcludedPaths.Contains(path);
    }

    private bool IsFieldSelected(string path, string? propertyName, string? exportName, ExportCategory categories)
    {
        if (!IsCategorySelected(categories))
        {
            return false;
        }

        if (_parseProfile.IncludedFields is null)
        {
            return true;
        }

        return IsIncludedFieldName(propertyName, path) || IsIncludedFieldName(exportName, path);
    }

    private bool IsIncludedFieldName(string? fieldName, string path)
    {
        return fieldName is not null
            && _parseProfile.IncludedFields is not null
            && (_parseProfile.IncludedFields.Contains(fieldName)
                || _parseProfile.IncludedFields.Contains($"{path}:{fieldName}"));
    }

    private bool IsCategorySelected(ExportCategory categories)
    {
        if (_parseProfile.EnabledCategories == ExportCategory.None)
        {
            return false;
        }

        return categories == ExportCategory.None
            || (categories & _parseProfile.EnabledCategories) != 0;
    }
}
