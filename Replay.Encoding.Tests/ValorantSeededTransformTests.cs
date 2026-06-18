using Replay.Encoding.Archives;
using Replay.Encoding.PayloadEncryption;

namespace Replay.Encoding.Tests;

public class ValorantSeededTransformTests
{
    private const string PayloadHex = "BFDF6F9EA1F27BA00000C66EAFAF2E0000339C0DD34B0C45C48063038003562A43C0C949";
    private const string TransformedHex = "100CA461300F080493400100000040394E5120000000B0792C626000000080FE7F3C2000";
    private const int PayloadBits = 287;
    private const uint ActorNetGuid = 2;

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

    [Test]
    public void Registry_ResolvesExactReplayVersion()
    {
        var registry = PayloadTransformRegistry.CreateDefault();

        Assert.That(registry.GetRequired("++Ares-Core+release-12.10"), Is.Not.Null);
        Assert.That(registry.GetRequired("++Ares-Core+release-12.11"), Is.Not.Null);
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
