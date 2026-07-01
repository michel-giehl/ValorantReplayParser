using Replay.Encoding.Archives;
using System.Runtime.CompilerServices;
using static Replay.Encoding.PayloadEncryption.ValorantSeededTransformHelpers;

namespace Replay.Encoding.PayloadEncryption.VersionedTransforms;

public sealed class ValorantSeededTransform13_00 : IPayloadTransform
{
    private const uint SeedAddend = 0x2949b6efu;
    private const uint InitAOffset = 0x11u;
    private const byte TailXor = 0xef;

    public IReadOnlyCollection<string> SupportedReplayVersions { get; } = ["++Ares-Core+release-13.00"];

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
                var ror1 = RotateRight(state, 1);
                var ror3 = RotateRight(state, 3);
                var ror6 = RotateRight(state, 6);
                var ror8 = RotateRight(state, 8);

                value += ror8;
                value = ReverseBits64WithoutFinal16BitSwap(value);
                value = (value + ror6) ^ ror3;
                value = SubstituteBytes(value, SubstituteTable64V13_00);
                value = RotateRight(value, (int)(ror1 % 63) + 1);

                WriteUInt64(output, byteOffset, value);
                AdvanceTransformState(ref state, ref prngA, ref prngB, out streamByte);
                byteOffset += 8;
                bitsRemaining -= 64;
            }

            while (bitsRemaining > 31)
            {
                var value = ReadUInt32(output, byteOffset);
                var rol1 = RotateLeft(state, 1);
                var rol3 = RotateLeft(state, 3);
                var rol6 = RotateLeft(state, 6);
                var rol8 = RotateLeft(state, 8);

                value += rol8;
                value = ReverseBits32(value);
                value = ~(value + rol6) ^ rol3;
                value = SubstituteBytes(value, SubstituteTable32V13_00);
                value = RotateRight(value, (int)(rol1 % 31) + 1);

                WriteUInt32(output, byteOffset, value);
                AdvanceTransformState(ref state, ref prngA, ref prngB, out streamByte);
                byteOffset += 4;
                bitsRemaining -= 32;
            }

            while (bitsRemaining > 7)
            {
                var value = output[byteOffset];
                var mix = state * 0x533u;

                value = (byte)(value + (byte)mix * 0x1b);
                value = ReverseBits8(value);
                value = (byte)(~(value + (byte)mix * 0x33) ^ (byte)mix);
                value = SubstituteTable8V13_00[value];
                value = RotateRight(value, (int)(state * 0x0bu % 7) + 1);

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

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static ulong SubstituteBytes(ulong value, byte[] table)
    {
        return (ulong)table[(byte)value] |
               ((ulong)table[(byte)(value >> 8)] << 8) |
               ((ulong)table[(byte)(value >> 16)] << 16) |
               ((ulong)table[(byte)(value >> 24)] << 24) |
               ((ulong)table[(byte)(value >> 32)] << 32) |
               ((ulong)table[(byte)(value >> 40)] << 40) |
               ((ulong)table[(byte)(value >> 48)] << 48) |
               ((ulong)table[(byte)(value >> 56)] << 56);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static uint SubstituteBytes(uint value, byte[] table)
    {
        return (uint)table[(byte)value] |
               ((uint)table[(byte)(value >> 8)] << 8) |
               ((uint)table[(byte)(value >> 16)] << 16) |
               ((uint)table[(byte)(value >> 24)] << 24);
    }

    private static readonly byte[] SubstituteTable32V13_00 = Convert.FromHexString(
        "2167b396313fbad3d5062b16f1b651a79c7b419584251536a4703546b05fa6c3" +
        "bb8638f62ea2a994831b6239f3d228149e9af2c9decc26a1d8d0748d69127189" +
        "f758cd4db7114809b968c77cf42042f56b54756da81d6a07d7c50ea066db" +
        "f899ad1004ff8fb1ef986c29e201183d371e654b4a6e24d9bd90fe135693" +
        "34aa8b0d79e74992f98eca43cbc6da022d8c0fb2c08a4785aee0d477c40b" +
        "5c617e335745e62ffd6f915b9fcf3c4fe33aede480087372ea63fbfcb8" +
        "7a23a51f815952875dfa78c1b5beb4a3641c3253f07fdc3b7640ec309755" +
        "4c00bc880c05e1df197d22c25a9be52a50bf1ac8035e2cd1abdd44ee82" +
        "ce27afebd64e0ae9173e9de8ac60");

    private static readonly byte[] SubstituteTable64V13_00 = Convert.FromHexString(
        "77b9042feb7d27c944739a3f36f565ddf7e0302da9985dde69a394a05e170678" +
        "a4f6ab0343c828e56a8e1cf270cf5305d30dffa7a23a32255a1f48c1" +
        "b7e16e85996047bbe48acbc01bea6164f0c2d88bcdfdadb819b5bf0e9181" +
        "839d45d249e9c731bd20bec66680d179d7e6fca15b5fdff1d0506752fe" +
        "7b3513f846b3758de33e2ef4dc342a0823e20c094beec30f248f544c" +
        "5539cc1d1e3b2272da296b41aaa6122c93ca9c970a56a87a9eb462923" +
        "d9f38f3408437b2d4af7633fa21effb716f9082511ac574f95907ba11" +
        "b1acd6ede702ae9610167c4f881426bc1501684a2b0b7fa54ee86dec4d" +
        "b05cc4009558b6d57e42db5718866cced99b89873c8c63");

    private static readonly byte[] SubstituteTable8V13_00 = Convert.FromHexString(
        "0a6c6996cadc5a08b38339a0f9adf4560e6e4c85649982d4885c8736239a" +
        "112db8c4341866136f59e07422faa665e2d7954e94b0779e1aeee705a" +
        "2c830900d9bd219c93a471512a9291f53acaf4352aef54dbfbee34a06" +
        "d5d0a378a7d61c7a6b81d8dee568fb267ebcbae8cce4727f2cfcf0" +
        "ec28716048ef3e038f1ef16a8df2461b9c86f7b476628a10fd6d0b" +
        "3f9f2f555fc3c6921627d344840fe1808cb7738945db332550ea0414" +
        "c50c32415e79a41d3d5b4037c1cffe2b54eb9d4991f307173cda57" +
        "8bcd61f6ce702eff2193972a7d67abb57c5d0042a5d92051eddd0209" +
        "c2d1f8bdbbe93524985838aab9a8b27501cbc063df3b8ec731b1a1" +
        "b6e67b4b4f");
}
