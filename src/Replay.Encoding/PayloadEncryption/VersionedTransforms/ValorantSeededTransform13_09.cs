using Replay.Encoding.Archives;
using static Replay.Encoding.PayloadEncryption.ValorantSeededTransformHelpers;

namespace Replay.Encoding.PayloadEncryption.VersionedTransforms;

// Recovered from the 13.09 Riot bit-serializer override, FUN_1442fda70.
public sealed class ValorantSeededTransform13_09 : IPayloadTransform
{
    private const uint SeedAddend = 0x7e8cb50f;
    private const byte TailXor = 0x0f;

    public IReadOnlyCollection<string> SupportedReplayVersions { get; } = ["++Ares-Core+release-13.09"];

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
        var prngA = InitialPrngA(seed, SeedAddend, 15);
        var prngB = InitialPrngB(seed);
        var byteOffset = 0;
        var bitsRemaining = bitCount;

        unchecked
        {
            while (bitsRemaining > 63)
            {
                WriteUInt64(output, byteOffset, TransformUInt64(ReadUInt64(output, byteOffset), state));
                AdvanceTransformState(ref state, ref prngA, ref prngB, out _);
                byteOffset += 8;
                bitsRemaining -= 64;
            }

            while (bitsRemaining > 31)
            {
                WriteUInt32(output, byteOffset, TransformUInt32(ReadUInt32(output, byteOffset), state));
                AdvanceTransformState(ref state, ref prngA, ref prngB, out _);
                byteOffset += 4;
                bitsRemaining -= 32;
            }

            while (bitsRemaining > 7)
            {
                output[byteOffset] = TransformByte(output[byteOffset], state);
                AdvanceTransformState(ref state, ref prngA, ref prngB, out _);
                byteOffset++;
                bitsRemaining -= 8;
            }

            if (bitsRemaining != 0)
            {
                var mask = (byte)(0xff >> (7 - ((bitCount - 1) & 7)));
                output[byteOffset] ^= (byte)(mask & ((byte)state ^ TailXor));
            }
        }
    }

    private static ulong TransformUInt64(ulong value, uint state)
    {
        unchecked
        {
            var shiftedState = state >> 1;
            var mixedState = ((state << 31) | shiftedState) >> 1;
            shiftedState = (shiftedState << 31 | mixedState) >> 1;
            var subtractState = mixedState << 31 | shiftedState;
            mixedState = subtractState >> 1;
            shiftedState = (shiftedState << 31 | mixedState) >> 1;
            var addState = mixedState << 31 | shiftedState;
            mixedState = addState >> 1;
            shiftedState = (shiftedState << 31 | mixedState) >> 1;
            mixedState = mixedState << 31 | shiftedState;

            var addend = (ulong)(shiftedState << 31 | mixedState >> 1) + mixedState;
            value = SubstituteBytes(value + addend, SubstituteTable64);

            var subtrahend = value - addState;
            var shifted = subtrahend >> 1;
            value = (((subtrahend * 2 ^ shifted) & 0xaaaaaaaaaaaaaaaaUL) ^ shifted) - subtractState;
            value = SubstituteBytes(value, SubstituteTable64);

            var reversed = value >> 1;
            reversed = ((value * 2 ^ reversed) & 0xaaaaaaaaaaaaaaaaUL) ^ reversed;
            var next = reversed >> 2;
            next = ((reversed * 4 ^ next) & 0xccccccccccccccccUL) ^ next;
            reversed = next >> 4;
            reversed = ((next << 4 ^ reversed) & 0xf0f0f0f0f0f0f0f0UL) ^ reversed;
            next = reversed >> 8;
            next = ((reversed << 8 ^ next) & 0xff00ff00ff00ff00UL) ^ next;
            return next >> 32 | next << 32;
        }
    }

    private static uint TransformUInt32(uint value, uint state)
    {
        unchecked
        {
            var mixedState = state * 2;
            var nextState = ((state >> 31) | mixedState) * 2;
            var subtractState = ((state & 0x7fffffff) >> 30 | nextState) * 2;
            var addState = ((mixedState & 0x7fffffff) >> 30) | subtractState;
            mixedState = addState * 2;
            nextState = ((nextState & 0x7fffffff) >> 30 | mixedState) * 2;
            var finalSubtractState = ((subtractState & 0x7fffffff) >> 30) | nextState;
            subtractState = finalSubtractState * 2;
            mixedState = ((nextState & 0x7fffffff) >> 30 |
                          ((mixedState & 0x7fffffff) >> 30 | subtractState) * 2);

            var addend = ((subtractState & 0x7fffffff) >> 30 | mixedState << 1) + mixedState;
            value = SubstituteBytes(value + addend, SubstituteTable32);

            var subtrahend = value - finalSubtractState;
            value = ((subtrahend >> 1 & 0x55555555) | ((subtrahend & 0xd5555555) * 2)) - addState;
            value = SubstituteBytes(value, SubstituteTable32);

            mixedState = (value >> 1 & 0x55555555) | ((value & 0xd5555555) * 2);
            mixedState = ((mixedState & 0xf3333333) << 2) | (mixedState >> 2 & 0x33333333);
            mixedState = (mixedState >> 4 & 0x0f0f0f0f) | ((mixedState & 0xff0f0f0f) << 4);
            return mixedState >> 24 | (mixedState & 0x00ff0000) >> 8 |
                   (mixedState & 0x0000ff00) << 8 | mixedState << 24;
        }
    }

    private static byte TransformByte(byte value, uint state)
    {
        unchecked
        {
            var stateByte = (sbyte)state;
            var tableIndex = (byte)(stateByte * 0x24 + value);
            var mixed = (byte)(SubstituteTable8[tableIndex] + stateByte * -0x1b);
            var nextIndex = (byte)(((mixed & 0xd5) * 2 | (mixed >> 1 & 0x55)) + stateByte * -0x33);
            var substituted = SubstituteTable8[nextIndex];
            var packed = ((uint)substituted * 0x802 & 0x22110) |
                         ((uint)substituted * 0x8020 & 0x88440);
            return (byte)(packed * 0x10101 >> 16);
        }
    }
}
