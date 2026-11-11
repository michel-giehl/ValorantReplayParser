using Replay.Encoding.Archives;
using static Replay.Encoding.PayloadEncryption.ValorantSeededTransformHelpers;

namespace Replay.Encoding.PayloadEncryption.VersionedTransforms;

// Recovered from the global 13.10 executable, FUN_14432eb50.
public sealed class ValorantSeededTransform13_10 : IPayloadTransform
{
    private const uint SeedAddend = 0x2c249eb3u;
    private const byte TailXor = 0xb3;

    public IReadOnlyCollection<string> SupportedReplayVersions { get; } = ["++Ares-Core+release-13.10"];

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
        var prngA = InitialPrngA(seed, SeedAddend, 0x33);
        var prngB = InitialPrngB(seed);
        var byteOffset = 0;
        var bitsRemaining = bitCount;

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

    private static ulong TransformUInt64(ulong value, uint state)
    {
        unchecked
        {
            value -= RotateRight(state, 8);
            value = RotateLeft(value, (int)(RotateRight(state, 6) % 63) + 1);
            value = SwapAdjacentBits(value);
            value ^= ~(ulong)RotateRight(state, 3);
            return value - RotateRight(state, 1);
        }
    }

    private static uint TransformUInt32(uint value, uint state)
    {
        unchecked
        {
            value -= RotateLeft(state, 8);
            value = RotateLeft(value, (int)(RotateLeft(state, 6) % 31) + 1);
            value = SwapAdjacentBits(value);
            value ^= RotateLeft(state, 3);
            return value - RotateLeft(state, 1);
        }
    }

    private static byte TransformByte(byte value, uint state)
    {
        unchecked
        {
            value = (byte)(value - (byte)(state * 0xcc6db61u));
            value = RotateLeft(value, (int)(state * 0x1b0829u % 7) + 1);
            value = SwapAdjacentBits(value);
            value ^= (byte)(state * 0x533u);
            return (byte)(value - (byte)(state * 0xbu));
        }
    }
}
