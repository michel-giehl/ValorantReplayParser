using Replay.Encoding.Archives;
using static Replay.Encoding.PayloadEncryption.ValorantSeededTransformHelpers;

namespace Replay.Encoding.PayloadEncryption.VersionedTransforms;

public sealed class ValorantSeededTransform13_05 : IPayloadTransform
{
    private const uint SeedAddend = 0x48c26613u;
    private const uint InitASeedAddend = 0x13u;
    private const byte TailXor = 0x13;

    public IReadOnlyCollection<string> SupportedReplayVersions { get; } = ["++Ares-Core+release-13.05"];

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
            var ror4 = RotateRight(state, 4);
            var ror5 = RotateRight(state, 5);
            var ror6 = RotateRight(state, 6);
            var ror7 = RotateRight(state, 7);
            var ror8 = RotateRight(state, 8);

            value = (value ^ ~(ulong)ror8) - ror7;
            value = RotateLeft(value, (int)(ror6 % 63) + 1);
            value = (value - ror5) ^ ~(ulong)ror4 ^ ~(ulong)ror3;
            value = RotateLeft(value, (int)(ror2 % 63) + 1);
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
            var rol4 = RotateLeft(state, 4);
            var rol5 = RotateLeft(state, 5);
            var rol6 = RotateLeft(state, 6);
            var rol7 = RotateLeft(state, 7);
            var rol8 = RotateLeft(state, 8);

            value = (value ^ rol8) - rol7;
            value = RotateLeft(value, (int)(rol6 % 31) + 1);
            value = (value - rol5) ^ rol4 ^ rol3;
            value = RotateLeft(value, (int)(rol2 % 31) + 1);
            return RotateLeft(value, (int)(rol1 % 31) + 1);
        }
    }

    private static byte TransformByte(byte value, uint state)
    {
        unchecked
        {
            var stateByte = (byte)state;
            var mixA = state * 0x1b0829u;

            value = (byte)(((byte)(mixA * 0x79u) ^ value) - (byte)(mixA * 0x0bu));
            value = RotateLeft(value, (int)(mixA % 7) + 1);
            value = (byte)((value - (byte)(stateByte * 0x1bu)) ^ (byte)(stateByte * 0x33u) ^
                           (byte)(stateByte * 0x31u));
            value = RotateLeft(value, (int)(state * 0x79u % 7) + 1);
            return RotateLeft(value, (int)(state * 0x0bu % 7) + 1);
        }
    }

    private static ulong InitialPrngA(uint seed)
    {
        unchecked
        {
            var seedPlus = seed + SeedAddend;
            var mixed = ((seedPlus >> 15) ^ seedPlus) >> 12 ^
                        ((seed + InitASeedAddend) * 0x02000000u) ^
                        seedPlus;
            return mixed * Multiplier;
        }
    }
}
