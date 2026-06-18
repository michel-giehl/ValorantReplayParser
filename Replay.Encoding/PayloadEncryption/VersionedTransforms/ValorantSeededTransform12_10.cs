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
                var constants = GetTransformConstants64(state);
                value = RotateRight(value, constants.Rotate2);
                value = SwapAdjacentBits(value);
                value -= constants.Addend2;
                value = RotateRight(value, constants.Rotate1);
                value = SwapAdjacentBits(value ^ ~(ulong)constants.Addend1);
                WriteUInt64(output, byteOffset, value);
                AdvanceTransformState(ref state, ref prngA, ref prngB, out streamByte);
                byteOffset += 8;
                bitsRemaining -= 64;
            }

            while (bitsRemaining > 31)
            {
                var value = ReadUInt32(output, byteOffset);
                var constants = GetTransformConstants32(state);
                value = RotateRight(value, constants.Rotate2);
                value = SwapAdjacentBits(value);
                value -= constants.Addend2;
                value = RotateRight(value, constants.Rotate1);
                value = SwapAdjacentBits(value ^ constants.Addend1);
                WriteUInt32(output, byteOffset, value);
                AdvanceTransformState(ref state, ref prngA, ref prngB, out streamByte);
                byteOffset += 4;
                bitsRemaining -= 32;
            }

            while (bitsRemaining > 7)
            {
                var value = output[byteOffset];
                var constants = GetTransformConstants8(state);
                value = RotateRight(value, constants.Rotate2);
                value = SwapAdjacentBits(value);
                value -= constants.Addend2;
                value = RotateRight(value, constants.Rotate1);
                value = SwapAdjacentBits((byte)(value ^ constants.Addend1));
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

    private static TransformConstants64 GetTransformConstants64(uint state)
    {
        var ror1 = (state >> 1) | (state << 31);
        var ror2 = (ror1 >> 1) | (ror1 << 31);
        var ror3 = (ror2 >> 1) | (ror2 << 31);
        var ror4 = (ror3 >> 1) | (ror3 << 31);
        var ror5 = (ror4 >> 1) | (ror4 << 31);
        var ror6 = (ror5 >> 1) | (ror5 << 31);
        var ror7 = (ror6 >> 1) | (ror6 << 31);
        var ror8 = (ror7 >> 1) | (ror7 << 31);

        return new TransformConstants64(ror4, ror6, (int)(ror5 % 63) + 1, (int)(ror8 % 63) + 1);
    }

    private static TransformConstants32 GetTransformConstants32(uint state)
    {
        var rot1 = (state << 1) | (state >> 31);
        var rot2 = (rot1 << 1) | (rot1 >> 31);
        var rot3 = (rot2 << 1) | (rot2 >> 31);
        var rot4 = (rot3 << 1) | (rot3 >> 31);
        var rot5 = (rot4 << 1) | (rot4 >> 31);
        var rot6 = (rot5 << 1) | (rot5 >> 31);
        var rot7 = (rot6 << 1) | (rot6 >> 31);
        var rot8 = (rot7 << 1) | (rot7 >> 31);

        return new TransformConstants32(rot4, rot6, (int)(rot5 % 31) + 1, (int)(rot8 % 31) + 1);
    }

    private static TransformConstants8 GetTransformConstants8(uint state) =>
        new((byte)(state * 0x31), (byte)(state * 0x29), (int)(state * 0x2751b % 7) + 1,
            (int)(state * 0xcc6db61 % 7) + 1);

    private readonly record struct TransformConstants64(uint Addend1, uint Addend2, int Rotate1, int Rotate2);

    private readonly record struct TransformConstants32(uint Addend1, uint Addend2, int Rotate1, int Rotate2);

    private readonly record struct TransformConstants8(byte Addend1, byte Addend2, int Rotate1, int Rotate2);
}