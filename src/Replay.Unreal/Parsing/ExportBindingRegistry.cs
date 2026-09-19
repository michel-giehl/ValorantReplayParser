using Replay.Models.Descriptors;
using Replay.Models.Net;

namespace Replay.Unreal.Parsing;

public sealed class ExportBindingRegistry
{
    private readonly DescriptorCatalogIndex _catalogIndex = new();
    private readonly BoundExportStore _store = new();
    private readonly ParseProfile _parseProfile;
    private ReplayExportBinder _binder;
    private long _catalogRevision;

    public ExportBindingRegistry(
        DescriptorCatalog? descriptorCatalog = null,
        ParseProfile? parseProfile = null)
    {
        _parseProfile = parseProfile ?? ParseProfile.Default;
        _binder = new ReplayExportBinder(_catalogIndex, _store, _parseProfile);
        if (descriptorCatalog is not null)
        {
            SetCatalog(descriptorCatalog);
        }
    }

    public void SetCatalog(DescriptorCatalog descriptorCatalog)
    {
        Clear();
        _catalogIndex.SetCatalog(descriptorCatalog);
        _store.SetPathAliasProvider(_catalogIndex.PathAliasProvider);
        _binder = new ReplayExportBinder(_catalogIndex, _store, _parseProfile);
        _catalogRevision = checked(_catalogRevision + 1);
    }

    public void OnExportGroupAdded(NetFieldExportGroup replayGroup) =>
        OnExportGroupChanged(replayGroup);

    public void OnExportGroupChanged(NetFieldExportGroup replayGroup)
    {
        var path = replayGroup.PathName;
        _store.SetPathIndex(replayGroup.PathNameIndex, path);

        if (_catalogIndex.TryGetExportDescriptor(path, out var exportDescriptor))
        {
            var bound = _binder.BindExportGroup(replayGroup, exportDescriptor);
            _store.IndexBoundExportGroup(path, bound);
            _store.ResolvePendingRpcFunctions(exportDescriptor.Path, bound);
            _store.ResolvePendingRpcFunctions(path, bound);
            return;
        }

        if (_catalogIndex.TryGetClassNetCacheDescriptor(path, out var cacheDescriptor))
        {
            var bound = _binder.BindClassNetCache(replayGroup, cacheDescriptor);
            _store.IndexBoundClassNetCache(path, bound);
        }
    }

    public BoundExportGroup? GetBoundGroup(string path) => _store.GetBoundGroup(path);

    internal string? GetSubobjectClassPath(string objectName) =>
        _catalogIndex.GetSubobjectClassPath(objectName);

    internal string? GetAlternatePath(string path) => _catalogIndex.PathAliasProvider?.GetAlternatePath(path);

    internal long CatalogRevision => _catalogRevision;

    public BoundExportGroup? GetBoundGroupByIndex(uint pathNameIndex) => _store.GetBoundGroupByIndex(pathNameIndex);

    public BoundClassNetCache? GetBoundCache(string path)
    {
        if (_store.GetBoundCache(path) is { } boundCache)
        {
            return boundCache;
        }

        if (!_catalogIndex.TryGetClassNetCacheDescriptor(path, out var descriptor))
        {
            return null;
        }

        boundCache = _binder.BindClassNetCache(descriptor);
        if (boundCache is null)
        {
            return null;
        }

        _store.IndexBoundClassNetCache(descriptor.Path, boundCache);
        return boundCache;
    }

    public BoundClassNetCache? GetBoundCacheByIndex(uint pathNameIndex) => _store.GetBoundCacheByIndex(pathNameIndex);

    public ExportGroupKind GetExportGroupKind(string path)
    {
        var boundGroup = _store.GetBoundGroup(path);
        return boundGroup is not null
            ? boundGroup.SourceDescriptor.Kind
            : _catalogIndex.GetExportGroupKind(path);
    }

    public bool HasBinding(string path) => _store.HasBinding(path);

    public void Clear()
    {
        _store.Clear();
    }
}
