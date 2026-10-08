using Replay.Encoding.Archives;
using static Replay.Encoding.PayloadEncryption.ValorantSeededTransformHelpers;

namespace Replay.Encoding.PayloadEncryption.VersionedTransforms;

internal sealed class ValorantSeededTransformChina13_05 : IPayloadTransform
{
    private const uint SeedAddend = 0xf67761c9u;
    private const uint InitASeedOffset = 0xc9u;
    private const byte TailXor = 0xc9;

    public IReadOnlyCollection<string> SupportedReplayVersions { get; } =
        ["++Ares-Core+release-china-13.05"];

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
                WriteUInt64(output, byteOffset, TransformUInt64(ReadUInt64(output, byteOffset), state));
                AdvanceTransformState(ref state, ref prngA, ref prngB, out streamByte);
                byteOffset += 8;
                bitsRemaining -= 64;
            }

            while (bitsRemaining > 31)
            {
                WriteUInt32(output, byteOffset, TransformUInt32(ReadUInt32(output, byteOffset), state));
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
            var ror4 = RotateRight(state, 4);
            var ror6 = RotateRight(state, 6);
            var ror7 = RotateRight(state, 7);
            var ror8 = RotateRight(state, 8);
            value += ror8;
            value = RotateLeft(value, (int)(ror6 % 63) + 1);
            value = RotateRight(value, (int)(ror7 % 63) + 1);
            value = RotateLeft(value, (int)(ror4 % 63) + 1);
            value = RotateRight(value, (int)(ror2 % 63) + 1);
            return value - ror1;
        }
    }

    private static uint TransformUInt32(uint value, uint state)
    {
        unchecked
        {
            var rol1 = RotateLeft(state, 1);
            var rol2 = RotateLeft(state, 2);
            var rol4 = RotateLeft(state, 4);
            var rol6 = RotateLeft(state, 6);
            var rol7 = RotateLeft(state, 7);
            var rol8 = RotateLeft(state, 8);
            value += rol8;
            value = RotateLeft(value, (int)(rol6 % 31) + 1);
            value = RotateRight(value, (int)(rol7 % 31) + 1);
            value = RotateLeft(value, (int)(rol4 % 31) + 1);
            value = RotateRight(value, (int)(rol2 % 31) + 1);
            return value - rol1;
        }
    }

    private static byte TransformByte(byte value, uint state)
    {
        unchecked
        {
            var m1 = state * 0x0000000bu;
            var m2 = state * 0x00000079u;
            var m4 = state * 0x00003931u;
            var m6 = state * 0x001b0829u;
            var m7 = state * 0x012959c3u;
            var m8 = state * 0x0cc6db61u;
            value += (byte)m8;
            value = RotateLeft(value, (int)(m6 % 7) + 1);
            value = RotateRight(value, (int)(m7 % 7) + 1);
            value = RotateLeft(value, (int)(m4 % 7) + 1);
            value = RotateRight(value, (int)(m2 % 7) + 1);
            return (byte)(value - (byte)m1);
        }
    }

    private static ulong InitialPrngA(uint seed)
    {
        unchecked
        {
            var seedPlus = seed + SeedAddend;
            var mixed = ((seedPlus >> 15) ^ seedPlus) >> 12 ^
                        ((seed + InitASeedOffset) * 0x02000000u) ^
                        seedPlus;
            return mixed * Multiplier;
        }
    }
}
