// Ported from vrfkit v0.2.0 (MIT). See third-party/vrfkit-LICENSE.
using Replay.Encoding.Archives;
using static Replay.Encoding.PayloadEncryption.ValorantSeededTransformHelpers;

namespace Replay.Encoding.PayloadEncryption.VersionedTransforms;

public sealed class ValorantSeededTransform11_06 : IPayloadTransform
{
    private const uint SeedAddend = 0x3325e3bdu;
    private const int SeedOffset = 0x3d;
    private const byte TailXor = 0xbd;

    public IReadOnlyCollection<string> SupportedReplayVersions { get; } = ["++Ares-Core+release-11.06"];

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
        var prngA = InitialPrngA(seed, SeedAddend, SeedOffset);
        var prngB = InitialPrngB(seed);
        var byteOffset = 0;
        var bitsRemaining = bitCount;

        unchecked
        {
            while (bitsRemaining > 63)
            {
                var value = ReadUInt64(output, byteOffset);
                value += RotateRight(state, 8);
                value = SubstituteBytes(value, SubstituteTable64);
                value ^= ~(ulong)RotateRight(state, 6);
                value = SubstituteBytes(value, SubstituteTable64);
                value = SubstituteBytes(value, SubstituteTable64);
                value = ReverseBits64WithoutFinal16BitSwap(value);
                value = SwapAdjacentBits(value);

                WriteUInt64(output, byteOffset, value);
                AdvanceTransformState(ref state, ref prngA, ref prngB, out streamByte);
                byteOffset += 8;
                bitsRemaining -= 64;
            }

            while (bitsRemaining > 31)
            {
                var value = ReadUInt32(output, byteOffset);
                value += RotateLeft(state, 8);
                value = SubstituteBytes(value, SubstituteTable32);
                value ^= RotateLeft(state, 6);
                value = SubstituteBytes(value, SubstituteTable32);
                value = SubstituteBytes(value, SubstituteTable32);
                value = ReverseBits32(value);
                value = SwapAdjacentBits(value);

                WriteUInt32(output, byteOffset, value);
                AdvanceTransformState(ref state, ref prngA, ref prngB, out streamByte);
                byteOffset += 4;
                bitsRemaining -= 32;
            }

            while (bitsRemaining > 7)
            {
                var value = output[byteOffset];
                value += (byte)(state * 0xcc6db61u);
                value = SubstituteTable8[value];
                value ^= (byte)(state * 0x1b0829u);
                value = SubstituteTable8[value];
                value = SubstituteTable8[value];
                value = ReverseBits8(value);
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
}
