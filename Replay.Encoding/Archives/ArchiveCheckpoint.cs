namespace Replay.Encoding.Archives;

public sealed class ArchiveCheckpoint : IDisposable
{
    private readonly FArchive _archive;
    private readonly long _position;
    private bool _committed;
    private bool _disposed;

    internal ArchiveCheckpoint(FArchive archive)
    {
        _archive = archive;
        _position = archive.Position;
    }

    public void Commit()
    {
        ThrowIfDisposed();
        _committed = true;
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        if (!_committed)
        {
            _archive.RestorePosition(_position);
        }

        _disposed = true;
    }

    private void ThrowIfDisposed()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
    }
}
