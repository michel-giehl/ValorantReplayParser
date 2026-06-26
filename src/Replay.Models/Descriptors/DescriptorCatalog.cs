namespace Replay.Models.Descriptors;

public sealed class DescriptorCatalog
{
    private readonly List<ExportGroupDescriptor> _exportGroupDescriptors = [];
    private readonly List<ClassNetCacheDescriptor> _classNetCacheDescriptors = [];

    public IReadOnlyList<ExportGroupDescriptor> ExportGroupDescriptors => _exportGroupDescriptors;

    public IReadOnlyList<ClassNetCacheDescriptor> ClassNetCacheDescriptors => _classNetCacheDescriptors;

    public void Add(ExportGroupDescriptor descriptor)
    {
        _exportGroupDescriptors.Add(descriptor);
    }

    public void Add(ClassNetCacheDescriptor descriptor)
    {
        _classNetCacheDescriptors.Add(descriptor);
    }

    public void AddExportGroup<TDescriptor>()
        where TDescriptor : ExportGroupDescriptor, new()
    {
        Add(new TDescriptor());
    }

    public void AddClassNetCache<TDescriptor>()
        where TDescriptor : ClassNetCacheDescriptor, new()
    {
        Add(new TDescriptor());
    }

    public void Clear()
    {
        _exportGroupDescriptors.Clear();
        _classNetCacheDescriptors.Clear();
    }
}
