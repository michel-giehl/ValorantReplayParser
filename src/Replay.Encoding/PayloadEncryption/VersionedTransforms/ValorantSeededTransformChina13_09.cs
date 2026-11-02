using Replay.Encoding.Archives;
using static Replay.Encoding.PayloadEncryption.ValorantSeededTransformHelpers;

namespace Replay.Encoding.PayloadEncryption.VersionedTransforms;

// Recovered from China 13.09 replay payloads by known plaintext, without the China executable.
internal sealed class ValorantSeededTransformChina13_09 : IPayloadTransform
{
    private const uint SeedAddend = 0x350f7d3fu;
    private const int InitASeedOffset = 0x3f;
    private const byte TailXor = 0x3f;

    public IReadOnlyCollection<string> SupportedReplayVersions { get; } =
        ["++Ares-Core+release-china-13.09"];

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
        var prngA = InitialPrngA(seed, SeedAddend, InitASeedOffset);
        var prngB = InitialPrngB(seed);
        var byteOffset = 0;
        var bitsRemaining = bitCount;

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

    private static ulong TransformUInt64(ulong value, uint state)
    {
        unchecked
        {
            value = ReverseBits64WithoutFinal16BitSwap(value + RotateRight(state, 6));
            value = ~ReverseBits64WithoutFinal16BitSwap(value - RotateRight(state, 4));
            return SubstituteBytes(value, SubstituteTable64);
        }
    }

    private static uint TransformUInt32(uint value, uint state)
    {
        unchecked
        {
            value = ReverseBits32(value + RotateLeft(state, 6));
            value = ~ReverseBits32(value - RotateLeft(state, 4));
            return SubstituteBytes(value, SubstituteTable32);
        }
    }

    private static byte TransformByte(byte value, uint state)
    {
        unchecked
        {
            value = ReverseBits8((byte)(value + (byte)(state * 0x1b0829u)));
            value = (byte)~ReverseBits8((byte)(value - (byte)(state * 0x3931u)));
            return SubstituteTable8[value];
        }
    }
}
