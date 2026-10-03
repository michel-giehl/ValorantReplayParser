// Ported from vrfkit v0.2.0 (MIT). See third-party/vrfkit-LICENSE.
using Replay.Encoding.Archives;
using static Replay.Encoding.PayloadEncryption.ValorantSeededTransformHelpers;

namespace Replay.Encoding.PayloadEncryption.VersionedTransforms;

public sealed class ValorantSeededTransform11_08 : IPayloadTransform
{
    private const uint SeedAddend = 0xacf2cdffu;
    private const int SeedOffset = -0x01;
    private const byte TailXor = 0xff;

    public IReadOnlyCollection<string> SupportedReplayVersions { get; } = ["++Ares-Core+release-11.08"];

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
                value = ~value;
                value = RotateRight(value, (int)(RotateRight(state, 7) % 63) + 1);
                value = RotateLeft(value, (int)(RotateRight(state, 6) % 63) + 1);
                value = RotateLeft(value, (int)(RotateRight(state, 5) % 63) + 1);
                value -= RotateRight(state, 2);
                value = SwapAdjacentBits(value);

                WriteUInt64(output, byteOffset, value);
                AdvanceTransformState(ref state, ref prngA, ref prngB, out streamByte);
                byteOffset += 8;
                bitsRemaining -= 64;
            }

            while (bitsRemaining > 31)
            {
                var value = ReadUInt32(output, byteOffset);
                value = ~value;
                value = RotateRight(value, (int)(RotateLeft(state, 7) % 31) + 1);
                value = RotateLeft(value, (int)(RotateLeft(state, 6) % 31) + 1);
                value = RotateLeft(value, (int)(RotateLeft(state, 5) % 31) + 1);
                value -= RotateLeft(state, 2);
                value = SwapAdjacentBits(value);

                WriteUInt32(output, byteOffset, value);
                AdvanceTransformState(ref state, ref prngA, ref prngB, out streamByte);
                byteOffset += 4;
                bitsRemaining -= 32;
            }

            while (bitsRemaining > 7)
            {
                var value = output[byteOffset];
                value = (byte)~value;
                value = RotateRight(value, (int)(state * 0x12959c3u % 7) + 1);
                value = RotateLeft(value, (int)(state * 0x1b0829u % 7) + 1);
                value = RotateLeft(value, (int)(state * 0x2751bu % 7) + 1);
                value -= (byte)(state * 0x79u);
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
