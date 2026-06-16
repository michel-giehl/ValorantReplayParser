using Replay.Encoding.Archives;
using static Replay.Encoding.PayloadEncryption.ValorantSeededTransformHelpers;

namespace Replay.Encoding.PayloadEncryption.VersionedTransforms;

public sealed class ValorantSeededTransform12_11 : IPayloadTransform
{
    private const uint SeedAddend = 0x409d36a3u;
    private const uint InitAOffset = 0x23u;
    private const byte TailXor = 0xa3;

    public IReadOnlyCollection<string> SupportedReplayVersions { get; } = ["++Ares+Release-12.11"];

    public int GetOutputByteCount(int bitCount) => ValorantSeededTransformHelpers.GetOutputByteCount(bitCount);

    public void Apply(FBitArchive input, uint seed, Span<byte> output)
    {
        var bitCount = CopyInputToOutput(input, output);
        Transform(output[..GetOutputByteCount(bitCount)], bitCount, seed);
    }

    private static void Transform(Span<byte> output, int bitCount, uint seed)
    {
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
                var value = ReadUInt64(output, byteOffset);
                var ror2 = RotateRight(state, 2);
                var ror3 = RotateRight(state, 3);
                var ror4 = RotateRight(state, 4);
                var ror6 = RotateRight(state, 6);
                var ror8 = RotateRight(state, 8);

                value = RotateRight(value, (int)(ror8 % 63) + 1);
                value = SwapAdjacentBits(value);
                value += ror6;
                value = ReverseBits64WithoutFinal16BitSwap(value);
                value -= ror4;
                value -= ror3;
                value -= ror2;
                value = SwapAdjacentBits(value);

                WriteUInt64(output, byteOffset, value);
                AdvanceTransformState(ref state, ref prngA, ref prngB, out streamByte);
                byteOffset += 8;
                bitsRemaining -= 64;
            }

            while (bitsRemaining > 31)
            {
                var value = ReadUInt32(output, byteOffset);
                var rol2 = RotateLeft(state, 2);
                var rol3 = RotateLeft(state, 3);
                var rol4 = RotateLeft(state, 4);
                var rol6 = RotateLeft(state, 6);
                var rol8 = RotateLeft(state, 8);

                value = RotateRight(value, (int)(rol8 % 31) + 1);
                value = SwapAdjacentBits(value);
                value += rol6;
                value = ReverseBits32(value);
                value -= rol4;
                value -= rol3;
                value -= rol2;
                value = SwapAdjacentBits(value);

                WriteUInt32(output, byteOffset, value);
                AdvanceTransformState(ref state, ref prngA, ref prngB, out streamByte);
                byteOffset += 4;
                bitsRemaining -= 32;
            }

            while (bitsRemaining > 7)
            {
                var value = output[byteOffset];
                var stateByte = (byte)state;
                var rotate2Input = state * 0x0cc6db61u;

                value = RotateRight(value, (int)(rotate2Input % 7) + 1);
                value = SwapAdjacentBits(value);
                value += (byte)(stateByte * 0x29);
                value = ReverseBits8(value);
                value += (byte)(stateByte * 0x23);
                value = SwapAdjacentBits(value);

                output[byteOffset] = value;
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

    private static ulong InitialPrngA(uint seed)
    {
        unchecked
        {
            var seedPlus = seed + SeedAddend;
            var mixed = ((seedPlus >> 15) ^ seedPlus) >> 12 ^ ((seed + InitAOffset) * 0x02000000u) ^ seedPlus;
            return mixed * Multiplier;
        }
    }
}