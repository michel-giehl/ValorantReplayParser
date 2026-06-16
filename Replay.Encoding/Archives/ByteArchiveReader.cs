using System.Buffers;

namespace Replay.Encoding.Archives;

public sealed class ByteArchiveReader : FArchive
{
    private readonly IMemoryOwner<byte>? _owner;
    private readonly ReadOnlyMemory<byte> _buffer;
    private long _position;

    public ByteArchiveReader(Stream input)
    {
        using var memoryStream = new MemoryStream();
        input.CopyTo(memoryStream);
        _buffer = memoryStream.ToArray();
    }

    public ByteArchiveReader(ReadOnlyMemory<byte> input) => _buffer = input;

    public ByteArchiveReader(ReadOnlySpan<byte> input) => _buffer = input.ToArray();

    public ByteArchiveReader(IMemoryOwner<byte> owner, int length)
    {
        ArgumentNullException.ThrowIfNull(owner);
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
        if (!TryReadByte(out var value))
        {
            throw EndOfArchive(nameof(ReadByte), Position, Length, 1);
        }

        return value;
    }

    public override bool TryReadByte(out byte value)
    {
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
        if (!TryReadBytes(count, out var value))
        {
            if (count < 0)
            {
                throw InvalidCount(nameof(ReadBytes), Position, Length, count);
            }

            throw EndOfArchive(nameof(ReadBytes), Position, Length, count);
        }

        return value;
    }

    public override bool TryReadBytes(int count, out ReadOnlyMemory<byte> value)
    {
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
        if (position < 0 || position > Length)
        {
            throw InvalidSeek(nameof(Seek), Position, Length, position);
        }

        _position = position;
    }

    public override void Skip(long count)
    {
        if (count < 0)
        {
            throw InvalidCount(nameof(Skip), Position, Length, count);
        }

        Seek(Position + count);
    }

    internal override void RestorePosition(long position) => _position = position;

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _owner?.Dispose();
        }
    }
}
