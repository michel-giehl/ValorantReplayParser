using Replay.Encoding.Archives;
using static Replay.Encoding.PayloadEncryption.ValorantSeededTransformHelpers;

namespace Replay.Encoding.PayloadEncryption.VersionedTransforms;

public sealed class ValorantSeededTransform13_02 : IPayloadTransform
{
    private const uint SeedAddend = 0x9e81a37cu;
    private const uint InitAOffset = 0x04u;
    private const byte TailXor = 0x7c;

    public IReadOnlyCollection<string> SupportedReplayVersions { get; } = ["++Ares-Core+release-13.02"];

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
                var value = SubstituteBytes(ReadUInt64(output, byteOffset), SubstituteTable64);
                var ror2 = RotateRight(state, 2);
                var ror3 = RotateRight(state, 3);
                var ror6 = RotateRight(state, 6);

                value = ReverseBits64WithoutFinal16BitSwap(value);
                value = ~(value - ror6);
                value = ReverseBits64WithoutFinal16BitSwap(value);
                value = RotateLeft(value, (int)(ror3 % 63) + 1);
                value = RotateRight(value, (int)(ror2 % 63) + 1);

                WriteUInt64(output, byteOffset, value);
                AdvanceTransformState(ref state, ref prngA, ref prngB, out streamByte);
                byteOffset += 8;
                bitsRemaining -= 64;
            }

            while (bitsRemaining > 31)
            {
                var value = SubstituteBytes(ReadUInt32(output, byteOffset), SubstituteTable32);
                var rol2 = RotateLeft(state, 2);
                var rol3 = RotateLeft(state, 3);
                var rol6 = RotateLeft(state, 6);

                value = ReverseBits32(value);
                value = ~(value - rol6);
                value = ReverseBits32(value);
                value = RotateLeft(value, (int)(rol3 % 31) + 1);
                value = RotateRight(value, (int)(rol2 % 31) + 1);

                WriteUInt32(output, byteOffset, value);
                AdvanceTransformState(ref state, ref prngA, ref prngB, out streamByte);
                byteOffset += 4;
                bitsRemaining -= 32;
            }

            while (bitsRemaining > 7)
            {
                var mixA = state * 0x79u;
                var mixB = mixA * 0x0bu;
                var value = SubstituteTable8[output[byteOffset]];

                value = ReverseBits8(value);
                value = (byte)~(value - (byte)(mixB * 0x33u));
                value = ReverseBits8(value);
                value = RotateLeft(value, (int)(mixB % 7) + 1);
                value = RotateRight(value, (int)(mixA % 7) + 1);

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
            var mixed = ((seedPlus >> 15) ^ seedPlus) >> 12 ^ ((seed - InitAOffset) * 0x02000000u) ^ seedPlus;
            return mixed * Multiplier;
        }
    }
}
