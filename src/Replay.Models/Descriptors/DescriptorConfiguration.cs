namespace Replay.Models.Descriptors;

internal sealed class DescriptorConfiguration<TBuilder, TItem>
{
    private readonly List<TBuilder> _builders = [];
    private IReadOnlyList<TItem>? _items;
    private bool _isConfiguring;

    public void Add(TBuilder builder, string descriptorName, string memberName)
    {
        if (!_isConfiguring)
        {
            throw new InvalidOperationException(
                $"{memberName} for '{descriptorName}' can only be added from Configure().");
        }

        _builders.Add(builder);
    }

    public IReadOnlyList<TItem> GetOrConfigure(
        string descriptorName,
        string recursiveMemberName,
        Action configure,
        Func<TBuilder, TItem> build)
    {
        if (Volatile.Read(ref _items) is { } configured)
        {
            return configured;
        }

        // Descriptors are shared between readers on different threads; the lock is reentrant, so a
        // descriptor requesting its own members while configuring is still caught below.
        lock (_builders)
        {
            if (_items is not null)
            {
                return _items;
            }

            if (_isConfiguring)
            {
                throw new InvalidOperationException($"Descriptor '{descriptorName}' recursively requested its {recursiveMemberName}.");
            }

            _builders.Clear();
            _isConfiguring = true;
            try
            {
                configure();
                var items = new TItem[_builders.Count];
                for (var i = 0; i < items.Length; i++)
                {
                    items[i] = build(_builders[i]);
                }

                Volatile.Write(ref _items, items);
                return items;
            }
            finally
            {
                _isConfiguring = false;
            }
        }
    }
}