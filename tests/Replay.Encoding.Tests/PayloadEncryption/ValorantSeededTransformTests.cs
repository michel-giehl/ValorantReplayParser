using Replay.Encoding.Archives;
using Replay.Encoding.PayloadEncryption;

namespace Replay.Encoding.Tests.PayloadEncryption;

public class ValorantSeededTransformTests
{
    private const string PayloadHex = "BFDF6F9EA1F27BA00000C66EAFAF2E0000339C0DD34B0C45C48063038003562A43C0C949";
    private const string TransformedHex = "100CA461300F080493400100000040394E5120000000B0792C626000000080FE7F3C2000";
    private const int PayloadBits = 287;
    private const uint ActorNetGuid = 2;

    private static readonly TestCaseData[] KnownTransformVectors =
    [
        new("++Ares-Core+release-12.10", 0, ""),
        new("++Ares-Core+release-12.10", 1, "01"),
        new("++Ares-Core+release-12.10", 7, "5F"),
        new("++Ares-Core+release-12.10", 8, "50"),
        new("++Ares-Core+release-12.10", 31, "49629D71"),
        new("++Ares-Core+release-12.10", 32, "A8FC7EF3"),
        new("++Ares-Core+release-12.10", 63, "47D3ED3F73178739"),
        new("++Ares-Core+release-12.10", 64, "10AC2E70AD1212C0"),
        new("++Ares-Core+release-12.10", 65, "7721410F808044D200"),
        new("++Ares-Core+release-12.10", 287, TransformedHex),
        new("++Ares-Core+release-12.10", 288, "398140967937107E4FA28FEB1FD75CAED71D618B1940D9C6092174B47BA199FD5E2F8393"),
        new("++Ares-Core+release-12.11", 0, ""),
        new("++Ares-Core+release-12.11", 1, "01"),
        new("++Ares-Core+release-12.11", 7, "19"),
        new("++Ares-Core+release-12.11", 8, "F4"),
        new("++Ares-Core+release-12.11", 31, "3C77997B"),
        new("++Ares-Core+release-12.11", 32, "18F42FF1"),
        new("++Ares-Core+release-12.11", 63, "8EF2B27ADAE67472"),
        new("++Ares-Core+release-12.11", 64, "D1545FF0BD2FB867"),
        new("++Ares-Core+release-12.11", 65, "E7A9BFB07CFF24CF01"),
        new("++Ares-Core+release-12.11", 287, "43FE3C8BA5D21FEFBFA741CE0E0071A3F279A1C6E817075ACF20662447D9E50F75F1481D"),
        new("++Ares-Core+release-12.11", 288, "022F9877FE647DE0F27D5FE813C5FC03BA3EA8C3D7C7BB79B8E1C7755F405825611C0C99"),
        new("++Ares-Core+release-13.00", 0, ""),
        new("++Ares-Core+release-13.00", 1, "01"),
        new("++Ares-Core+release-13.00", 7, "55"),
        new("++Ares-Core+release-13.00", 8, "88"),
        new("++Ares-Core+release-13.00", 31, "01B0DD66"),
        new("++Ares-Core+release-13.00", 32, "901B662B"),
        new("++Ares-Core+release-13.00", 63, "029693CD8ADFD510"),
        new("++Ares-Core+release-13.00", 64, "224FB261A44ADF65"),
        new("++Ares-Core+release-13.00", 65, "C8336218A9D2979001"),
        new("++Ares-Core+release-13.00", 287, "4FE8F025C0F05BA5DBDD798E8A23E32372F1B49C61C270104E7BD61458C2A433218A1A77"),
        new("++Ares-Core+release-13.00", 288, "8772F8F262B8A7D2A6703E5E961BA7D703AC43D56EE0CC82F4BE1987FC7847365E6B7C32"),
    ];

    [TestCaseSource(nameof(KnownTransformVectors))]
    public void Apply_ProducesKnownPayloadTransform(string replayVersion, int bitCount, string transformedHex)
    {
        var payload = new BitArchiveReader(Convert.FromHexString(PayloadHex), bitCount);
        var transform = PayloadTransformRegistry.CreateDefault().GetRequired(replayVersion);
        var output = new byte[transform.GetOutputByteCount(bitCount)];

        transform.Apply(payload, ((uint)bitCount) ^ ActorNetGuid, output);

        Assert.That(Convert.ToHexString(output), Is.EqualTo(transformedHex));
        Assert.That(payload.AtEnd, Is.True);
    }

    [TestCase("++Ares-Core+release-12.10", 31)]
    [TestCase("++Ares-Core+release-12.11", 64)]
    [TestCase("++Ares-Core+release-13.00", 65)]
    public void Apply_WithExplicitBitCount_ConsumesOnlyRequestedPayloadBits(string replayVersion, int bitCount)
    {
        var bytes = Convert.FromHexString(PayloadHex);
        var expectedTail = new BitArchiveReader(bytes, bitCount + 8);
        expectedTail.SkipBits(bitCount);
        var expectedNextByte = expectedTail.ReadBitsToUInt64(8);

        var payload = new BitArchiveReader(bytes, bitCount + 8);
        var transform = PayloadTransformRegistry.CreateDefault().GetRequired(replayVersion);
        var output = new byte[transform.GetOutputByteCount(bitCount)];

        transform.Apply(payload, bitCount, ((uint)bitCount) ^ ActorNetGuid, output);

        Assert.Multiple(() =>
        {
            Assert.That(payload.BitPosition, Is.EqualTo(bitCount));
            Assert.That(payload.ReadBitsToUInt64(8), Is.EqualTo(expectedNextByte));
        });
    }

    [Test]
    public void Apply_Release12_10_ProducesKnownPayloadTransform()
    {
        var payload = new BitArchiveReader(Convert.FromHexString(PayloadHex), PayloadBits);
        var registry = PayloadTransformRegistry.CreateDefault();
        var transform = registry.GetRequired("++Ares-Core+release-12.10");
        var output = new byte[transform.GetOutputByteCount(PayloadBits)];

        transform.Apply(
            payload,
            PayloadBits ^ ActorNetGuid,
            output);

        Assert.That(Convert.ToHexString(output), Is.EqualTo(TransformedHex));
        Assert.That(payload.AtEnd, Is.True);
    }

    [TestCase(PayloadBits)]
    [TestCase(288)]
    public void Apply_Release13_00_InitializesTablesAndConsumesPayload(int bitCount)
    {
        var payload = new BitArchiveReader(Convert.FromHexString(PayloadHex), bitCount);
        var registry = PayloadTransformRegistry.CreateDefault();
        var transform = registry.GetRequired("++Ares-Core+release-13.00");
        var output = new byte[transform.GetOutputByteCount(bitCount)];

        transform.Apply(
            payload,
            ((uint)bitCount) ^ ActorNetGuid,
            output);

        Assert.That(Convert.ToHexString(output), Is.Not.EqualTo(PayloadHex));
        Assert.That(payload.AtEnd, Is.True);
    }

    [Test]
    public void Registry_ResolvesExactReplayVersion()
    {
        var registry = PayloadTransformRegistry.CreateDefault();

        Assert.That(registry.GetRequired("++Ares-Core+release-12.10"), Is.Not.Null);
        Assert.That(registry.GetRequired("++Ares-Core+release-12.11"), Is.Not.Null);
        Assert.That(registry.GetRequired("++Ares-Core+release-13.00"), Is.Not.Null);
    }

    [Test]
    public void Registry_RejectsUnsupportedReplayVersionImmediately()
    {
        var registry = PayloadTransformRegistry.CreateDefault();

        Assert.Throws<UnsupportedPayloadTransformVersionException>(() => registry.GetRequired("release-12.12"));
    }

    [Test]
    public void Apply_RejectsTooSmallOutputBuffer()
    {
        var payload = new BitArchiveReader(Convert.FromHexString(PayloadHex), PayloadBits);
        var transform = PayloadTransformRegistry.CreateDefault().GetRequired("++Ares-Core+release-12.10");

        var exception = Assert.Throws<ArchiveReadException>(() =>
            transform.Apply(payload, PayloadBits ^ ActorNetGuid, Span<byte>.Empty));

        Assert.That(exception!.ErrorCode, Is.EqualTo(ArchiveErrorCode.BufferTooSmall));
    }
}
