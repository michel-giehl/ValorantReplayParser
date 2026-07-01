using System.Buffers;
using System.Buffers.Binary;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace Replay.Encoding.Archives;

public sealed class BitArchiveReader : FBitArchive
{
    private const int MaxBufferedBits = 56;

    private readonly IMemoryOwner<byte>? _owner;

    private readonly byte[] _source;
    private readonly int _sourceOffset;
    private readonly int _sourceEnd;

    private readonly ReadOnlyMemory<byte> _memory;

    private readonly long _startBit;
    private readonly long _bitLength;

    private long _bitPosition;

    // Absolute index into _source, not relative to _sourceOffset.
    private int _byteIndex;

    // LSB-first bit buffer.
    private ulong _bitBuffer;
    private int _bitsInBuffer;

    public BitArchiveReader(ReadOnlyMemory<byte> input)
        : this(input, 0, input.Length * 8L)
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

    private BitArchiveReader(
        IMemoryOwner<byte>? owner,
        ReadOnlyMemory<byte> input,
        long startBit,
        long bitLength)
    {
        int sourceLength;
        var maxBits = input.Length * 8L;

        if (startBit < 0 || bitLength < 0 || startBit > maxBits || bitLength > maxBits - startBit)
        {
            throw new ArchiveReadException(
                ArchiveErrorCode.InvalidBitCount,
                nameof(BitArchiveReader),
                0,
                maxBits,
                bitLength);
        }

        _owner = owner;
        _startBit = startBit;
        _bitLength = bitLength;

        if (MemoryMarshal.TryGetArray(input, out ArraySegment<byte> segment) && segment.Array is not null)
        {
            _source = segment.Array;
            _sourceOffset = segment.Offset;
            sourceLength = input.Length;
            _memory = input;
        }
        else
        {
            _source = input.ToArray();
            _sourceOffset = 0;
            sourceLength = _source.Length;
            _memory = _source;
        }

        _sourceEnd = _sourceOffset + sourceLength;

        ResetStateToPosition(0);
    }

    public override long BitPosition
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _bitPosition;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        protected set => ResetStateToPosition(value);
    }

    public override long BitLength
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _bitLength;
    }

    private long RemainingBits
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _bitLength - _bitPosition;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public override bool ReadBit()
    {
        if (_bitPosition >= _bitLength)
        {
            throw EndOfArchive(nameof(ReadBit), Position, Length, 1);
        }

        return ReadBitUnchecked();
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public override bool TryReadBit(out bool value)
    {
        if (_bitPosition >= _bitLength)
        {
            value = false;
            return false;
        }

        value = ReadBitUnchecked();
        return true;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private bool ReadBitUnchecked()
    {
        if (_bitsInBuffer == 0)
        {
            FillBuffer(1);
        }

        var value = (_bitBuffer & 1UL) != 0;

        _bitBuffer >>= 1;
        _bitsInBuffer--;
        _bitPosition++;

        return value;
    }

    public override ulong ReadBitsToUInt64(int bitCount)
    {
        if ((uint)bitCount > 64)
        {
            throw new ArchiveReadException(
                ArchiveErrorCode.InvalidBitCount,
                nameof(ReadBitsToUInt64),
                Position,
                Length,
                bitCount);
        }

        if (RemainingBits < bitCount)
        {
            throw EndOfArchive(nameof(ReadBitsToUInt64), Position, Length, bitCount);
        }

        return ReadBitsToUInt64Unchecked(bitCount);
    }

    public override bool TryReadBitsToUInt64(int bitCount, out ulong value)
    {
        if ((uint)bitCount > 64 || RemainingBits < bitCount)
        {
            value = 0;
            return false;
        }

        value = ReadBitsToUInt64Unchecked(bitCount);
        return true;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private ulong ReadBitsToUInt64Unchecked(int bitCount)
    {
        if (bitCount == 0)
        {
            return 0;
        }

        // Ultra-fast byte-aligned path: no buffering, no masking loop.
        if (_bitsInBuffer == 0 && (bitCount & 7) == 0)
        {
            var byteCount = bitCount >> 3;

            if (_byteIndex + byteCount <= _sourceEnd)
            {
                ref var src = ref MemoryMarshal.GetArrayDataReference(_source);

                ulong value = byteCount == 8
                    ? ReadUInt64LittleEndian(ref Unsafe.Add(ref src, _byteIndex))
                    : ReadUpToSevenLittleEndian(ref Unsafe.Add(ref src, _byteIndex), byteCount);

                _byteIndex += byteCount;
                _bitPosition += bitCount;

                return value;
            }
        }

        if (bitCount <= MaxBufferedBits)
        {
            FillBuffer(bitCount);

            var value = _bitBuffer & MaskLow(bitCount);
            ConsumeBufferedBits(bitCount);

            return value;
        }

        if (_bitsInBuffer >= bitCount)
        {
            var value = _bitBuffer & MaskLow(bitCount);
            ConsumeBufferedBits(bitCount);

            return value;
        }

        FillBuffer(MaxBufferedBits);

        var result = _bitBuffer & MaskLow(MaxBufferedBits);
        ConsumeBufferedBits(MaxBufferedBits);

        var rest = bitCount - MaxBufferedBits;

        FillBuffer(rest);

        result |= (_bitBuffer & MaskLow(rest)) << MaxBufferedBits;
        ConsumeBufferedBits(rest);

        return result;
    }

    public override ReadOnlyMemory<byte> ReadBits(int bitCount)
    {
        if (bitCount < 0)
        {
            throw new ArchiveReadException(
                ArchiveErrorCode.InvalidBitCount,
                nameof(ReadBits),
                Position,
                Length,
                bitCount);
        }

        if (RemainingBits < bitCount)
        {
            throw EndOfArchive(nameof(ReadBits), Position, Length, bitCount);
        }

        var byteCount = ByteCountForBits(bitCount);
        var output = new byte[byteCount];

        CopyBitsTo(output, bitCount);

        return output;
    }

    public override bool TryReadBits(int bitCount, out ReadOnlyMemory<byte> value)
    {
        if (bitCount < 0 || RemainingBits < bitCount)
        {
            value = default;
            return false;
        }

        var byteCount = ByteCountForBits(bitCount);
        var output = new byte[byteCount];

        CopyBitsTo(output, bitCount);

        value = output;
        return true;
    }

    public override void CopyBitsTo(Span<byte> destination, int bitCount)
    {
        if (bitCount < 0)
        {
            throw new ArchiveReadException(
                ArchiveErrorCode.InvalidBitCount,
                nameof(CopyBitsTo),
                Position,
                Length,
                bitCount);
        }

        var byteCount = ByteCountForBits(bitCount);

        if (destination.Length < byteCount)
        {
            throw new ArchiveReadException(
                ArchiveErrorCode.BufferTooSmall,
                nameof(CopyBitsTo),
                Position,
                Length,
                byteCount);
        }

        if (RemainingBits < bitCount)
        {
            throw EndOfArchive(nameof(CopyBitsTo), Position, Length, bitCount);
        }

        if (bitCount == 0)
        {
            return;
        }

        var output = destination[..byteCount];

        if (bitCount <= 64)
        {
            var value = ReadBitsToUInt64Unchecked(bitCount);

            WriteUInt64TruncatedLittleEndian(output, value, byteCount);

            var tailBits = bitCount & 7;
            if (tailBits != 0)
            {
                output[byteCount - 1] &= (byte)((1 << tailBits) - 1);
            }

            return;
        }

        CopyBitsLarge(output, bitCount);
    }

    private void CopyBitsLarge(Span<byte> destination, int bitCount)
    {
        var absoluteBit = _startBit + _bitPosition;
        var sourceByteIndex = (int)(absoluteBit >> 3);
        var sourceBitOffset = (int)(absoluteBit & 7);

        var byteCount = destination.Length;
        var tailBits = bitCount & 7;
        var sourceBase = _sourceOffset + sourceByteIndex;

        if (sourceBitOffset == 0)
        {
            var fullBytes = bitCount >> 3;

            if (fullBytes != 0)
            {
                _source.AsSpan(sourceBase, fullBytes).CopyTo(destination);
            }

            if (tailBits != 0)
            {
                destination[fullBytes] =
                    (byte)(_source[sourceBase + fullBytes] & ((1 << tailBits) - 1));
            }

            ResetStateToPosition(_bitPosition + bitCount);
            return;
        }

        ref var src = ref MemoryMarshal.GetArrayDataReference(_source);
        ref var dst = ref MemoryMarshal.GetReference(destination);

        var shift = sourceBitOffset;
        var inverseShift = 8 - shift;

        var availableSourceBytes = _sourceEnd - sourceBase;
        var fastBytes = byteCount & ~7;

        var maxFastBytes = availableSourceBytes > 1
            ? (availableSourceBytes - 1) & ~7
            : 0;

        if (fastBytes > maxFastBytes)
        {
            fastBytes = maxFastBytes;
        }

        var i = 0;

        // 8 output bytes per iteration.
        // Needs 9 source bytes because the source bit offset is non-zero.
        for (; i < fastBytes; i += 8)
        {
            var low = ReadUInt64LittleEndian(ref Unsafe.Add(ref src, sourceBase + i));
            var high = (ulong)Unsafe.Add(ref src, sourceBase + i + 8);

            var value = (low >> shift) | (high << (64 - shift));

            WriteUInt64LittleEndian(ref Unsafe.Add(ref dst, i), value);
        }

        // Tail path. Bounds-safe for archives ending mid-byte.
        for (; i < byteCount; i++)
        {
            var value = Unsafe.Add(ref src, sourceBase + i) >> shift;
            var nextIndex = sourceBase + i + 1;

            if (nextIndex < _sourceEnd)
            {
                value |= Unsafe.Add(ref src, nextIndex) << inverseShift;
            }

            Unsafe.Add(ref dst, i) = (byte)value;
        }

        if (tailBits != 0)
        {
            destination[byteCount - 1] &= (byte)((1 << tailBits) - 1);
        }

        ResetStateToPosition(_bitPosition + bitCount);
    }

    public override FBitArchive ReadSubArchive(int bitCount)
    {
        if (bitCount < 0)
        {
            throw new ArchiveReadException(
                ArchiveErrorCode.InvalidBitCount,
                nameof(ReadSubArchive),
                Position,
                Length,
                bitCount);
        }

        if (RemainingBits < bitCount)
        {
            throw EndOfArchive(nameof(ReadSubArchive), Position, Length, bitCount);
        }

        var child = new BitArchiveReader(_memory, _startBit + _bitPosition, bitCount);

        ResetStateToPosition(_bitPosition + bitCount);

        return child;
    }

    public override void SeekBits(long position)
    {
        if (position < 0 || position > _bitLength)
        {
            throw InvalidSeek(nameof(SeekBits), Position, Length, position);
        }

        var delta = position - _bitPosition;

        if ((ulong)delta <= (uint)_bitsInBuffer)
        {
            ConsumeBufferedBits((int)delta);
            return;
        }

        ResetStateToPosition(position);
    }

    public override void SkipBits(long count)
    {
        if (count < 0)
        {
            throw InvalidCount(nameof(SkipBits), Position, Length, count);
        }

        if (count > _bitLength - _bitPosition)
        {
            throw InvalidSeek(nameof(SkipBits), Position, Length, _bitPosition + count);
        }

        if ((ulong)count <= (uint)_bitsInBuffer)
        {
            ConsumeBufferedBits((int)count);
            return;
        }

        ResetStateToPosition(_bitPosition + count);
    }

    protected internal override void RestorePosition(long position)
    {
        ResetStateToPosition(position);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _owner?.Dispose();
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void FillBuffer(int wantedBits)
    {
        if (_bitsInBuffer >= wantedBits)
        {
            return;
        }

        ref var src = ref MemoryMarshal.GetArrayDataReference(_source);

        // Dirty fast path: pull 64 bits at once when the buffer is empty.
        // This makes ReadBit loops massively cheaper because one refill feeds 64 calls.
        if (_bitsInBuffer == 0 && _byteIndex + 8 <= _sourceEnd)
        {
            _bitBuffer = ReadUInt64LittleEndian(ref Unsafe.Add(ref src, _byteIndex));
            _byteIndex += 8;
            _bitsInBuffer = 64;
            return;
        }

        var neededBytes = (wantedBits - _bitsInBuffer + 7) >> 3;

        var chunk = neededBytes == 8
            ? ReadUInt64LittleEndian(ref Unsafe.Add(ref src, _byteIndex))
            : ReadUpToSevenLittleEndian(ref Unsafe.Add(ref src, _byteIndex), neededBytes);

        _bitBuffer |= chunk << _bitsInBuffer;
        _byteIndex += neededBytes;
        _bitsInBuffer += neededBytes << 3;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void ConsumeBufferedBits(int bitCount)
    {
        switch (bitCount)
        {
            case 0:
                return;
            case 64:
                _bitBuffer = 0;
                _bitsInBuffer = 0;
                _bitPosition += 64;
                return;
        }

        _bitBuffer >>= bitCount;
        _bitsInBuffer -= bitCount;
        _bitPosition += bitCount;
    }

    private void ResetStateToPosition(long bitPosition)
    {
        _bitPosition = bitPosition;
        _bitBuffer = 0;
        _bitsInBuffer = 0;

        var absoluteBit = _startBit + bitPosition;

        _byteIndex = _sourceOffset + (int)(absoluteBit >> 3);

        var bitOffset = (int)(absoluteBit & 7);
        if (bitOffset == 0 || _byteIndex >= _sourceEnd)
        {
            return;
        }

        _bitBuffer = (ulong)(_source[_byteIndex] >> bitOffset);
        _bitsInBuffer = 8 - bitOffset;
        _byteIndex++;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static int ByteCountForBits(int bitCount)
    {
        return (bitCount >> 3) + ((bitCount & 7) == 0 ? 0 : 1);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static ulong MaskLow(int bitCount)
    {
        return bitCount == 64
            ? ulong.MaxValue
            : (1UL << bitCount) - 1UL;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static ulong ReadUInt64LittleEndian(ref byte source)
    {
        var value = Unsafe.ReadUnaligned<ulong>(ref source);

        return BitConverter.IsLittleEndian
            ? value
            : BinaryPrimitives.ReverseEndianness(value);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void WriteUInt64LittleEndian(ref byte destination, ulong value)
    {
        if (!BitConverter.IsLittleEndian)
        {
            value = BinaryPrimitives.ReverseEndianness(value);
        }

        Unsafe.WriteUnaligned(ref destination, value);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void WriteUInt32LittleEndian(ref byte destination, uint value)
    {
        if (!BitConverter.IsLittleEndian)
        {
            value = BinaryPrimitives.ReverseEndianness(value);
        }

        Unsafe.WriteUnaligned(ref destination, value);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void WriteUInt16LittleEndian(ref byte destination, ushort value)
    {
        if (!BitConverter.IsLittleEndian)
        {
            value = BinaryPrimitives.ReverseEndianness(value);
        }

        Unsafe.WriteUnaligned(ref destination, value);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static ulong ReadUpToSevenLittleEndian(ref byte source, int byteCount)
    {
        return byteCount switch
        {
            1 => source,

            2 => source
               | ((ulong)Unsafe.Add(ref source, 1) << 8),

            3 => source
               | ((ulong)Unsafe.Add(ref source, 1) << 8)
               | ((ulong)Unsafe.Add(ref source, 2) << 16),

            4 => source
               | ((ulong)Unsafe.Add(ref source, 1) << 8)
               | ((ulong)Unsafe.Add(ref source, 2) << 16)
               | ((ulong)Unsafe.Add(ref source, 3) << 24),

            5 => source
               | ((ulong)Unsafe.Add(ref source, 1) << 8)
               | ((ulong)Unsafe.Add(ref source, 2) << 16)
               | ((ulong)Unsafe.Add(ref source, 3) << 24)
               | ((ulong)Unsafe.Add(ref source, 4) << 32),

            6 => source
               | ((ulong)Unsafe.Add(ref source, 1) << 8)
               | ((ulong)Unsafe.Add(ref source, 2) << 16)
               | ((ulong)Unsafe.Add(ref source, 3) << 24)
               | ((ulong)Unsafe.Add(ref source, 4) << 32)
               | ((ulong)Unsafe.Add(ref source, 5) << 40),

            7 => source
               | ((ulong)Unsafe.Add(ref source, 1) << 8)
               | ((ulong)Unsafe.Add(ref source, 2) << 16)
               | ((ulong)Unsafe.Add(ref source, 3) << 24)
               | ((ulong)Unsafe.Add(ref source, 4) << 32)
               | ((ulong)Unsafe.Add(ref source, 5) << 40)
               | ((ulong)Unsafe.Add(ref source, 6) << 48),

            _ => 0
        };
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void WriteUInt64TruncatedLittleEndian(Span<byte> destination, ulong value, int byteCount)
    {
        ref var dst = ref MemoryMarshal.GetReference(destination);

        switch (byteCount)
        {
            case 1:
                dst = (byte)value;
                return;

            case 2:
                WriteUInt16LittleEndian(ref dst, (ushort)value);
                return;

            case 3:
                WriteUInt16LittleEndian(ref dst, (ushort)value);
                Unsafe.Add(ref dst, 2) = (byte)(value >> 16);
                return;

            case 4:
                WriteUInt32LittleEndian(ref dst, (uint)value);
                return;

            case 5:
                WriteUInt32LittleEndian(ref dst, (uint)value);
                Unsafe.Add(ref dst, 4) = (byte)(value >> 32);
                return;

            case 6:
                WriteUInt32LittleEndian(ref dst, (uint)value);
                WriteUInt16LittleEndian(ref Unsafe.Add(ref dst, 4), (ushort)(value >> 32));
                return;

            case 7:
                WriteUInt32LittleEndian(ref dst, (uint)value);
                WriteUInt16LittleEndian(ref Unsafe.Add(ref dst, 4), (ushort)(value >> 32));
                Unsafe.Add(ref dst, 6) = (byte)(value >> 48);
                return;

            case 8:
                WriteUInt64LittleEndian(ref dst, value);
                return;
        }
    }
}