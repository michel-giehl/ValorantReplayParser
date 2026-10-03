using Replay.Encoding.Archives;
using Replay.Encoding.PayloadEncryption;

namespace Replay.Encoding.Tests.PayloadEncryption;

public partial class ValorantLegacyTransformTests
{
    private static readonly string[] LegacyBranches =
    [
        "++Ares-Core+release-11.06",
        "++Ares-Core+release-11.07",
        "++Ares-Core+release-11.08",
        "++Ares-Core+release-11.09",
        "++Ares-Core+release-11.10",
        "++Ares-Core+release-11.11",
        "++Ares-Core+release-12.00",
        "++Ares-Core+release-12.01",
        "++Ares-Core+release-12.02",
        "++Ares-Core+release-12.03",
        "++Ares-Core+release-12.04",
        "++Ares-Core+release-12.05",
        "++Ares-Core+release-12.06",
        "++Ares-Core+release-12.07",
        "++Ares-Core+release-12.08",
        "++Ares-Core+release-12.09",
    ];

    [TestCaseSource(nameof(NativeTransformVectors))]
    public void Apply_MatchesNativeExecutableOutput(
        string replayVersion, int bitCount, uint seed, string inputHex, string expectedHex)
    {
        var transform = PayloadTransformRegistry.CreateDefault().GetRequired(replayVersion);
        var bytes = Convert.FromHexString(inputHex);
        var byteCount = transform.GetOutputByteCount(bitCount);
        var remainingInput = new BitArchiveReader(bytes, bitCount);
        var explicitInput = new BitArchiveReader(bytes, bitCount);
        var remainingOutput = Enumerable.Repeat((byte)0xa5, byteCount + 3).ToArray();
        var explicitOutput = Enumerable.Repeat((byte)0xa5, byteCount + 3).ToArray();

        transform.Apply(remainingInput, seed, remainingOutput);
        transform.Apply(explicitInput, bitCount, seed, explicitOutput);

        Assert.Multiple(() =>
        {
            Assert.That(Convert.ToHexString(remainingOutput.AsSpan(0, byteCount)), Is.EqualTo(expectedHex));
            Assert.That(Convert.ToHexString(explicitOutput.AsSpan(0, byteCount)), Is.EqualTo(expectedHex));
            Assert.That(remainingOutput.AsSpan(byteCount).ToArray(), Is.EqualTo(new byte[] { 0xa5, 0xa5, 0xa5 }));
            Assert.That(explicitOutput.AsSpan(byteCount).ToArray(), Is.EqualTo(new byte[] { 0xa5, 0xa5, 0xa5 }));
            Assert.That(remainingInput.AtEnd, Is.True);
            Assert.That(explicitInput.AtEnd, Is.True);
        });
    }

    [TestCase("++Ares-Core+release-11.06", "9E33B8BF58F4BDD61F6FFD121C852D6A98C47A633BB6EABF3BF236889693397DF4D96032")]
    [TestCase("++Ares-Core+release-11.07", "D1616B9C1A09CEF24F20AC1AAC42715FAAEC028E7FDE1FF6F5B77F712A45CF8FF4900A03")]
    [TestCase("++Ares-Core+release-11.08", "E6C17CE50B7F40808ABFA62345FDFFFFD21678CF3707DE7CC13108A2F1FFF25B666B7D6D")]
    [TestCase("++Ares-Core+release-11.09", "2D113E94B49F3F37713E6F03CABAE247A1876BD0F1CCA4FB154C987334D0F8B9D1CBD376")]
    [TestCase("++Ares-Core+release-11.10", "87FB90C208DB5905F33582828914C1CFED0A07A441668E8F14C64073838C669EDA643238")]
    [TestCase("++Ares-Core+release-11.11", "8DDCED0D671DE52A06D43C1855E820310752F47383FD34B93E3AF1602C16F4731AB33B55")]
    [TestCase("++Ares-Core+release-12.00", "33FF3C07A8C202EFE6FD674D6E0CBEDE5051F6C09CC3BD0C256CB2600E0052038A66B628")]
    [TestCase("++Ares-Core+release-12.01", "304412E9C6E422659E8229F836AF1F09C98AF60EAE43CA8FC43AF03C0290D4C138D6C176")]
    [TestCase("++Ares-Core+release-12.02", "EF71A18C13D745899B76AF9B4608C611F77D513635F2B96104AFCC6F4BE0C770E166161B")]
    [TestCase("++Ares-Core+release-12.03", "00FF7DC91028152ECE76302E20AA667548FF40F05938DB7381789CF9A62E1009A2958C5E")]
    [TestCase("++Ares-Core+release-12.04", "6C8C4B40D08DA367B48A8663A5E12657F24772CDB4404D0153D9CDA832FE7E0B2D783403")]
    [TestCase("++Ares-Core+release-12.05", "46F7866D076409800B0B7EF2C70F54F505BE7F72062FAAD310D95A74BF3E621CFB49B02E")]
    [TestCase("++Ares-Core+release-12.06", "763FE18CD402F8E78D7247DC33FED8FDD13C83474F00165AF54954128DC4FE09BEC71407")]
    [TestCase("++Ares-Core+release-12.07", "44B6AACEAC7ED4CD54399785D2E9177700E5840FA5233609CA9C2D65712FA8983C17FD2E")]
    [TestCase("++Ares-Core+release-12.08", "82D02D2842B9F2AC05E9B544FC1427B131CBFBF15C1A7C95BB66F3701D76B2E16371F37C")]
    [TestCase("++Ares-Core+release-12.09", "86693570AA008154B3AF05E54442416815B00A38BD47AD46E72ABA7AE78F9BEC261A6032")]
    public void Apply_UnalignedExplicitPayload_PreservesFollowingBits(string replayVersion, string expectedHex)
    {
        const int prefixBits = 3;
        const int bitCount = 287;
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

        var payload = new BitArchiveReader(framedBytes, prefixBits + bitCount + 8);
        payload.SkipBits(prefixBits);
        var transform = PayloadTransformRegistry.CreateDefault().GetRequired(replayVersion);
        var output = new byte[transform.GetOutputByteCount(bitCount)];

        transform.Apply(payload, bitCount, 285u, output);

        Assert.Multiple(() =>
        {
            Assert.That(Convert.ToHexString(output), Is.EqualTo(expectedHex));
            Assert.That(payload.BitPosition, Is.EqualTo(prefixBits + bitCount));
            Assert.That(payload.ReadBitsToUInt64(8), Is.EqualTo(0xa7));
            Assert.That(payload.AtEnd, Is.True);
        });
    }

    [TestCaseSource(nameof(LegacyBranches))]
    public void Apply_TooSmallOutput_FailsBeforeConsumingInput(string replayVersion)
    {
        var input = new BitArchiveReader(new byte[9], 65);
        var transform = PayloadTransformRegistry.CreateDefault().GetRequired(replayVersion);
        var output = new byte[8];

        var exception = Assert.Throws<ArchiveReadException>(() => transform.Apply(input, 0u, output));

        Assert.Multiple(() =>
        {
            Assert.That(exception!.ErrorCode, Is.EqualTo(ArchiveErrorCode.BufferTooSmall));
            Assert.That(exception.Requested, Is.EqualTo(9));
            Assert.That(input.BitPosition, Is.Zero);
        });
    }

    [TestCaseSource(nameof(LegacyBranches))]
    public void Apply_TruncatedInput_FailsBeforeModifyingOutput(string replayVersion)
    {
        var input = new BitArchiveReader(new byte[8], 64);
        var transform = PayloadTransformRegistry.CreateDefault().GetRequired(replayVersion);
        var output = Enumerable.Repeat((byte)0xa5, 9).ToArray();

        var exception = Assert.Throws<ArchiveReadException>(() => transform.Apply(input, 65, 0u, output));

        Assert.Multiple(() =>
        {
            Assert.That(exception!.ErrorCode, Is.EqualTo(ArchiveErrorCode.EndOfArchive));
            Assert.That(exception.Requested, Is.EqualTo(65));
            Assert.That(input.BitPosition, Is.Zero);
            Assert.That(output, Is.All.EqualTo(0xa5));
        });
    }

    [TestCaseSource(nameof(LegacyBranches))]
    public void Apply_NegativeBitCount_IsRejected(string replayVersion)
    {
        var input = new BitArchiveReader(new byte[1]);
        var transform = PayloadTransformRegistry.CreateDefault().GetRequired(replayVersion);

        var exception = Assert.Throws<ArchiveReadException>(() => transform.Apply(input, -1, 0u, new byte[1]));

        Assert.Multiple(() =>
        {
            Assert.That(exception!.ErrorCode, Is.EqualTo(ArchiveErrorCode.InvalidBitCount));
            Assert.That(input.BitPosition, Is.Zero);
        });
        Assert.Throws<ArgumentOutOfRangeException>(() => transform.GetOutputByteCount(-1));
    }

    [TestCaseSource(nameof(LegacyBranches))]
    public void Registry_ResolvesOnlyExactLegacyBranch(string replayVersion)
    {
        var registry = PayloadTransformRegistry.CreateDefault();

        Assert.That(registry.GetRequired(replayVersion).SupportedReplayVersions, Is.EqualTo(new[] { replayVersion }));
        Assert.Throws<UnsupportedPayloadTransformVersionException>(() => registry.GetRequired(replayVersion.ToLowerInvariant()));
        Assert.Throws<UnsupportedPayloadTransformVersionException>(() => registry.GetRequired(replayVersion + ".1"));
    }

    [TestCase("++Ares-Core+release-11.05")]
    [TestCase("++Ares-Core+release-11.12")]
    [TestCase("++Ares-Core+release-12.12")]
    [TestCase("release-11.06")]
    public void Registry_RejectsUnregisteredBranches(string replayVersion)
    {
        Assert.Throws<UnsupportedPayloadTransformVersionException>(() =>
            PayloadTransformRegistry.CreateDefault().GetRequired(replayVersion));
    }
}
