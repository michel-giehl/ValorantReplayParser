using System.Buffers.Binary;

namespace Replay.Encoding.Archives;

public abstract class FArchive : IDisposable
{
    public abstract long Position { get; }

    public abstract long Length { get; }

    public long Remaining => Length - Position;

    public bool AtEnd => Remaining == 0;

    public abstract byte ReadByte();

    public abstract bool TryReadByte(out byte value);

    public abstract ReadOnlyMemory<byte> ReadBytes(int count);

    public abstract bool TryReadBytes(int count, out ReadOnlyMemory<byte> value);

    public abstract void Seek(long position);

    public abstract void Skip(long count);

    internal abstract void RestorePosition(long position);

    public ArchiveCheckpoint CreateCheckpoint() => new(this);

    public void Seek(long offset, SeekOrigin origin)
    {
        var position = origin switch
        {
            SeekOrigin.Begin => offset,
            SeekOrigin.Current => Position + offset,
            SeekOrigin.End => Length - offset,
            _ => offset
        };

        Seek(position);
    }

    public void SkipRemaining() => Skip(Remaining);

    public void EnsureFullyConsumed(string operation = "EnsureFullyConsumed")
    {
        if (!AtEnd)
        {
            throw new ArchiveReadException(ArchiveErrorCode.UnexpectedTrailingData, operation, Position, Length, Remaining);
        }
    }

    public sbyte ReadSByte() => unchecked((sbyte)ReadByte());

    public ushort ReadUInt16() => BinaryPrimitives.ReadUInt16LittleEndian(ReadBytes(sizeof(ushort)).Span);

    public short ReadInt16() => BinaryPrimitives.ReadInt16LittleEndian(ReadBytes(sizeof(short)).Span);

    public uint ReadUInt32() => BinaryPrimitives.ReadUInt32LittleEndian(ReadBytes(sizeof(uint)).Span);

    public int ReadInt32() => BinaryPrimitives.ReadInt32LittleEndian(ReadBytes(sizeof(int)).Span);

    public ulong ReadUInt64() => BinaryPrimitives.ReadUInt64LittleEndian(ReadBytes(sizeof(ulong)).Span);

    public long ReadInt64() => BinaryPrimitives.ReadInt64LittleEndian(ReadBytes(sizeof(long)).Span);

    public float ReadSingle() => BitConverter.UInt32BitsToSingle(ReadUInt32());

    public double ReadDouble() => BitConverter.UInt64BitsToDouble(ReadUInt64());

    public bool TryReadUInt32(out uint value)
    {
        if (TryReadBytes(sizeof(uint), out var bytes))
        {
            value = BinaryPrimitives.ReadUInt32LittleEndian(bytes.Span);
            return true;
        }

        value = 0;
        return false;
    }

    public virtual uint ReadIntPacked()
    {
        uint value = 0;
        var shift = 0;

        for (var i = 0; i < 5; i++)
        {
            var nextByte = ReadByte();
            value |= (uint)(nextByte >> 1) << shift;

            if ((nextByte & 1) == 0)
            {
                return value;
            }

            shift += 7;
        }

        throw new ArchiveReadException(
            ArchiveErrorCode.MalformedPackedInteger,
            nameof(ReadIntPacked),
            Position,
            Length,
            0,
            "Packed integer did not terminate within five bytes.");
    }

    protected static ArchiveReadException InvalidCount(string operation, long position, long length, long count) =>
        new(ArchiveErrorCode.InvalidCount, operation, position, length, count);

    protected static ArchiveReadException EndOfArchive(string operation, long position, long length, long count) =>
        new(ArchiveErrorCode.EndOfArchive, operation, position, length, count);

    protected static ArchiveReadException InvalidSeek(string operation, long position, long length, long target) =>
        new(ArchiveErrorCode.InvalidSeek, operation, position, length, target);

    protected virtual void Dispose(bool disposing)
    {
    }

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }
}
