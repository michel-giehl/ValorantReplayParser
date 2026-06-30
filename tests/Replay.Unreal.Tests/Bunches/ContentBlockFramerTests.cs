using Replay.Encoding.Archives;
using Replay.Encoding.Net;
using Replay.Models.Descriptors;
using Replay.Models.Events;
using Replay.Unreal.Bunches;
using Replay.Unreal.Bunches.Payload;
using Replay.Unreal.Channels;
using Replay.Unreal.PackageMap;
using Replay.Unreal.Parsing;

namespace Replay.Unreal.Tests.Bunches;

public class ContentBlockFramerTests
{
    private const string TestPath = "/Game/Test.Test_C";
    private const string ReplayVersion = "++Ares-Core+release-12.11";

    [Test]
    public void FrameContentBlocks_UnknownClassPath_SkipsWithoutDecoding()
    {
        var netGuidCache = new NetGuidCache();
        var decoder = new CountingPropertyPayloadDecoder();
        var eventSink = new CapturingReplayEventSink();
        var framer = CreateFramer(netGuidCache, new ExportBindingRegistry(), decoder, eventSink);
        var payload = BuildContentBlockPayload(hasRepLayout: true, contentBitCount: 8);
        var stats = new BunchPayloadStats();
        var channel = new ActorChannelState { ActorNetGuid = new NetworkGuid(100) };

        framer.FrameContentBlocks(payload, channel, stats, timeSeconds: 0, packetId: 0, replayVersionBranch: ReplayVersion);

        var exportGroup = eventSink.Events.OfType<ExportGroupReceived>().Single();
        Assert.Multiple(() =>
        {
            Assert.That(decoder.DecodeCount, Is.EqualTo(0));
            Assert.That(stats.ContentPayloadBitsSkipped, Is.EqualTo(8));
            Assert.That(stats.ContentBlockCount, Is.EqualTo(1));
            Assert.That(exportGroup.WasDecoded, Is.False);
            Assert.That(exportGroup.ExportGroupPath, Is.Null);
        });
    }

    [Test]
    public void FrameContentBlocks_DisabledRepLayoutGroup_SkipsWithoutDecoding()
    {
        var netGuidCache = new NetGuidCache();
        var decoder = new CountingPropertyPayloadDecoder();
        var registry = CreateRegistry(ParseProfile.Minimal, netGuidCache);
        var eventSink = new CapturingReplayEventSink();
        var framer = CreateFramer(netGuidCache, registry, decoder, eventSink);
        var payload = BuildContentBlockPayload(hasRepLayout: true, contentBitCount: 8);
        var stats = new BunchPayloadStats();
        var channel = CreateKnownActorChannel();

        framer.FrameContentBlocks(payload, channel, stats, timeSeconds: 0, packetId: 0, replayVersionBranch: ReplayVersion);

        var exportGroup = eventSink.Events.OfType<ExportGroupReceived>().Single();
        Assert.Multiple(() =>
        {
            Assert.That(decoder.DecodeCount, Is.EqualTo(0));
            Assert.That(stats.ContentPayloadBitsSkipped, Is.EqualTo(8));
            Assert.That(stats.ContentBlockCount, Is.EqualTo(1));
            Assert.That(exportGroup.WasDecoded, Is.False);
            Assert.That(exportGroup.ExportGroupPath, Is.EqualTo(TestPath));
        });
    }

    [Test]
    public void FrameContentBlocks_EnabledRepLayoutGroup_DecodesBeforeDispatch()
    {
        var netGuidCache = new NetGuidCache();
        var decoder = new CountingPropertyPayloadDecoder();
        var registry = CreateRegistry(ParseProfile.Default, netGuidCache);
        var eventSink = new CapturingReplayEventSink();
        var framer = CreateFramer(netGuidCache, registry, decoder, eventSink);
        var payload = BuildContentBlockPayload(hasRepLayout: true, contentBitCount: 8);
        var stats = new BunchPayloadStats();
        var channel = CreateKnownActorChannel();

        framer.FrameContentBlocks(payload, channel, stats, timeSeconds: 0, packetId: 0, replayVersionBranch: ReplayVersion);

        var exportGroup = eventSink.Events.OfType<ExportGroupReceived>().Single();
        Assert.Multiple(() =>
        {
            Assert.That(decoder.DecodeCount, Is.EqualTo(1));
            Assert.That(stats.ContentBlockCount, Is.EqualTo(1));
            Assert.That(stats.ContentPayloadBitsParsed, Is.EqualTo(9));
            Assert.That(exportGroup.WasDecoded, Is.True);
            Assert.That(exportGroup.ExportGroupPath, Is.EqualTo(TestPath));
            Assert.That(exportGroup.ActorNetGuid, Is.EqualTo(100));
        });
    }

    private static ContentBlockFramer CreateFramer(
        NetGuidCache netGuidCache,
        ExportBindingRegistry registry,
        IPropertyPayloadDecoder decoder,
        IReplayEventSink eventSink) =>
        new(
            new PackageMapReader(netGuidCache),
            netGuidCache,
            eventSink,
            new FieldPayloadParser(),
            registry,
            decoder);

    private static ActorChannelState CreateKnownActorChannel() => new()
    {
        ActorNetGuid = new NetworkGuid(100),
        ReplicationClassPath = TestPath,
    };

    private static ExportBindingRegistry CreateRegistry(ParseProfile parseProfile, NetGuidCache netGuidCache)
    {
        var catalog = new DescriptorCatalog();
        catalog.Add(new ExportGroupDescriptor(
            TestPath,
            ExportCategory.Debug,
            ExportGroupKind.Actor,
            fields:
            [
                new FieldDescriptor
                {
                    ExportName = "FieldA",
                    PropertyName = "FieldA",
                    Decoder = PrimitiveDecoders.Skip,
                },
            ]));

        var registry = new ExportBindingRegistry(catalog, parseProfile);
        var exportGroup = new NetFieldExportGroup
        {
            PathName = TestPath,
            PathNameIndex = 1,
            NetFieldExports =
            [
                new NetFieldExport
                {
                    Handle = 0,
                    Name = "FieldA",
                    CompatibleChecksum = 0,
                },
            ],
        };
        netGuidCache.AddExportGroup(exportGroup);
        registry.OnExportGroupAdded(exportGroup);

        return registry;
    }

    private static FBitArchive BuildContentBlockPayload(bool hasRepLayout, int contentBitCount)
    {
        var bits = new List<bool>
        {
            hasRepLayout,
            true,
        };

        WriteIntPacked(bits, (uint)contentBitCount);
        for (var i = 0; i < contentBitCount; i++)
        {
            bits.Add((i & 1) == 0);
        }

        return new BitArchiveReader(PackBits(bits), bits.Count);
    }

    private static void WriteIntPacked(List<bool> bits, uint value)
    {
        do
        {
            var nextByte = (byte)((value & 0x7F) << 1);
            value >>= 7;
            if (value != 0)
            {
                nextByte |= 1;
            }

            for (var i = 0; i < 8; i++)
            {
                bits.Add((nextByte & (1 << i)) != 0);
            }
        } while (value != 0);
    }

    private static byte[] PackBits(IReadOnlyList<bool> bits)
    {
        var bytes = new byte[(bits.Count + 7) / 8];
        for (var i = 0; i < bits.Count; i++)
        {
            if (bits[i])
            {
                bytes[i >> 3] |= (byte)(1 << (i & 7));
            }
        }

        return bytes;
    }

    private sealed class CountingPropertyPayloadDecoder : IPropertyPayloadDecoder
    {
        public int DecodeCount { get; private set; }

        public void EnsureSupported(string replayVersion)
        {
        }

        public FBitArchive Decode(FBitArchive payload, uint actorNetGuid, string replayVersion)
        {
            DecodeCount++;
            payload.SkipRemaining();
            return new BitArchiveReader([0x00, 0x00], 9);
        }
    }

    private sealed class CapturingReplayEventSink : IReplayEventSink
    {
        public List<ReplayEvent> Events { get; } = [];

        public void Emit(ReplayEvent replayEvent) => Events.Add(replayEvent);
    }
}