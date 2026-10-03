// Ported from vrfkit v0.2.0 (MIT). See third-party/vrfkit-LICENSE.
using Replay.Encoding.Archives;
using static Replay.Encoding.PayloadEncryption.ValorantSeededTransformHelpers;

namespace Replay.Encoding.PayloadEncryption.VersionedTransforms;

public sealed class ValorantSeededTransform12_06 : IPayloadTransform
{
    private const uint SeedAddend = 0x8d686ca6u;
    private const int SeedOffset = 0x26;
    private const byte TailXor = 0xa6;

    public IReadOnlyCollection<string> SupportedReplayVersions { get; } = ["++Ares-Core+release-12.06"];

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
                value ^= ~(ulong)RotateRight(state, 8);
                value = RotateRight(value, (int)(RotateRight(state, 7) % 63) + 1);
                value -= RotateRight(state, 6);
                value = ReverseBits64WithoutFinal16BitSwap(value);
                value = RotateLeft(value, (int)(RotateRight(state, 3) % 63) + 1);
                value = ReverseBits64WithoutFinal16BitSwap(value);
                value -= RotateRight(state, 1);

                WriteUInt64(output, byteOffset, value);
                AdvanceTransformState(ref state, ref prngA, ref prngB, out streamByte);
                byteOffset += 8;
                bitsRemaining -= 64;
            }

            while (bitsRemaining > 31)
            {
                var value = ReadUInt32(output, byteOffset);
                value ^= RotateLeft(state, 8);
                value = RotateRight(value, (int)(RotateLeft(state, 7) % 31) + 1);
                value -= RotateLeft(state, 6);
                value = ReverseBits32(value);
                value = RotateLeft(value, (int)(RotateLeft(state, 3) % 31) + 1);
                value = ReverseBits32(value);
                value -= RotateLeft(state, 1);

                WriteUInt32(output, byteOffset, value);
                AdvanceTransformState(ref state, ref prngA, ref prngB, out streamByte);
                byteOffset += 4;
                bitsRemaining -= 32;
            }

            while (bitsRemaining > 7)
            {
                var value = output[byteOffset];
                value ^= (byte)(state * 0xcc6db61u);
                value = RotateRight(value, (int)(state * 0x12959c3u % 7) + 1);
                value -= (byte)(state * 0x1b0829u);
                value = ReverseBits8(value);
                value = RotateLeft(value, (int)(state * 0x533u % 7) + 1);
                value = ReverseBits8(value);
                value -= (byte)(state * 0xbu);

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
