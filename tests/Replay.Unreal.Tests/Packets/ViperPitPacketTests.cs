using Replay.Models.Net;
using Replay.Unreal.Packets;

namespace Replay.Unreal.Tests.Packets;

public sealed class ViperPitPacketTests
{
    [Test]
    public void RecordedPitVolume_PartialHeadersCompleteWithoutDroppingFragments()
    {
        var reader = new RawPacketReader();
        var headers = new List<RawBunchHeader>();
        foreach (var packetId in Enumerable.Range(2069, 10))
        {
            var packet = File.ReadAllBytes(Path.Combine(TestContext.CurrentContext.TestDirectory,
                "Fixtures", "ViperPit", $"packet-{packetId}.bin"));
            var result = reader.ReadPacket(packet, packetId, (ref header, _) => headers.Add(header));
            Assert.That(result.IsMalformed, Is.False);
            Assert.That(result.PartialErrorCount, Is.Zero);
        }
        Assert.Multiple(() =>
        {
            Assert.That(headers, Has.Count.EqualTo(10));
            Assert.That(headers.All(h => h.bPartial && h.ChIndex == 54), Is.True);
            Assert.That(headers[0].bPartialInitial, Is.True);
            Assert.That(headers.Skip(1).All(h => !h.bPartialInitial), Is.True);
            Assert.That(headers[^1].bPartialFinal, Is.True);
            Assert.That(headers[^1].IsPartialCompleted, Is.True);
            Assert.That(headers.Count(h => h.IsPartialCompleted), Is.EqualTo(1));
            Assert.That(headers.Sum(h => h.PayloadBitCount), Is.EqualTo(148453));
        });
    }
}
