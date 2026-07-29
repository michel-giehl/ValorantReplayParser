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

    internal static ulong RotateLeft(ulong value, int count) => BitOperations.RotateLeft(value, count);

    internal static uint RotateLeft(uint value, int count) => BitOperations.RotateLeft(value, count);

    internal static byte RotateLeft(byte value, int count) =>
        (byte)((value << count) | (value >> (8 - count)));

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

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static ulong SubstituteBytes(ulong value, byte[] table)
    {
        return table[(byte)value] |
               ((ulong)table[(byte)(value >> 8)] << 8) |
               ((ulong)table[(byte)(value >> 16)] << 16) |
               ((ulong)table[(byte)(value >> 24)] << 24) |
               ((ulong)table[(byte)(value >> 32)] << 32) |
               ((ulong)table[(byte)(value >> 40)] << 40) |
               ((ulong)table[(byte)(value >> 48)] << 48) |
               ((ulong)table[(byte)(value >> 56)] << 56);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static uint SubstituteBytes(uint value, byte[] table)
    {
        return table[(byte)value] |
               ((uint)table[(byte)(value >> 8)] << 8) |
               ((uint)table[(byte)(value >> 16)] << 16) |
               ((uint)table[(byte)(value >> 24)] << 24);
    }

    internal static readonly byte[] SubstituteTable32 = Convert.FromHexString(
        "2167b396313fbad3d5062b16f1b651a79c7b419584251536a4703546b05fa6c3" +
        "bb8638f62ea2a994831b6239f3d228149e9af2c9decc26a1d8d0748d69127189" +
        "f758cd4db7114809b968c77cf42042f56b54756da81d6a07d7c50ea066db" +
        "f899ad1004ff8fb1ef986c29e201183d371e654b4a6e24d9bd90fe135693" +
        "34aa8b0d79e74992f98eca43cbc6da022d8c0fb2c08a4785aee0d477c40b" +
        "5c617e335745e62ffd6f915b9fcf3c4fe33aede480087372ea63fbfcb8" +
        "7a23a51f815952875dfa78c1b5beb4a3641c3253f07fdc3b7640ec309755" +
        "4c00bc880c05e1df197d22c25a9be52a50bf1ac8035e2cd1abdd44ee82" +
        "ce27afebd64e0ae9173e9de8ac60");

    internal static readonly byte[] SubstituteTable64 = Convert.FromHexString(
        "77b9042feb7d27c944739a3f36f565ddf7e0302da9985dde69a394a05e170678" +
        "a4f6ab0343c828e56a8e1cf270cf5305d30dffa7a23a32255a1f48c1" +
        "b7e16e85996047bbe48acbc01bea6164f0c2d88bcdfdadb819b5bf0e9181" +
        "839d45d249e9c731bd20bec66680d179d7e6fca15b5fdff1d0506752fe" +
        "7b3513f846b3758de33e2ef4dc342a0823e20c094beec30f248f544c" +
        "5539cc1d1e3b2272da296b41aaa6122c93ca9c970a56a87a9eb462923" +
        "d9f38f3408437b2d4af7633fa21effb716f9082511ac574f95907ba11" +
        "b1acd6ede702ae9610167c4f881426bc1501684a2b0b7fa54ee86dec4d" +
        "b05cc4009558b6d57e42db5718866cced99b89873c8c63");

    internal static readonly byte[] SubstituteTable8 = Convert.FromHexString(
        "0a6c6996cadc5a08b38339a0f9adf4560e6e4c85649982d4885c8736239a" +
        "112db8c4341866136f59e07422faa665e2d7954e94b0779e1aeee705a" +
        "2c830900d9bd219c93a471512a9291f53acaf4352aef54dbfbee34a06" +
        "d5d0a378a7d61c7a6b81d8dee568fb267ebcbae8cce4727f2cfcf0" +
        "ec28716048ef3e038f1ef16a8df2461b9c86f7b476628a10fd6d0b" +
        "3f9f2f555fc3c6921627d344840fe1808cb7738945db332550ea0414" +
        "c50c32415e79a41d3d5b4037c1cffe2b54eb9d4991f307173cda57" +
        "8bcd61f6ce702eff2193972a7d67abb57c5d0042a5d92051eddd0209" +
        "c2d1f8bdbbe93524985838aab9a8b27501cbc063df3b8ec731b1a1" +
        "b6e67b4b4f");
}
