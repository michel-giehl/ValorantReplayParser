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

    public void Clear()
    {
        _exportGroupDescriptors.Clear();
        _classNetCacheDescriptors.Clear();
    }
}
