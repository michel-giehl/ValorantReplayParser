using System.Buffers;

namespace Replay.Encoding.Archives;

public class ByteArchiveReader : FArchive
{
    private readonly IMemoryOwner<byte>? _owner;
    private readonly ReadOnlyMemory<byte> _buffer;
    private long _position;
    private bool _disposed;

    protected ByteArchiveReader(Stream input)
    {
        using var memoryStream = new MemoryStream();
        input.CopyTo(memoryStream);
        _buffer = memoryStream.ToArray();
    }

    protected ByteArchiveReader(ReadOnlyMemory<byte> input) => _buffer = input;

    public ByteArchiveReader(ReadOnlySpan<byte> input) => _buffer = input.ToArray();

    public ByteArchiveReader(IMemoryOwner<byte> owner, int length)
    {
        if (length < 0 || length > owner.Memory.Length)
        {
            throw InvalidCount(nameof(ByteArchiveReader), 0, owner.Memory.Length, length);
        }

        _owner = owner;
        _buffer = owner.Memory[..length];
    }

    public override long Position => _position;

    public override long Length => _buffer.Length;

    public override byte ReadByte()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (!TryReadByte(out var value))
        {
            throw EndOfArchive(nameof(ReadByte), Position, Length, 1);
        }

        return value;
    }

    public override bool TryReadByte(out byte value)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (Remaining < 1)
        {
            value = 0;
            return false;
        }

        value = _buffer.Span[(int)_position];
        _position++;
        return true;
    }

    public override ReadOnlyMemory<byte> ReadBytes(int count)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (TryReadBytes(count, out var value)) return value;
        if (count < 0)
        {
            throw InvalidCount(nameof(ReadBytes), Position, Length, count);
        }

        throw EndOfArchive(nameof(ReadBytes), Position, Length, count);

    }

    public override bool TryReadBytes(int count, out ReadOnlyMemory<byte> value)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (count < 0 || Remaining < count)
        {
            value = default;
            return false;
        }

        value = _buffer.Slice((int)_position, count);
        _position += count;
        return true;
    }

    public override void Seek(long position)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (position < 0 || position > Length)
        {
            throw InvalidSeek(nameof(Seek), Position, Length, position);
        }

        _position = position;
    }

    public override void Skip(long count)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (count < 0)
        {
            throw InvalidCount(nameof(Skip), Position, Length, count);
        }

        Seek(Position + count);
    }

    protected internal override void RestorePosition(long position)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        _position = position;
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing && !_disposed)
        {
            _disposed = true;
            _owner?.Dispose();
        }
    }
}
