using Replay.Models.Descriptors;

namespace Replay.Unreal.Parsing;

internal sealed class BoundExportStore
{
    private static readonly StringComparer PathComparer = StringComparer.Ordinal;

    private readonly Dictionary<string, BoundExportGroup> _boundGroupsByPath = new(PathComparer);
    private readonly Dictionary<string, BoundClassNetCache> _boundCachesByPath = new(PathComparer);
    private readonly Dictionary<string, List<BoundRpcFunction>> _pendingRpcFunctionsByExportPath = new(PathComparer);
    private readonly Dictionary<uint, string> _pathIndexToPath = new();
    private IReplayPathAliasProvider? _pathAliasProvider;

    public void SetPathAliasProvider(IReplayPathAliasProvider? provider) => _pathAliasProvider = provider;

    public void SetPathIndex(uint pathNameIndex, string path)
    {
        _pathIndexToPath[pathNameIndex] = path;
    }

    public BoundExportGroup? GetBoundGroup(string path) =>
        TryGetByLookup(_boundGroupsByPath, ReplayPath.LookupKeys(path, _pathAliasProvider), out var boundGroup) ? boundGroup : null;

    public BoundExportGroup? GetBoundGroupByIndex(uint pathNameIndex)
    {
        if (_pathIndexToPath.TryGetValue(pathNameIndex, out var path))
        {
            return GetBoundGroup(path);
        }

        return null;
    }

    public BoundClassNetCache? GetBoundCache(string path) =>
        TryGetByLookup(_boundCachesByPath, ReplayPath.ClassNetCacheLookupKeys(path, _pathAliasProvider), out var boundCache) ? boundCache : null;

    public BoundClassNetCache? GetBoundCacheByIndex(uint pathNameIndex)
    {
        if (_pathIndexToPath.TryGetValue(pathNameIndex, out var path))
        {
            return GetBoundCache(path);
        }

        return null;
    }

    public bool HasBinding(string path) => GetBoundGroup(path) is not null || GetBoundCache(path) is not null;

    public void IndexBoundExportGroup(string path, BoundExportGroup bound)
    {
        _boundGroupsByPath[path] = bound;
    }

    public void IndexBoundClassNetCache(string path, BoundClassNetCache bound)
    {
        _boundCachesByPath[path] = bound;
    }

    public void AddPendingRpcFunction(string functionExportPath, BoundRpcFunction function)
    {
        if (!_pendingRpcFunctionsByExportPath.TryGetValue(functionExportPath, out var pendingFunctions))
        {
            pendingFunctions = [];
            _pendingRpcFunctionsByExportPath.Add(functionExportPath, pendingFunctions);
        }

        pendingFunctions.Add(function);
    }

    public void ResolvePendingRpcFunctions(string functionExportPath, BoundExportGroup functionGroup)
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

    public void Clear()
    {
        _boundGroupsByPath.Clear();
        _boundCachesByPath.Clear();
        _pathIndexToPath.Clear();
        _pendingRpcFunctionsByExportPath.Clear();
    }

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
}
