using System.Text.Json;
using Replay.Encoding.Archives;
using Replay.Models.Descriptors;
using Replay.Models.Net;
using Replay.Models.Replay;
using Replay.Unreal.Parsing;
using Replay.Valorant.Descriptors;
using Replay.Valorant.GameState;
using Replay.Valorant.Movement;

namespace Replay.Valorant.Tests.Descriptors;

public sealed class DescriptorCoveragePayloadTests
{
    public sealed record RecordedField(string Path, uint Handle, string Field, string Replay, string? Release, int Bits, string Data);

    private static RecordedField[] ReadFixtures(string file) => JsonSerializer.Deserialize<RecordedField[]>(
        File.ReadAllText(Path.Combine(TestContext.CurrentContext.TestDirectory,
            "Fixtures", "DescriptorCoverage", file)), new JsonSerializerOptions { PropertyNameCaseInsensitive = true })!;

    public static IEnumerable<TestCaseData> Fields() => ReadFixtures("fields.json").Select((field, index) =>
        new TestCaseData(field).SetName($"RecordedCoverage_{index}_{field.Field}_{field.Release}"));

    [TestCaseSource(nameof(Fields))]
    public void RecordedFieldReturnsTypedValueAndConsumesEveryBit(RecordedField fixture)
    {
        var releaseParts = fixture.Release!.Split('.');
        var release = new ReplayReleaseVersion(ushort.Parse(releaseParts[0]), ushort.Parse(releaseParts[1]));
        var registry = new ExportBindingRegistry(ValorantDescriptors.CreateCatalog(), releaseVersion: release);
        var exports = new NetFieldExport?[fixture.Handle + 1];
        exports[fixture.Handle] = new NetFieldExport { Handle = fixture.Handle, Name = fixture.Field, CompatibleChecksum = 0 };
        registry.OnExportGroupAdded(new NetFieldExportGroup
        {
            PathName = fixture.Path, PathNameIndex = 1, NetFieldExports = exports,
        });
        var group = registry.GetBoundGroup(fixture.Path)!;
        var binding = group.FieldsByHandle[fixture.Handle];
        Assert.That(binding.Decoder, Is.Not.Null, fixture.Path);
        using var archive = new BitArchiveReader(Convert.FromBase64String(fixture.Data), fixture.Bits);
        var context = new FieldDecodeContext { ReplayReleaseVersion = release, FieldName = fixture.Field };
        var value = binding.Decoder!.Decode(ref context, archive);
        var payload = (ExportGroupDescriptor)group.SourceDescriptor.CreatePayloadInstance();
        DecodedValueAssigner.Assign(payload, binding, value);
        Assert.Multiple(() =>
        {
            Assert.That(value.HasValue, Is.True, fixture.Path);
            Assert.That(value.ObjectValue, Is.Not.InstanceOf<ValorantRawPayload>());
            Assert.That(value.ObjectValue, Is.Not.InstanceOf<ValorantCapturedPayload>());
            Assert.That(archive.AtEnd, Is.True, $"{fixture.Path}:{fixture.Field}");
            Assert.That(payload.HasDecoded(binding.Name!), Is.True, "The typed value must reach its DTO property.");
        });
    }

    [Test]
    public void RecordedComponentStreamsDecodeTheirNativeFramingAndComponentHeaders()
    {
        foreach (var fixture in ReadFixtures("components.json"))
        {
            using var archive = new BitArchiveReader(Convert.FromBase64String(fixture.Data), fixture.Bits);
            var data = ComponentDataStream.Decode(archive);
            Assert.Multiple(() =>
            {
                Assert.That(data.PayloadByteCount, Is.EqualTo(394));
                Assert.That(data.UnusedBitCount, Is.EqualTo(5));
                Assert.That(data.MoveCount, Is.EqualTo(16));
                Assert.That(data.HasInputSection, Is.True);
                Assert.That(data.HasPlayerDiscrepancySection, Is.True);
                Assert.That(data.InputEndBit, Is.EqualTo(3139));
                Assert.That(archive.AtEnd, Is.True);
            });
            using var truncated = new BitArchiveReader(Convert.FromBase64String(fixture.Data), fixture.Bits - 1);
            Assert.Throws<ArchiveReadException>(() => ComponentDataStream.Decode(truncated));
        }
    }

    [Test]
    public void CombatReportRetainsPreviouslyDiscardedValues()
    {
        var fixture = ReadFixtures("fields.json").First(field => field.Field == "Rounds" && field.Bits == 1890);
        using var archive = new BitArchiveReader(Convert.FromBase64String(fixture.Data), fixture.Bits);
        var context = new FieldDecodeContext();
        var rounds = (CombatReportArrayUpdate<CombatRoundReportUpdate>)new CombatRoundReportsDecoder().Decode(ref context, archive).ObjectValue!;
        var participant = rounds.Updates.Single().Reports!.Updates.Single().Interactions!.Updates.Single();
        Assert.Multiple(() =>
        {
            Assert.That(participant.LastInteractionTime!.GameTime, Is.Not.Null);
            Assert.That(participant.DealtInteractions!.Updates.Single().DamageType, Is.GreaterThan(0));
            Assert.That(participant.DealtInteractions!.Updates.Single().StartTime, Is.Not.Null);
            Assert.That(participant.DealtInteractions!.Updates.Single().Regions!.Updates.Single().Hits, Is.GreaterThan(0));
        });
    }

    [Test]
    public void ObfuscatedPlayerArrayPreservesAbsentEntriesAndDecodedFalse()
    {
        using var archive = new BitArchiveReader(Convert.FromBase64String("BAQ+MQL4BQAAAFJpb3QABQAAAHRlc3QAQAKECAYAAA=="), 243);
        var context = new FieldDecodeContext();
        var values = (ObfuscatedPlayerInformation?[])RepLayoutArrayDecoders.DynamicArray<ObfuscatedPlayerInformation>()
            .Decode(ref context, archive).ObjectValue!;
        Assert.Multiple(() =>
        {
            Assert.That(values, Has.Length.EqualTo(2));
            Assert.That(values[0], Is.Null);
            Assert.That(values[1]!.SubjectUniqueId, Is.EqualTo(new RiotUniqueNetId("Riot", "test")));
            Assert.That(values[1]!.IsAfk, Is.False);
            Assert.That(values[1]!.HasDecoded(nameof(ObfuscatedPlayerInformation.IsAfk)), Is.True);
            Assert.That(values[1]!.ConnectionStatus, Is.EqualTo(3));
            Assert.That(archive.AtEnd, Is.True);
        });
    }

    [TestCase(0)]
    [TestCase(0xF9)]
    public void RiotUniqueIdRejectsUnsupportedEncoding(int flags)
    {
        using var archive = new BitArchiveReader(new[] { checked((byte)flags) });
        var context = new FieldDecodeContext();
        var error = Assert.Throws<ArchiveReadException>(() => RiotUniqueNetIdDecoder.Instance.Decode(ref context, archive));
        Assert.That(error!.ErrorCode, Is.EqualTo(ArchiveErrorCode.InvalidData));
    }

    [Test]
    public void RiotUniqueIdRejectsTruncatedStringAndUnsupportedType()
    {
        var fixture = ReadFixtures("fields.json").First(f => f.Field == "UniqueId");
        var bytes = Convert.FromBase64String(fixture.Data);
        var context = new FieldDecodeContext();
        using var truncated = new BitArchiveReader(bytes, fixture.Bits - 1);
        Assert.Throws<ArchiveReadException>(() => RiotUniqueNetIdDecoder.Instance.Decode(ref context, truncated));
        bytes[5] = (byte)'X';
        using var unknownType = new BitArchiveReader(bytes, fixture.Bits);
        Assert.Throws<ArchiveReadException>(() => RiotUniqueNetIdDecoder.Instance.Decode(ref context, unknownType));
    }

    [TestCase(4, "AgL0QAcAAAD2cAIC+BBUAAAAAA==", 152)]
    [TestCase(5, "AgL8QAcAAAD+gAICAQIQVAAAAAA=", 160)]
    [TestCase(8, "AgLWQAcAAADYcAIC2hBUAAAAAA==", 152)]
    [TestCase(9, "AgLkQAcAAADmcAIC6BBUAAAAAA==", 152)]
    public void RoundParticipantsSelectsObservedReleaseHandles(int minor, string data, int bits)
    {
        var release = new ReplayReleaseVersion(13, checked((ushort)minor));
        var field = new BombGameStateDescriptor().Fields.Single(f => f.PropertyName == "RoundParticipantsInfos");
        using var archive = new BitArchiveReader(Convert.FromBase64String(data), bits);
        var context = new FieldDecodeContext { ReplayReleaseVersion = release };
        var values = (RoundParticipantsInfo?[])((IFieldDecoder)field.Decoder!).Decode(ref context, archive).ObjectValue!;
        Assert.Multiple(() =>
        {
            Assert.That(values.Single()!.RoundNumber, Is.EqualTo(7));
            Assert.That(values.Single()!.PenalizedPlayers!.Single()!.NetGuid?.NetGuid, Is.EqualTo(42));
            Assert.That(archive.AtEnd, Is.True);
        });
    }

    [TestCase(-1)]
    [TestCase(8193)]
    public void ComponentStreamRejectsInvalidAllocationCount(int count)
    {
        using var archive = new BitArchiveReader(BitConverter.GetBytes(count));
        var error = Assert.Throws<ArchiveReadException>(() => ComponentDataStream.Decode(archive));
        Assert.That(error!.ErrorCode, Is.EqualTo(ArchiveErrorCode.InvalidData));
    }

    [Test]
    public void ComponentStreamRejectsInvalidPaddingAndComponentMagic()
    {
        var fixture = ReadFixtures("components.json")[0];
        var bytes = Convert.FromBase64String(fixture.Data);
        bytes[^1] = 8;
        using var invalidPadding = new BitArchiveReader(bytes, fixture.Bits);
        Assert.Throws<ArchiveReadException>(() => ComponentDataStream.Decode(invalidPadding));
        bytes[^1] = 5;
        // 32-bit byte count + 3115 movement bits + 16-bit absolute input boundary.
        const int inputMagicBit = 32 + 3115 + 16;
        bytes[inputMagicBit / 8] ^= 1 << (inputMagicBit % 8);
        using var invalidMagic = new BitArchiveReader(bytes, fixture.Bits);
        var error = Assert.Throws<ArchiveReadException>(() => ComponentDataStream.Decode(invalidMagic));
        Assert.That(error!.ErrorCode, Is.EqualTo(ArchiveErrorCode.InvalidData));
    }
}
