using System.Buffers;

namespace Replay.Encoding.Archives;

public sealed class BitArchiveReader : FBitArchive
{
    private readonly IMemoryOwner<byte>? _owner;
    private readonly ReadOnlyMemory<byte> _buffer;
    private readonly long _startBit;

    public BitArchiveReader(ReadOnlyMemory<byte> input)
        : this(input, 0, input.Length * 8)
    {
    }

    public BitArchiveReader(ReadOnlySpan<byte> input)
        : this((ReadOnlyMemory<byte>)input.ToArray())
    {
    }

    public BitArchiveReader(ReadOnlyMemory<byte> input, int bitCount)
        : this(input, 0, bitCount)
    {
    }

    public BitArchiveReader(ReadOnlySpan<byte> input, int bitCount)
        : this((ReadOnlyMemory<byte>)input.ToArray(), bitCount)
    {
    }

    public BitArchiveReader(IMemoryOwner<byte> owner, int bitCount)
        : this(owner, owner.Memory, 0, bitCount)
    {
    }

    private BitArchiveReader(ReadOnlyMemory<byte> input, long startBit, long bitLength)
        : this(null, input, startBit, bitLength)
    {
    }

    private BitArchiveReader(IMemoryOwner<byte>? owner, ReadOnlyMemory<byte> input, long startBit, long bitLength)
    {
        if (startBit < 0 || bitLength < 0 || startBit + bitLength > input.Length * 8L)
        {
            throw new ArchiveReadException(ArchiveErrorCode.InvalidBitCount, nameof(BitArchiveReader), 0, input.Length * 8L, bitLength);
        }

        _owner = owner;
        _buffer = input;
        _startBit = startBit;
        BitLength = bitLength;
    }

    public override long BitPosition { get; protected set; }

    public override long BitLength { get; }

    public override bool ReadBit()
    {
        if (!TryReadBit(out var value))
        {
            throw EndOfArchive(nameof(ReadBit), Position, Length, 1);
        }

        return value;
    }

    public override bool TryReadBit(out bool value)
    {
        if (BitsRemaining < 1)
        {
            value = false;
            return false;
        }

        var absoluteBit = _startBit + BitPosition;
        var byteIndex = (int)(absoluteBit >> 3);
        var bitIndex = (int)(absoluteBit & 7);
        value = (_buffer.Span[byteIndex] & (1 << bitIndex)) != 0;
        BitPosition++;
        return true;
    }

    public override ulong ReadBitsToUInt64(int bitCount)
    {
        if (TryReadBitsToUInt64(bitCount, out var value)) return value;
        if (bitCount is < 0 or > 64)
        {
            throw new ArchiveReadException(ArchiveErrorCode.InvalidBitCount, nameof(ReadBitsToUInt64), Position, Length, bitCount);
        }

        throw EndOfArchive(nameof(ReadBitsToUInt64), Position, Length, bitCount);

    }

    public override bool TryReadBitsToUInt64(int bitCount, out ulong value)
    {
        if (bitCount is < 0 or > 64 || BitsRemaining < bitCount)
        {
            value = 0;
            return false;
        }

        value = 0;
        for (var i = 0; i < bitCount; i++)
        {
            if (ReadBit())
            {
                value |= 1UL << i;
            }
        }

        return true;
    }

    public override ReadOnlyMemory<byte> ReadBits(int bitCount)
    {
        if (!TryReadBits(bitCount, out var value))
        {
            if (bitCount < 0)
            {
                throw new ArchiveReadException(ArchiveErrorCode.InvalidBitCount, nameof(ReadBits), Position, Length, bitCount);
            }

            throw EndOfArchive(nameof(ReadBits), Position, Length, bitCount);
        }

        return value;
    }

    public override bool TryReadBits(int bitCount, out ReadOnlyMemory<byte> value)
    {
        if (bitCount < 0 || BitsRemaining < bitCount)
        {
            value = default;
            return false;
        }

        var output = new byte[(bitCount + 7) / 8];
        CopyBitsTo(output, bitCount);
        value = output;
        return true;
    }

    public override void CopyBitsTo(Span<byte> destination, int bitCount)
    {
        if (bitCount < 0)
        {
            throw new ArchiveReadException(ArchiveErrorCode.InvalidBitCount, nameof(CopyBitsTo), Position, Length, bitCount);
        }

        var byteCount = (bitCount + 7) / 8;
        if (destination.Length < byteCount)
        {
            throw new ArchiveReadException(ArchiveErrorCode.BufferTooSmall, nameof(CopyBitsTo), Position, Length, byteCount);
        }

        if (BitsRemaining < bitCount)
        {
            throw EndOfArchive(nameof(CopyBitsTo), Position, Length, bitCount);
        }

        destination[..byteCount].Clear();
        for (var i = 0; i < bitCount; i++)
        {
            if (ReadBit())
            {
                destination[i >> 3] |= (byte)(1 << (i & 7));
            }
        }
    }

    public override FBitArchive ReadSubArchive(int bitCount)
    {
        if (bitCount < 0)
        {
            throw new ArchiveReadException(ArchiveErrorCode.InvalidBitCount, nameof(ReadSubArchive), Position, Length, bitCount);
        }

        if (BitsRemaining < bitCount)
        {
            throw EndOfArchive(nameof(ReadSubArchive), Position, Length, bitCount);
        }

        var child = new BitArchiveReader(_buffer, _startBit + BitPosition, bitCount);
        BitPosition += bitCount;
        return child;
    }

    public override uint ReadIntPacked()
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

    public override void SeekBits(long position)
    {
        if (position < 0 || position > BitLength)
        {
            throw InvalidSeek(nameof(SeekBits), Position, Length, position);
        }

        BitPosition = position;
    }

    public override void SkipBits(long count)
    {
        if (count < 0)
        {
            throw InvalidCount(nameof(SkipBits), Position, Length, count);
        }

        SeekBits(BitPosition + count);
    }

    internal override void RestorePosition(long position) => BitPosition = position;

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _owner?.Dispose();
        }
    }
}
