using Replay.Encoding.Archives;
using Replay.Models.Replay;
using Replay.Unreal.Parsing;
using Replay.Valorant.Descriptors;
using Replay.Valorant.GameState;

namespace Replay.Valorant.Tests.GameState;

public class CombatRoundReportsDecoderTests
{
    [TestCase("0000", 0)]
    [TestCase("0A00", 5)]
    public void Decode_PreservesDeclaredLengthWhenNoElementsChanged(string hex, int declaredCount)
    {
        using var archive = new BitArchiveReader(Convert.FromHexString(hex));
        var context = new FieldDecodeContext();
        var update = (CombatReportArrayUpdate<CombatRoundReportUpdate>)Decoder().Decode(ref context, archive).ObjectValue!;
        Assert.Multiple(() =>
        {
            Assert.That(update.DeclaredCount, Is.EqualTo(declaredCount));
            Assert.That(update.Updates, Is.Empty);
            Assert.That(archive.AtEnd, Is.True);
        });
    }

    [TestCase("02020000", false)]
    [TestCase("02020A2000000000", true)]
    public void Decode_DistinguishesAbsentReportsFromDecodedEmptyArray(string hex, bool hasReports)
    {
        using var archive = new BitArchiveReader(Convert.FromHexString(hex));
        var context = new FieldDecodeContext();
        var update = (CombatReportArrayUpdate<CombatRoundReportUpdate>)Decoder().Decode(ref context, archive).ObjectValue!;
        var round = update.Updates.Single();
        Assert.That(round.Reports is not null, Is.EqualTo(hasReports));
        if (hasReports) Assert.That(round.Reports!.DeclaredCount, Is.Zero);
        Assert.That(archive.AtEnd, Is.True);
    }

    [Test]
    public void Decode_DecodesRelease1301RoundAndCharacterEnvelope()
    {
        const string payload =
            "AgIIQAAAAAAKMwwCAgxAAAAAAA4gNyIQQAAAgL8SQAAAgL8UEP7GEADIID8cykAE8O9BzEAAAAAAzhAC0AKkAwUAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAACoQW5ErIEAAAB/sYEAAAB/tSH8AQAAAAA=";
        var archive = new BitArchiveReader(Convert.FromBase64String(payload), 897);
        var context = new FieldDecodeContext();

        var value = Decoder().Decode(ref context, archive);
        var rounds = (CombatReportArrayUpdate<CombatRoundReportUpdate>)value.ObjectValue!;
        var report = rounds.Updates.Single().Reports!.Updates.Single();

        Assert.Multiple(() =>
        {
            Assert.That(rounds.Updates.Single().RoundNumber, Is.Zero);
            Assert.That(report.RoundNumber, Is.Zero);
            Assert.That(report.Died, Is.False);
            Assert.That(archive.AtEnd, Is.True);
        });
    }

    [Test]
    public void Decode_DecodesParticipantDamageUpdates()
    {
        const string payload =
            "AgIKVRwCAg4gcy4QQG6awUISQAAAQEIUEAQWtRgCAhiRBCUAAAA3YmVhMzNjMC02N2JhLTViODgtYjY1NS01ODI2ZTcwODkzZDMAGsIIAAAApMrIAAAAAAA4QJZgPEDmXECA3DSDhUSAAACAhEggCEyAADggglCAAgAAAFSAAAAAAFiAAAAAAFwEwCgAGQgAGgE2tQgCAjggcy46QG6awUI8QAAAQEI+EARAIHMuQkBumsFCREAAAEBCRhAESCArMFqlAgICXBAAXkABAAAAYEAAHBBBYgLIBJhBAAAAAAAIAwUAAAAAAAAAAAAA";
        var archive = new BitArchiveReader(Convert.FromBase64String(payload), 1890);
        var context = new FieldDecodeContext();

        var value = Decoder().Decode(ref context, archive);
        var participants = ((CombatReportArrayUpdate<CombatRoundReportUpdate>)value.ObjectValue!)
            .Updates.SelectMany(round => round.Reports?.Updates ?? [])
            .SelectMany(report => report.Interactions?.Updates ?? [])
            .ToArray();

        Assert.That(participants, Is.Not.Empty);
        Assert.That(participants.Any(item => item.Subject is not null), Is.True);
        Assert.That(archive.AtEnd, Is.True);
    }

    // Recorded 13.10 payloads: DamageIcon follows DamageType, moving RegionalDamageInteractions and every later handle.
    [TestCase(
        "AgIKVQ4CAhBAK6PmQRaFDAYGIEAro+ZBJkAAANBBKEABAAAANvUIAgI4IE9MOkAro+ZBPEAAAJZCPhAEQCBPTEJAK6PmQURAAACWQkYQBEggn0pKIL9KXKUCAgJeEABgQAEAAABiQAAA0EFkAswEoEEAAAAAAAAAAAAAAAA=",
        true)]
    [TestCase(
        "AgIKVQ4CAhBAK6PmQRaFDAQEIEAro+ZBKkAAANBBLEABAAAAfvUIAgKAIE9MgkAro+ZBhEAAAJZChhAEiCBPTIpAK6PmQYxAAACWQo4QBJAgn0qSIL9KpKUCAgKmEACoQAEAAACqQAAA0EGsAlwFwEIAAAAAAAAAAAAAAAA=",
        false)]
    public void Decode_Release1310DecodesDamageIconAndShiftedRegions(string payload, bool dealt)
    {
        var archive = new BitArchiveReader(Convert.FromBase64String(payload), 994);
        var context = new FieldDecodeContext();

        var value = Decoder(new ReplayReleaseVersion(13, 10)).Decode(ref context, archive);
        var participant = ((CombatReportArrayUpdate<CombatRoundReportUpdate>)value.ObjectValue!)
            .Updates.Single().Reports!.Updates.Single().Interactions!.Updates.Single();
        var interaction = (dealt ? participant.DealtInteractions : participant.ReceivedInteractions)!.Updates.Single();
        var region = interaction.Regions!.Updates.Single();

        Assert.Multiple(() =>
        {
            Assert.That(dealt ? participant.ReceivedInteractions : participant.DealtInteractions, Is.Null);
            Assert.That(interaction.DamageType, Is.EqualTo(4815));
            Assert.That(interaction.DamageIcon, Is.EqualTo(4831));
            Assert.That(interaction.StartTime, Is.Not.Null);
            Assert.That(interaction.EndTime, Is.Not.Null);
            Assert.That(region.Region, Is.Zero);
            Assert.That(region.Hits, Is.EqualTo(1));
            Assert.That(region.Damage, Is.EqualTo(26f));
            Assert.That(region.IsKill, Is.False);
            Assert.That(archive.AtEnd, Is.True);
        });
    }

    [Test]
    public void Decode_Release1310DecodesShiftedCharacterEnvelope()
    {
        const string payload =
            "AgIIQAAAAAAKIwwCAgxAAAAAAA4gqzIQQAAAgL8SQAAAgL8UEP7KEADMEELOQP/v70HQQAAAAADSEALUAqwDBQAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAALBBVmW0gQAAAH+5gQAAAH+9IfwBAAAAAA==";
        var archive = new BitArchiveReader(Convert.FromBase64String(payload), 889);
        var context = new FieldDecodeContext();

        var value = Decoder(new ReplayReleaseVersion(13, 10)).Decode(ref context, archive);
        var report = ((CombatReportArrayUpdate<CombatRoundReportUpdate>)value.ObjectValue!)
            .Updates.Single().Reports!.Updates.Single();

        Assert.Multiple(() =>
        {
            Assert.That(report.RoundNumber, Is.Zero);
            Assert.That(report.ResurrectorPlayerState, Is.Zero);
            Assert.That(report.RespawnTime, Is.EqualTo(new CombatTimestamp(33, 29.992186f, 0f, 2)));
            Assert.That(report.Died, Is.False);
            Assert.That(report.DeathLocation, Is.Not.Null);
            Assert.That(report.DeathTime, Is.EqualTo(new CombatTimestamp(3285, -1f, -1f, 254)));
            Assert.That(archive.AtEnd, Is.True);
        });
    }

    [Test]
    public void Decode_Release1309LayoutRejectsRelease1310Payload()
    {
        const string payload =
            "AgIKVQ4CAhBAK6PmQRaFDAYGIEAro+ZBJkAAANBBKEABAAAANvUIAgI4IE9MOkAro+ZBPEAAAJZCPhAEQCBPTEJAK6PmQURAAACWQkYQBEggn0pKIL9KXKUCAgJeEABgQAEAAABiQAAA0EFkAswEoEEAAAAAAAAAAAAAAAA=";
        var archive = new BitArchiveReader(Convert.FromBase64String(payload), 994);
        var decoder = Decoder(new ReplayReleaseVersion(13, 9));

        Assert.Throws<ArchiveReadException>(() =>
        {
            var context = new FieldDecodeContext();
            decoder.Decode(ref context, archive);
        });
    }

    private static IFieldDecoder Decoder() => new CombatRoundReportsDecoder();

    private static IFieldDecoder Decoder(ReplayReleaseVersion release)
    {
        var field = ValorantDescriptors.CreateCatalog()
            .ExportGroupDescriptors
            .Single(descriptor => descriptor.Path == "/Game/GameModes/Bomb/Bomb_CombatReportComponent.Bomb_CombatReportComponent_C")
            .Fields
            .Single(field => field.ExportName == "Rounds");
        return (IFieldDecoder)field.DecoderDefinition!.Resolve(release);
    }
}
