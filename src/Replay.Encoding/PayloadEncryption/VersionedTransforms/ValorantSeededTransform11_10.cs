// Ported from vrfkit v0.2.0 (MIT). See third-party/vrfkit-LICENSE.
using Replay.Encoding.Archives;
using static Replay.Encoding.PayloadEncryption.ValorantSeededTransformHelpers;

namespace Replay.Encoding.PayloadEncryption.VersionedTransforms;

public sealed class ValorantSeededTransform11_10 : IPayloadTransform
{
    private const uint SeedAddend = 0x34e9d3ecu;
    private const int SeedOffset = -0x14;
    private const byte TailXor = 0xec;

    public IReadOnlyCollection<string> SupportedReplayVersions { get; } = ["++Ares-Core+release-11.10"];

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
                value -= RotateRight(state, 8);
                value ^= ~(ulong)RotateRight(state, 7);
                value -= RotateRight(state, 6);
                value = RotateLeft(value, (int)(RotateRight(state, 5) % 63) + 1);
                value -= RotateRight(state, 4);
                value = ReverseBits64WithoutFinal16BitSwap(value);
                value = SubstituteBytes(value, SubstituteTable64);
                value = ~value;

                WriteUInt64(output, byteOffset, value);
                AdvanceTransformState(ref state, ref prngA, ref prngB, out streamByte);
                byteOffset += 8;
                bitsRemaining -= 64;
            }

            while (bitsRemaining > 31)
            {
                var value = ReadUInt32(output, byteOffset);
                value -= RotateLeft(state, 8);
                value ^= RotateLeft(state, 7);
                value -= RotateLeft(state, 6);
                value = RotateLeft(value, (int)(RotateLeft(state, 5) % 31) + 1);
                value -= RotateLeft(state, 4);
                value = ReverseBits32(value);
                value = SubstituteBytes(value, SubstituteTable32);
                value = ~value;

                WriteUInt32(output, byteOffset, value);
                AdvanceTransformState(ref state, ref prngA, ref prngB, out streamByte);
                byteOffset += 4;
                bitsRemaining -= 32;
            }

            while (bitsRemaining > 7)
            {
                var value = output[byteOffset];
                value -= (byte)(state * 0xcc6db61u);
                value ^= (byte)(state * 0x12959c3u);
                value -= (byte)(state * 0x1b0829u);
                value = RotateLeft(value, (int)(state * 0x2751bu % 7) + 1);
                value -= (byte)(state * 0x3931u);
                value = ReverseBits8(value);
                value = SubstituteTable8[value];
                value = (byte)~value;

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
