using System.Buffers.Binary;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Replay.Encoding.Archives;

namespace Replay.Encoding.PayloadEncryption;

internal static class ValorantSeededTransformHelpers
{
    internal const ulong Multiplier = 0x2545f4914f6cdd1dUL;

    internal static int GetOutputByteCount(int bitCount)
    {
        if (bitCount < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(bitCount), bitCount, "Bit count cannot be negative.");
        }

        return checked((bitCount + 7) / 8);
    }

    internal static int CopyInputToOutput(FBitArchive input, Span<byte> output)
    {
        if (input.BitsRemaining > int.MaxValue)
        {
            throw new ArchiveReadException(ArchiveErrorCode.InvalidBitCount, nameof(CopyInputToOutput), input.Position,
                input.Length, input.BitsRemaining);
        }

        return CopyInputToOutput(input, (int)input.BitsRemaining, output);
    }

    internal static int CopyInputToOutput(FBitArchive input, int bitCount, Span<byte> output)
    {
        if (bitCount < 0)
        {
            throw new ArchiveReadException(ArchiveErrorCode.InvalidBitCount, nameof(CopyInputToOutput), input.Position,
                input.Length, bitCount);
        }

        var byteCount = GetOutputByteCount(bitCount);
        if (output.Length < byteCount)
        {
            throw new ArchiveReadException(ArchiveErrorCode.BufferTooSmall, nameof(CopyInputToOutput), input.Position,
                input.Length, byteCount);
        }

        input.CopyBitsTo(output[..byteCount], bitCount);
        return bitCount;
    }

    internal static void AdvanceTransformState(ref uint state, ref ulong prngA, ref ulong prngB, out byte streamByte)
    {
        unchecked
        {
            var sum = prngB + prngA;
            prngB ^= prngA;
            prngA = RotateRight(prngA, 9) ^ (prngB << 14) ^ prngB;
            prngB = RotateLeft(prngB, 36);
            state = (uint)(sum >> 32);
            streamByte = (byte)state;
        }
    }

    internal static ulong InitialPrngB(uint seed)
    {
        unchecked
        {
            var mixed = ((seed >> 15) ^ seed) >> 12 ^ (seed << 25) ^ seed;
            return mixed * Multiplier;
        }
    }

    internal static ulong SwapAdjacentBits(ulong value) =>
        ((value & 0x5555555555555555UL) << 1) | ((value >> 1) & 0x5555555555555555UL);

    internal static uint SwapAdjacentBits(uint value) =>
        ((value & 0x55555555u) << 1) | ((value >> 1) & 0x55555555u);

    internal static byte SwapAdjacentBits(byte value) =>
        (byte)(((value & 0x55) << 1) | ((value >> 1) & 0x55));

    internal static ulong ReverseBits64WithoutFinal16BitSwap(ulong value)
    {
        value = ((value & 0x5555555555555555UL) << 1) | ((value >> 1) & 0x5555555555555555UL);
        value = ((value & 0x3333333333333333UL) << 2) | ((value >> 2) & 0x3333333333333333UL);
        value = ((value & 0x0F0F0F0F0F0F0F0FUL) << 4) | ((value >> 4) & 0x0F0F0F0F0F0F0F0FUL);
        value = ((value & 0x00FF00FF00FF00FFUL) << 8) | ((value >> 8) & 0x00FF00FF00FF00FFUL);
        return (value << 32) | (value >> 32);
    }

    internal static uint ReverseBits32(uint value)
    {
        value = ((value & 0x55555555u) << 1) | ((value >> 1) & 0x55555555u);
        value = ((value & 0x33333333u) << 2) | ((value >> 2) & 0x33333333u);
        value = ((value & 0x0F0F0F0Fu) << 4) | ((value >> 4) & 0x0F0F0F0Fu);
        value = ((value & 0x00FF00FFu) << 8) | ((value >> 8) & 0x00FF00FFu);
        return (value << 16) | (value >> 16);
    }

    internal static byte ReverseBits8(byte value)
    {
        value = (byte)(((value & 0x55) << 1) | ((value >> 1) & 0x55));
        value = (byte)(((value & 0x33) << 2) | ((value >> 2) & 0x33));
        return (byte)(((value & 0x0F) << 4) | ((value >> 4) & 0x0F));
    }

    private static ulong RotateLeft(ulong value, int count) => BitOperations.RotateLeft(value, count);

    internal static uint RotateLeft(uint value, int count) => BitOperations.RotateLeft(value, count);

    internal static ulong RotateRight(ulong value, int count) => BitOperations.RotateRight(value, count);

    internal static uint RotateRight(uint value, int count) => BitOperations.RotateRight(value, count);

    internal static byte RotateRight(byte value, int count) =>
        (byte)((value >> count) | (value << (8 - count)));

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static ulong ReadUInt64(ReadOnlySpan<byte> output, int byteOffset)
    {
        ref var source = ref MemoryMarshal.GetReference(output);
        return ReadUInt64LittleEndian(ref Unsafe.Add(ref source, byteOffset));
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static uint ReadUInt32(ReadOnlySpan<byte> output, int byteOffset)
    {
        ref var source = ref MemoryMarshal.GetReference(output);
        return ReadUInt32LittleEndian(ref Unsafe.Add(ref source, byteOffset));
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void WriteUInt64(Span<byte> output, int byteOffset, ulong value)
    {
        ref var destination = ref MemoryMarshal.GetReference(output);
        WriteUInt64LittleEndian(ref Unsafe.Add(ref destination, byteOffset), value);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void WriteUInt32(Span<byte> output, int byteOffset, uint value)
    {
        ref var destination = ref MemoryMarshal.GetReference(output);
        WriteUInt32LittleEndian(ref Unsafe.Add(ref destination, byteOffset), value);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static ulong ReadUInt64LittleEndian(ref byte source)
    {
        var value = Unsafe.ReadUnaligned<ulong>(ref source);
        return BitConverter.IsLittleEndian ? value : BinaryPrimitives.ReverseEndianness(value);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static uint ReadUInt32LittleEndian(ref byte source)
    {
        var value = Unsafe.ReadUnaligned<uint>(ref source);
        return BitConverter.IsLittleEndian ? value : BinaryPrimitives.ReverseEndianness(value);
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
}
