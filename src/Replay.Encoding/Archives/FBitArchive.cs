using System.Numerics;

namespace Replay.Encoding.Archives;

public abstract class FBitArchive : FArchive
{
    public abstract long BitPosition { get; protected set; }

    public abstract long BitLength { get; }

    public long BitsRemaining => BitLength - BitPosition;

    public override long Position => BitPosition;

    public override long Length => BitLength;

    public abstract bool ReadBit();

    public abstract bool TryReadBit(out bool value);

    public abstract ulong ReadBitsToUInt64(int bitCount);

    public abstract bool TryReadBitsToUInt64(int bitCount, out ulong value);

    public abstract ReadOnlyMemory<byte> ReadBits(int bitCount);

    public abstract bool TryReadBits(int bitCount, out ReadOnlyMemory<byte> value);

    public abstract void CopyBitsTo(Span<byte> destination, int bitCount);

    public abstract FBitArchive ReadSubArchive(int bitCount);

    public abstract void SeekBits(long position);

    public abstract void SkipBits(long count);

    public override byte ReadByte() => (byte)ReadBitsToUInt64(8);

    public override bool TryReadByte(out byte value)
    {
        if (TryReadBitsToUInt64(8, out var bits))
        {
            value = (byte)bits;
            return true;
        }

        value = 0;
        return false;
    }

    public override ReadOnlyMemory<byte> ReadBytes(int count)
    {
        if (count is < 0 or > int.MaxValue / 8)
        {
            throw InvalidCount(nameof(ReadBytes), Position, Length, count);
        }

        return ReadBits(count * 8);
    }

    public override bool TryReadBytes(int count, out ReadOnlyMemory<byte> value)
    {
        if (count is >= 0 and <= int.MaxValue / 8) return TryReadBits(count * 8, out value);
        value = default;
        return false;

    }

    public uint ReadSerializedInt(int maxValue)
    {
        if (maxValue <= 0)
        {
            throw new ArchiveReadException(ArchiveErrorCode.MalformedSerializedInt, nameof(ReadSerializedInt), Position,
                Length, maxValue);
        }

        var valueBitCount = BitOperations.Log2((uint)maxValue);
        uint value = 0;
        if (valueBitCount > 0)
        {
            if (!TryReadBitsToUInt64(valueBitCount, out var bits))
            {
                throw EndOfArchive(nameof(ReadSerializedInt), Position, Length, valueBitCount);
            }

            value = (uint)bits;
        }

        var mask = 1U << valueBitCount;
        if (value + mask >= maxValue) return value;
        if (!TryReadBit(out var highBit))
        {
            throw EndOfArchive(nameof(ReadSerializedInt), Position, Length, 1);
        }

        if (highBit)
        {
            value |= mask;
        }

        return value;
    }

    public override void Seek(long position) => SeekBits(position);

    public override void Skip(long count) => SkipBits(count);

    public string ReadFName() => ReadFNameCore(ReadBit);
}
