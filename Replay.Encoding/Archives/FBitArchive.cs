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

    public byte PeekByte()
    {
        using var checkpoint = CreateCheckpoint();
        var value = ReadByte();
        return value;
    }

    public bool PeekBit()
    {
        using var checkpoint = CreateCheckpoint();
        var value = ReadBit();
        return value;
    }

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
        if (count < 0)
        {
            throw InvalidCount(nameof(ReadBytes), Position, Length, count);
        }

        return ReadBits(checked(count * 8));
    }

    public override bool TryReadBytes(int count, out ReadOnlyMemory<byte> value)
    {
        if (count < 0)
        {
            value = default;
            return false;
        }

        return TryReadBits(checked(count * 8), out value);
    }

    public uint ReadSerializedInt(int maxValue)
    {
        if (maxValue <= 0)
        {
            throw new ArchiveReadException(ArchiveErrorCode.MalformedSerializedInt, nameof(ReadSerializedInt), Position,
                Length, maxValue);
        }

        uint value = 0;
        for (uint mask = 1; value + mask < maxValue; mask <<= 1)
        {
            if (!TryReadBit(out var bit))
            {
                throw EndOfArchive(nameof(ReadSerializedInt), Position, Length, 1);
            }

            if (bit)
            {
                value |= mask;
            }
        }

        return value;
    }

    public override void Seek(long position) => SeekBits(position);

    public override void Skip(long count) => SkipBits(count);

    public string ReadFString()
    {
        var length = ReadInt32();
        if (length == 0)
        {
            return string.Empty;
        }

        if (length == int.MinValue)
        {
            throw new ArchiveReadException(ArchiveErrorCode.InvalidCount, nameof(ReadFString), Position, Length,
                length);
        }

        var isUnicode = length < 0;
        int byteCount;
        if (isUnicode)
        {
            checked { byteCount = -length * 2; }
        }
        else
        {
            byteCount = length;
        }

        if (byteCount <= 0 || byteCount > 1024 * 1024)
        {
            throw new ArchiveReadException(ArchiveErrorCode.InvalidCount, nameof(ReadFString), Position, Length,
                byteCount, $"FString byte count {byteCount} exceeds maximum allowed.");
        }

        var encoding = isUnicode ? System.Text.Encoding.Unicode : System.Text.Encoding.UTF8;
        var bytes = ReadBytes(byteCount);
        return encoding.GetString(bytes.Span).TrimEnd('\0');
    }

    public string ReadFName()
    {
        var isHardcoded = ReadBit();
        if (isHardcoded)
        {
            var nameIndex = ReadIntPacked();
            return nameIndex.ToString(System.Globalization.CultureInfo.InvariantCulture);
        }

        var inString = ReadFString();
        _ = ReadInt32();
        return inString;
    }
}