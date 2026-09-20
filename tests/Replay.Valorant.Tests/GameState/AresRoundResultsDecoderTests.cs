using Replay.Encoding.Archives;
using Replay.Models.Diagnostics;
using Replay.Models.Replay;
using Replay.Unreal.Parsing;
using Replay.Unreal.Readers;
using Replay.Valorant.Descriptors;
using Replay.Valorant.GameState;

namespace Replay.Valorant.Tests.GameState;

public class AresRoundResultsDecoderTests
{
    [Test]
    public void Decode_EmitsRoundIndexAndWinnerFields()
    {
        var archive = CreateArchive(writer =>
        {
            writer.WriteIntPacked(3);
            writer.WriteIntPacked(3);
            WriteField(writer, 93, payload => payload.WriteFName("Blue"));
            WriteField(writer, 94, payload => payload.WriteBits((byte)AresTeamRole.Defender, 3));
            WriteField(writer, 95, payload => payload.WriteBits((byte)AresRoundOutcome.Defuse, 4));
            writer.WriteIntPacked(0);
            writer.WriteIntPacked(0);
        });
        var context = new FieldDecodeContext();

        var value = Decoder(new ReplayReleaseVersion(13, 4)).Decode(ref context, archive);
        var results = (AresRoundResult[])value.ObjectValue!;

        Assert.Multiple(() =>
        {
            Assert.That(results, Has.Length.EqualTo(1));
            Assert.That(results[0].RoundNumber, Is.EqualTo(2));
            Assert.That(results[0].WinningTeam, Is.EqualTo("Blue"));
            Assert.That(results[0].WinningTeamRole, Is.EqualTo(AresTeamRole.Defender));
            Assert.That(results[0].RoundResult, Is.EqualTo(AresRoundOutcome.Defuse));
            Assert.That(archive.AtEnd, Is.True);
        });
    }

    [TestCase(
        "0202BCD20A00000084D8EACA00000000007C0D048C00C2800202C420D90400000000",
        272,
        0,
        "Blue",
        AresTeamRole.Defender,
        AresRoundOutcome.Elimination)]
    [TestCase(
        "0606BCC208000000A4CAC800000000007C0D028C200000",
        184,
        2,
        "Red",
        AresTeamRole.Attacker,
        AresRoundOutcome.Detonate)]
    public void Decode_DecodesRelease1301ReplayPayload(
        string hex,
        int bitCount,
        int roundNumber,
        string winningTeam,
        AresTeamRole winningTeamRole,
        AresRoundOutcome roundResult)
    {
        var archive = new BitArchiveReader(Convert.FromHexString(hex), bitCount);
        var context = new FieldDecodeContext();

        var value = Decoder().Decode(ref context, archive);
        var result = ((AresRoundResult[])value.ObjectValue!).Single();

        Assert.That(
            result,
            Is.EqualTo(new AresRoundResult(roundNumber, winningTeam, winningTeamRole, roundResult)));
        Assert.That(archive.AtEnd, Is.True);
    }

    [Test]
    public void Decode_DecodesRelease1305HandleLayout()
    {
        var archive = CreateArchive(writer =>
        {
            writer.WriteIntPacked(20);
            writer.WriteIntPacked(8);
            WriteField(writer, 82, payload => payload.WriteFName("Red"));
            WriteField(writer, 83, payload => payload.WriteBits((byte)AresTeamRole.Attacker, 3));
            WriteField(writer, 84, payload => payload.WriteBits((byte)AresRoundOutcome.Detonate, 4));
            WriteField(writer, 85, payload => payload.WriteBits(0xA5, 8));
            writer.WriteIntPacked(0);
            writer.WriteIntPacked(0);
        });
        var release = new ReplayReleaseVersion(13, 5);
        var context = new FieldDecodeContext { ReplayReleaseVersion = release };

        var value = Decoder(release).Decode(ref context, archive);
        var result = ((AresRoundResult[])value.ObjectValue!).Single();

        Assert.Multiple(() =>
        {
            Assert.That(result, Is.EqualTo(new AresRoundResult(
                7,
                "Red",
                AresTeamRole.Attacker,
                AresRoundOutcome.Detonate)));
            Assert.That(archive.AtEnd, Is.True);
        });
    }

    [Test]
    public void Decode_RejectsRoundCountAboveBound()
    {
        var archive = CreateArchive(writer => writer.WriteIntPacked(129));
        var context = new FieldDecodeContext();

        var exception = Assert.Throws<ArchiveReadException>(() => Decoder().Decode(ref context, archive));

        Assert.That(exception!.ErrorCode, Is.EqualTo(ArchiveErrorCode.InvalidCount));
    }

    [Test]
    public void Decode_RejectsTrailingBitsInsideKnownField()
    {
        var archive = CreateArchive(writer =>
        {
            writer.WriteIntPacked(1);
            writer.WriteIntPacked(1);
            WriteField(writer, 93, payload =>
            {
                payload.WriteFName("Red");
                payload.WriteBit(true);
            });
            writer.WriteIntPacked(0);
            writer.WriteIntPacked(0);
        });
        var context = new FieldDecodeContext();

        var exception = Assert.Throws<ArchiveReadException>(() => Decoder().Decode(ref context, archive));

        Assert.That(exception!.ErrorCode, Is.EqualTo(ArchiveErrorCode.UnexpectedTrailingData));
    }

    [Test]
    public void Decode_Release1305MalformedKnownFieldRemainsFatal()
    {
        var archive = CreateArchive(writer =>
        {
            writer.WriteIntPacked(1);
            writer.WriteIntPacked(1);
            WriteField(writer, 82, payload =>
            {
                payload.WriteFName("Red");
                payload.WriteBit(true);
            });
            writer.WriteIntPacked(0);
            writer.WriteIntPacked(0);
        });
        var diagnostics = new ReplayDiagnosticCollector();
        var release = new ReplayReleaseVersion(13, 5);
        var context = new FieldDecodeContext
        {
            Diagnostics = diagnostics,
            ReplayReleaseVersion = release,
        };

        var exception = Assert.Throws<ArchiveReadException>(() =>
            Decoder(release).Decode(ref context, archive));

        Assert.Multiple(() =>
        {
            Assert.That(exception!.ErrorCode, Is.EqualTo(ArchiveErrorCode.UnexpectedTrailingData));
            Assert.That(diagnostics.TotalDiagnosticCount, Is.Zero);
        });
    }

    [Test]
    public void Decode_UnknownHandleAfterKnownField_RollsBackAndPreservesExactRawPayload()
    {
        var writer = new BitWriter();
        writer.WriteIntPacked(1);
        writer.WriteIntPacked(1);
        WriteField(writer, 82, payload => payload.WriteFName("Blue"));
        WriteField(writer, 99, payload => payload.WriteBit(true));
        writer.WriteIntPacked(0);
        writer.WriteIntPacked(0);
        var expectedData = writer.ToArray();
        var archive = new BitArchiveReader(expectedData, writer.BitCount);
        var bitCount = archive.BitLength;
        var diagnostics = new ReplayDiagnosticCollector();
        var release = new ReplayReleaseVersion(13, 5);
        var context = new FieldDecodeContext
        {
            Diagnostics = diagnostics,
            ReplayReleaseVersion = release,
            FieldName = "RoundResults",
        };

        var value = Decoder(release).Decode(ref context, archive);
        var raw = (ValorantRawPayload)value.ObjectValue!;
        var diagnostic = diagnostics.Diagnostics.Single();

        Assert.Multiple(() =>
        {
            Assert.That(raw.TypeName, Is.EqualTo("TArray<FAresRoundResult>"));
            Assert.That(raw.BitCount, Is.EqualTo(bitCount));
            Assert.That(raw.Data.ToArray(), Is.EqualTo(expectedData));
            Assert.That(raw.BitCount % 8, Is.Not.Zero);
            Assert.That(archive.AtEnd, Is.True);
            Assert.That(diagnostics.TotalDiagnosticCount, Is.EqualTo(1));
            Assert.That(diagnostic.Code, Is.EqualTo(ReplayDiagnosticCode.RawPayloadFallback));
            Assert.That(diagnostic.Message, Does.Contain("release 13.05"));
            Assert.That(diagnostic.Message, Does.Contain("layout '13.05'"));
            Assert.That(diagnostic.Message, Does.Contain("field handle 99"));
        });
    }

    private static IFieldDecoder Decoder() =>
        (IFieldDecoder)ValorantDescriptors.CreateCatalog()
            .ExportGroupDescriptors
            .Single(descriptor => descriptor.Path == "/Game/GameModes/Bomb/BombGameState.BombGameState_C")
            .Fields
            .Single(field => field.ExportName == "RoundResults")
            .Decoder!;

    private static IFieldDecoder Decoder(ReplayReleaseVersion release)
    {
        var field = ValorantDescriptors.CreateCatalog()
            .ExportGroupDescriptors
            .Single(descriptor => descriptor.Path == "/Game/GameModes/Bomb/BombGameState.BombGameState_C")
            .Fields
            .Single(field => field.ExportName == "RoundResults");
        return (IFieldDecoder)field.DecoderDefinition!.Resolve(release);
    }

    private static BitArchiveReader CreateArchive(Action<BitWriter> write)
    {
        var writer = new BitWriter();
        write(writer);
        return new BitArchiveReader(writer.ToArray(), writer.BitCount);
    }

    private static void WriteField(BitWriter writer, uint handle, Action<BitWriter> writePayload)
    {
        var payload = new BitWriter();
        writePayload(payload);
        writer.WriteIntPacked(handle + 1);
        writer.WriteIntPacked((uint)payload.BitCount);
        writer.WriteBits(payload.ToArray(), payload.BitCount);
    }

    private sealed class BitWriter
    {
        private readonly List<bool> _bits = [];

        public int BitCount => _bits.Count;

        public void WriteBit(bool value) => _bits.Add(value);

        public void WriteBits(byte value, int bitCount)
        {
            for (var i = 0; i < bitCount; i++) WriteBit((value & (1 << i)) != 0);
        }

        public void WriteBits(byte[] bytes, int bitCount)
        {
            for (var i = 0; i < bitCount; i++) WriteBit((bytes[i >> 3] & (1 << (i & 7))) != 0);
        }

        public void WriteFName(string value)
        {
            WriteBit(false);
            var bytes = System.Text.Encoding.UTF8.GetBytes(value + '\0');
            WriteInt32(bytes.Length);
            WriteBits(bytes, bytes.Length * 8);
            WriteInt32(0);
        }

        public void WriteInt32(int value)
        {
            foreach (var item in BitConverter.GetBytes(value)) WriteBits(item, 8);
        }

        public void WriteIntPacked(uint value)
        {
            do
            {
                var next = (byte)((value & 0x7F) << 1);
                value >>= 7;
                if (value != 0) next |= 1;
                WriteBits(next, 8);
            } while (value != 0);
        }

        public byte[] ToArray()
        {
            var bytes = new byte[(_bits.Count + 7) / 8];
            for (var i = 0; i < _bits.Count; i++)
            {
                if (_bits[i]) bytes[i >> 3] |= (byte)(1 << (i & 7));
            }

            return bytes;
        }
    }
}
