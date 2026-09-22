using Replay.Encoding.Archives;
using static Replay.Encoding.PayloadEncryption.ValorantSeededTransformHelpers;

namespace Replay.Encoding.PayloadEncryption.VersionedTransforms;

public sealed class ValorantSeededTransform13_06 : IPayloadTransform
{
    private const uint SeedAddend = 0xe974593cu;
    private const uint InitASeedAddend = 0x3cu;
    private const byte TailXor = 0x3c;

    public IReadOnlyCollection<string> SupportedReplayVersions { get; } = ["++Ares-Core+release-13.06"];

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
                var value = ReadUInt64(output, byteOffset);
                value = SwapAdjacentBits(value) ^ ~(ulong)RotateRight(state, 7);
                value = ReverseBits64WithoutFinal16BitSwap(SwapAdjacentBits(value));
                value = SubstituteBytes(value, SubstituteTable64);
                value = (ulong)RotateRight(state, 2) + ~value;
                value = SubstituteBytes(value, SubstituteTable64);

                WriteUInt64(output, byteOffset, value);
                AdvanceTransformState(ref state, ref prngA, ref prngB, out streamByte);
                byteOffset += 8;
                bitsRemaining -= 64;
            }

            while (bitsRemaining > 31)
            {
                var value = ReadUInt32(output, byteOffset);
                value = RotateLeft(state, 7) ^ SwapAdjacentBits(value);
                value = ReverseBits32(SwapAdjacentBits(value));
                value = SubstituteBytes(value, SubstituteTable32);
                value = ~value + RotateLeft(state, 2);
                value = SubstituteBytes(value, SubstituteTable32);

                WriteUInt32(output, byteOffset, value);
                AdvanceTransformState(ref state, ref prngA, ref prngB, out streamByte);
                byteOffset += 4;
                bitsRemaining -= 32;
            }

            while (bitsRemaining > 7)
            {
                var stateByte = (byte)((sbyte)state * 0x79);
                var value = (byte)(SwapAdjacentBits(output[byteOffset]) ^ (byte)(stateByte * 0x1b));
                value = ReverseBits8(value);
                value = SubstituteTable8[SwapAdjacentBits(value)];
                value = SubstituteTable8[(byte)(~value + stateByte)];

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
            var mixed = ((seedPlus >> 15) ^ seedPlus) >> 12 ^
                        ((seed + InitASeedAddend) * 0x02000000u) ^
                        seedPlus;
            return mixed * Multiplier;
        }
    }
}
