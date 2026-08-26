using Replay.Encoding.Archives;
using static Replay.Encoding.PayloadEncryption.ValorantSeededTransformHelpers;

namespace Replay.Encoding.PayloadEncryption.VersionedTransforms;

public sealed class ValorantSeededTransform13_04 : IPayloadTransform
{
    private const uint SeedAddend = 0x076dc658u;
    private const uint InitAOffset = 0x28u;
    private const byte TailXor = 0x58;

    public IReadOnlyCollection<string> SupportedReplayVersions { get; } = ["++Ares-Core+release-13.04"];

    public int GetOutputByteCount(int bitCount) => ValorantSeededTransformHelpers.GetOutputByteCount(bitCount);

    public void Apply(FBitArchive input, uint seed, Span<byte> output)
    {
        var bitCount = CopyInputToOutput(input, output);
        Transform(output[..GetOutputByteCount(bitCount)], bitCount, seed);
    }

    public void Apply(FBitArchive input, int bitCount, uint seed, Span<byte> output)
    {
        CopyInputToOutput(input, bitCount, output);
        Transform(output[..GetOutputByteCount(bitCount)], bitCount, seed);
    }

    private static void Transform(Span<byte> output, int bitCount, uint seed)
    {
        if (bitCount == 0)
        {
            return;
        }

        var state = seed;
        var streamByte = (byte)seed;
        var prngA = InitialPrngA(seed);
        var prngB = InitialPrngB(seed);
        var byteOffset = 0;
        var bitsRemaining = bitCount;

        unchecked
        {
            while (bitsRemaining > 63)
            {
                var value = TransformUInt64(ReadUInt64(output, byteOffset), state);
                WriteUInt64(output, byteOffset, value);
                AdvanceTransformState(ref state, ref prngA, ref prngB, out streamByte);
                byteOffset += 8;
                bitsRemaining -= 64;
            }

            while (bitsRemaining > 31)
            {
                var value = TransformUInt32(ReadUInt32(output, byteOffset), state);
                WriteUInt32(output, byteOffset, value);
                AdvanceTransformState(ref state, ref prngA, ref prngB, out streamByte);
                byteOffset += 4;
                bitsRemaining -= 32;
            }

            while (bitsRemaining > 7)
            {
                output[byteOffset] = TransformByte(output[byteOffset], state);
                AdvanceTransformState(ref state, ref prngA, ref prngB, out streamByte);
                byteOffset++;
                bitsRemaining -= 8;
            }

            if (bitsRemaining != 0)
            {
                var mask = (byte)(0xff >> (7 - ((bitCount - 1) & 7)));
                output[byteOffset] ^= (byte)(mask & (streamByte ^ TailXor));
            }
        }
    }

    private static ulong TransformUInt64(ulong value, uint state)
    {
        unchecked
        {
            var ror1 = RotateRight(state, 1);
            var ror2 = RotateRight(state, 2);
            var ror3 = RotateRight(state, 3);
            var ror5 = RotateRight(state, 5);
            var ror6 = RotateRight(state, 6);
            var ror7 = RotateRight(state, 7);

            value = RotateRight(value, (int)(ror7 % 63) + 1);
            value ^= ~(ulong)ror6;
            value -= ror5;
            value = SwapAdjacentBits(value);
            value += ror3;
            value += ror2;
            return RotateLeft(value, (int)(ror1 % 63) + 1);
        }
    }

    private static uint TransformUInt32(uint value, uint state)
    {
        unchecked
        {
            var rol1 = RotateLeft(state, 1);
            var rol2 = RotateLeft(state, 2);
            var rol3 = RotateLeft(state, 3);
            var rol5 = RotateLeft(state, 5);
            var rol6 = RotateLeft(state, 6);
            var rol7 = RotateLeft(state, 7);

            value = RotateRight(value, (int)(rol7 % 31) + 1);
            value ^= rol6;
            value -= rol5;
            value = SwapAdjacentBits(value);
            value += rol3;
            value += rol2;
            return RotateLeft(value, (int)(rol1 % 31) + 1);
        }
    }

    private static byte TransformByte(byte value, uint state)
    {
        unchecked
        {
            var mixA = state * 0x0bu;
            var mixB = mixA * 0x0bu;
            var mixC = mixB * 0x0bu;
            var mixD = mixC * 0x79u;
            var mixE = mixD * 0x0bu;
            var mixF = mixE * 0x0bu;

            value = RotateRight(value, (int)(mixF % 7) + 1);
            value = (byte)((value ^ (byte)mixE) - (byte)mixD);
            value = SwapAdjacentBits(value);
            value = (byte)(value + (byte)mixB + (byte)mixC);
            return RotateLeft(value, (int)(mixA % 7) + 1);
        }
    }

    private static ulong InitialPrngA(uint seed)
    {
        unchecked
        {
            var seedPlus = seed + SeedAddend;
            var mixed = ((seedPlus >> 15) ^ seedPlus) >> 12 ^
                        ((seed - InitAOffset) * 0x02000000u) ^
                        seedPlus;
            return mixed * Multiplier;
        }
    }
}
