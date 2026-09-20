namespace Replay.Models.Descriptors;

using global::Replay.Models.Replay;

public sealed class DescriptorCatalog
{
    private readonly List<VersionedDefinition<ExportGroupDescriptor>> _exportGroupDefinitions = [];
    private readonly List<VersionedDefinition<ClassNetCacheDescriptor>> _classNetCacheDefinitions = [];
    private readonly Dictionary<string, string> _subobjectClassPaths = new(StringComparer.Ordinal);

    public IReadOnlyDictionary<string, string> SubobjectClassPaths => _subobjectClassPaths;

    public IReplayPathAliasProvider? PathAliasProvider { get; init; }

    public void AddSubobjectClassPath(string objectName, string classPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(objectName);
        ArgumentException.ThrowIfNullOrWhiteSpace(classPath);
        _subobjectClassPaths.Add(objectName, classPath);
    }

    public IReadOnlyList<ExportGroupDescriptor> ExportGroupDescriptors =>
        _exportGroupDefinitions.Select(definition => definition.Baseline).ToArray();

    public IReadOnlyList<ClassNetCacheDescriptor> ClassNetCacheDescriptors =>
        _classNetCacheDefinitions.Select(definition => definition.Baseline).ToArray();

    public IReadOnlyList<VersionedDefinition<ExportGroupDescriptor>> ExportGroupDefinitions =>
        _exportGroupDefinitions;

    public IReadOnlyList<VersionedDefinition<ClassNetCacheDescriptor>> ClassNetCacheDefinitions =>
        _classNetCacheDefinitions;

    public void Add(ExportGroupDescriptor descriptor)
    {
        _exportGroupDefinitions.Add(new VersionedDefinition<ExportGroupDescriptor>(descriptor));
    }

    public void Add(IEnumerable<ExportGroupDescriptor> descriptors)
    {
        foreach (var descriptor in descriptors)
        {
            Add(descriptor);
        }
    }

    public void Add(VersionedDefinition<ExportGroupDescriptor> definition)
    {
        ArgumentNullException.ThrowIfNull(definition);
        _exportGroupDefinitions.Add(ValidateExportGroupPaths(definition));
    }

    public void AddExportGroup<TDescriptor>(VersionedDefinition<TDescriptor> definition)
        where TDescriptor : ExportGroupDescriptor
    {
        ArgumentNullException.ThrowIfNull(definition);
        Add(definition.Select(static descriptor => (ExportGroupDescriptor)descriptor));
    }

    public void Add(ClassNetCacheDescriptor descriptor)
    {
        _classNetCacheDefinitions.Add(new VersionedDefinition<ClassNetCacheDescriptor>(descriptor));
    }

    public void Add(IEnumerable<ClassNetCacheDescriptor> descriptor)
    {
        foreach (var item in descriptor)
        {
            Add(item);
        }
    }

    public void Add(VersionedDefinition<ClassNetCacheDescriptor> definition)
    {
        ArgumentNullException.ThrowIfNull(definition);
        _classNetCacheDefinitions.Add(ValidateClassNetCachePaths(definition));
    }

    public void AddClassNetCache<TDescriptor>(VersionedDefinition<TDescriptor> definition)
        where TDescriptor : ClassNetCacheDescriptor
    {
        ArgumentNullException.ThrowIfNull(definition);
        Add(definition.Select(static descriptor => (ClassNetCacheDescriptor)descriptor));
    }

    public void Clear()
    {
        _exportGroupDefinitions.Clear();
        _classNetCacheDefinitions.Clear();
        _subobjectClassPaths.Clear();
    }

    private static VersionedDefinition<ExportGroupDescriptor> ValidateExportGroupPaths(
        VersionedDefinition<ExportGroupDescriptor> definition)
    {
        var expectedPath = definition.Baseline.Path;
        return definition.Select(descriptor => descriptor.Path == expectedPath
            ? descriptor
            : throw new ArgumentException(
                $"Versioned export-group descriptors must use the same path. Expected '{expectedPath}', got '{descriptor.Path}'.",
                nameof(definition)));
    }

    private static VersionedDefinition<ClassNetCacheDescriptor> ValidateClassNetCachePaths(
        VersionedDefinition<ClassNetCacheDescriptor> definition)
    {
        var expectedPath = definition.Baseline.Path;
        return definition.Select(descriptor => descriptor.Path == expectedPath
            ? descriptor
            : throw new ArgumentException(
                $"Versioned class-net-cache descriptors must use the same path. Expected '{expectedPath}', got '{descriptor.Path}'.",
                nameof(definition)));
    }
}
