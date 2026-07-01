using Replay.Encoding.Archives;
using static Replay.Encoding.PayloadEncryption.ValorantSeededTransformHelpers;

namespace Replay.Encoding.PayloadEncryption.VersionedTransforms;

public sealed class ValorantSeededTransform12_10 : IPayloadTransform
{
    private const uint SeedAddend = 0x12fd0ee5u;
    private const uint InitAOffset = 0x1bu;
    private const byte TailXor = 0xe5;

    public IReadOnlyCollection<string> SupportedReplayVersions { get; } = ["++Ares-Core+release-12.10"];

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
                var ror4 = RotateRight(state, 4);
                var ror5 = RotateRight(state, 5);
                var ror6 = RotateRight(state, 6);
                var ror8 = RotateRight(state, 8);

                value = RotateRight(value, (int)(ror8 % 63) + 1);
                value = SwapAdjacentBits(value);
                value -= ror6;
                value = RotateRight(value, (int)(ror5 % 63) + 1);
                value = SwapAdjacentBits(value ^ ~(ulong)ror4);
                WriteUInt64(output, byteOffset, value);
                AdvanceTransformState(ref state, ref prngA, ref prngB, out streamByte);
                byteOffset += 8;
                bitsRemaining -= 64;
            }

            while (bitsRemaining > 31)
            {
                var value = ReadUInt32(output, byteOffset);
                var rot4 = RotateLeft(state, 4);
                var rot5 = RotateLeft(state, 5);
                var rot6 = RotateLeft(state, 6);
                var rot8 = RotateLeft(state, 8);

                value = RotateRight(value, (int)(rot8 % 31) + 1);
                value = SwapAdjacentBits(value);
                value -= rot6;
                value = RotateRight(value, (int)(rot5 % 31) + 1);
                value = SwapAdjacentBits(value ^ rot4);
                WriteUInt32(output, byteOffset, value);
                AdvanceTransformState(ref state, ref prngA, ref prngB, out streamByte);
                byteOffset += 4;
                bitsRemaining -= 32;
            }

            while (bitsRemaining > 7)
            {
                var value = output[byteOffset];
                var addend1 = (byte)(state * 0x31);
                var addend2 = (byte)(state * 0x29);

                value = RotateRight(value, (int)(state * 0xcc6db61 % 7) + 1);
                value = SwapAdjacentBits(value);
                value -= addend2;
                value = RotateRight(value, (int)(state * 0x2751b % 7) + 1);
                value = SwapAdjacentBits((byte)(value ^ addend1));
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
