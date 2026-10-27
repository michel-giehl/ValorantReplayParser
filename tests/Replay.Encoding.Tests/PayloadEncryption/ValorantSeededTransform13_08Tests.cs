using Replay.Encoding.Archives;
using Replay.Encoding.PayloadEncryption;

namespace Replay.Encoding.Tests.PayloadEncryption;

public class ValorantSeededTransform13_08Tests
{
    private const string Branch = "++Ares-Core+release-13.08";
    private const string PayloadHex = "BFDF6F9EA1F27BA00000C66EAFAF2E0000339C0DD34B0C45C48063038003562A43C0C949";

    // Native outputs from the global 13.08 executable at RVA 0x4539860.
    // The executable was mapped without initialization; only the recovered routine
    // ran on scratch buffers. The test suite needs neither the executable nor Ghidra.
    [TestCase(8, 0xffffffffu, "5D")]
    [TestCase(32, 0xffffffffu, "E3BF8FB2")]
    [TestCase(64, 0xffffffffu, "1B32C16E98E8E25A")]
    [TestCase(287, 0u, "29CFDE482DEC365E8970E0AB71F8148FA5E18EF7B59A056A1EC6131835AB6D5804834E6E")]
    [TestCase(287, 0x80000000u, "31847EEDBC6334F22B590E81DD10B964AF5F7D223E089572CFD507F25C198D9D79821843")]
    [TestCase(287, 0xffffffffu, "1B32C16E98E8E25A8A6AC1833A088AB9A97A9DC652261B5E73DAE8107E646A2D8C94AC0B")]
    [TestCase(287, 0xc3e85606u, "0553099F20E07814CD8910140C5E575810E7C4DDEFFAEECF2D0706BCAAC6B43C2F7B0D13")]
    [TestCase(287, 0x12345678u, "9BA1212934D5F92E1A22C1260A08A73A5053BA370B6FB22D098D7B639621F974A208DD25")]
    [TestCase(95, 0x12345678u, "9BA1212934D5F92E4B451B7D")]
    [TestCase(96, 0x12345678u, "9BA1212934D5F92EBAC42D11")]
    [TestCase(127, 0x12345678u, "9BA1212934D5F92EBAC42D112EAD9B58")]
    [TestCase(128, 0x12345678u, "9BA1212934D5F92E1A22C1260A08A73A")]
    [TestCase(135, 0x12345678u, "9BA1212934D5F92E1A22C1260A08A73A66")]
    public void Apply_MatchesNativeRoutine(int bitCount, uint seed, string expectedHex)
    {
        var transform = PayloadTransformRegistry.CreateDefault().GetRequired(Branch);
        var bytes = Convert.FromHexString(PayloadHex);
        var byteCount = transform.GetOutputByteCount(bitCount);
        using var remainingInput = new BitArchiveReader(bytes, bitCount);
        using var explicitInput = new BitArchiveReader(bytes, bitCount);
        var remainingOutput = Enumerable.Repeat((byte)0xa5, byteCount + 3).ToArray();
        var explicitOutput = Enumerable.Repeat((byte)0xa5, byteCount + 3).ToArray();

        transform.Apply(remainingInput, seed, remainingOutput);
        transform.Apply(explicitInput, bitCount, seed, explicitOutput);

        Assert.Multiple(() =>
        {
            Assert.That(Convert.ToHexString(remainingOutput.AsSpan(0, byteCount)), Is.EqualTo(expectedHex));
            Assert.That(Convert.ToHexString(explicitOutput.AsSpan(0, byteCount)), Is.EqualTo(expectedHex));
            Assert.That(remainingOutput.AsSpan(byteCount).ToArray(), Is.All.EqualTo(0xa5));
            Assert.That(explicitOutput.AsSpan(byteCount).ToArray(), Is.All.EqualTo(0xa5));
            Assert.That(remainingInput.AtEnd, Is.True);
            Assert.That(explicitInput.AtEnd, Is.True);
        });
    }

    [TestCase(95, 0x12345678u, "9BA1212934D5F92E4B451B7D")]
    [TestCase(96, 0x12345678u, "9BA1212934D5F92EBAC42D11")]
    [TestCase(287, 285u, "1FEE335A39DA29B773FED1DF6DCD7519C180BAA20B6F5B3045DC6EDC088C26A906195007")]
    public void Apply_UnalignedExplicitPayload_PreservesFollowingBits(int bitCount, uint seed, string expectedHex)
    {
        const int prefixBits = 3;
        var source = Convert.FromHexString(PayloadHex);
        var framedBytes = new byte[(prefixBits + bitCount + 8 + 7) / 8];
        for (var bit = 0; bit < bitCount + 8; bit++)
        {
            var value = bit < bitCount
                ? (source[bit / 8] >> (bit & 7)) & 1
                : (0xa7 >> (bit - bitCount)) & 1;
            var framedBit = prefixBits + bit;
            framedBytes[framedBit / 8] |= (byte)(value << (framedBit & 7));
        }

        using var input = new BitArchiveReader(framedBytes, prefixBits + bitCount + 8);
        input.SkipBits(prefixBits);
        var transform = PayloadTransformRegistry.CreateDefault().GetRequired(Branch);
        var output = new byte[transform.GetOutputByteCount(bitCount)];

        transform.Apply(input, bitCount, seed, output);

        Assert.Multiple(() =>
        {
            Assert.That(Convert.ToHexString(output), Is.EqualTo(expectedHex));
            Assert.That(input.BitPosition, Is.EqualTo(prefixBits + bitCount));
            Assert.That(input.ReadBitsToUInt64(8), Is.EqualTo(0xa7));
            Assert.That(input.AtEnd, Is.True);
        });
    }

    [TestCase(-1, 1, ArchiveErrorCode.InvalidBitCount)]
    [TestCase(65, 8, ArchiveErrorCode.BufferTooSmall)]
    [TestCase(65, 9, ArchiveErrorCode.EndOfArchive)]
    public void Apply_InvalidPayload_FailsBeforeConsumingInputOrModifyingOutput(
        int bitCount, int outputSize, ArchiveErrorCode errorCode)
    {
        using var input = new BitArchiveReader(new byte[8], 64);
        var transform = PayloadTransformRegistry.CreateDefault().GetRequired(Branch);
        var output = Enumerable.Repeat((byte)0xa5, outputSize).ToArray();

        var exception = Assert.Throws<ArchiveReadException>(() => transform.Apply(input, bitCount, 0u, output));

        Assert.Multiple(() =>
        {
            Assert.That(exception!.ErrorCode, Is.EqualTo(errorCode));
            Assert.That(input.BitPosition, Is.Zero);
            Assert.That(output, Is.All.EqualTo(0xa5));
        });
    }

    [TestCase("++Ares-Core+release-13.07")]
    [TestCase("++Ares-Core+release-13.08.1")]
    [TestCase("++ares-core+release-13.08")]
    [TestCase("release-13.08")]
    public void Registry_RejectsUnregisteredBranches(string replayVersion)
    {
        Assert.Throws<UnsupportedPayloadTransformVersionException>(() =>
            PayloadTransformRegistry.CreateDefault().GetRequired(replayVersion));
    }
}
