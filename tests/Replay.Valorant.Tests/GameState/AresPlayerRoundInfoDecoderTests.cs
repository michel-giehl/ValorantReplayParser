using Replay.Encoding.Archives;
using Replay.Models.Descriptors;
using Replay.Models.Diagnostics;
using Replay.Unreal.Parsing;
using Replay.Unreal.Readers;
using Replay.Valorant.Descriptors;
using Replay.Valorant.GameState;

namespace Replay.Valorant.Tests.GameState;

public class AresPlayerRoundInfoDecoderTests
{
    [Test]
    public void Decode_DecodesKnownRoundFields()
    {
        var archive = CreateArchive(writer =>
        {
            writer.WriteIntPacked(3);
            writer.WriteIntPacked(2);
            WriteField(writer, 40, payload => payload.WriteInt32(1));
            WriteField(writer, 41, payload => payload.WriteInt32(800));
            WriteField(writer, 42, payload => payload.WriteInt32(3_900));
            WriteField(writer, 43, payload => payload.WriteInt32(1_200));
            WriteField(writer, 44, payload => payload.WriteInt32(4_500));
            writer.WriteIntPacked(0);
            writer.WriteIntPacked(0);
        });
        var context = new FieldDecodeContext();

        var value = Decoder().Decode(ref context, archive);
        var info = ((AresPlayerRoundInfo[])value.ObjectValue!).Single();

        Assert.Multiple(() =>
        {
            Assert.That(info, Is.EqualTo(new AresPlayerRoundInfo(1, 800, 3_900, 1_200, 4_500)));
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
    public void Decode_RejectsTruncatedFieldPayload()
    {
        var archive = CreateArchive(writer =>
        {
            writer.WriteIntPacked(1);
            writer.WriteIntPacked(1);
            writer.WriteIntPacked(42);
            writer.WriteIntPacked(32);
            writer.WriteBits(0xFF, 8);
        });
        var context = new FieldDecodeContext();

        var exception = Assert.Throws<ArchiveReadException>(() => Decoder().Decode(ref context, archive));

        Assert.That(exception!.ErrorCode, Is.EqualTo(ArchiveErrorCode.InvalidBitCount));
    }

    [Test]
    public void Decode_UnknownLayoutFallsBackAndReportsDiagnostic()
    {
        var writer = new BitWriter();
        writer.WriteIntPacked(1);
        writer.WriteIntPacked(1);
        WriteField(writer, 99, payload => payload.WriteBit(true));
        writer.WriteIntPacked(0);
        writer.WriteIntPacked(0);
        var expectedData = writer.ToArray();
        var archive = new BitArchiveReader(expectedData, writer.BitCount);
        var bitCount = archive.BitLength;
        var diagnostics = new ReplayDiagnosticCollector();
        var context = new FieldDecodeContext
        {
            Diagnostics = diagnostics,
            CurrentPacketId = 42,
            CurrentTimeSeconds = 12.5f,
            ChannelIndex = 7,
            ExportGroupPath = "/Script/ShooterGame.OwnerExclusivePlayerInfo",
            FieldName = "RoundInfos",
        };

        var value = Decoder().Decode(ref context, archive);
        var raw = (ValorantRawPayload)value.ObjectValue!;
        var diagnostic = diagnostics.Diagnostics.Single();

        Assert.Multiple(() =>
        {
            Assert.That(raw.TypeName, Is.EqualTo("TArray<FAresPlayerRoundInfo>"));
            Assert.That(raw.BitCount, Is.EqualTo(bitCount));
            Assert.That(raw.Data.ToArray(), Is.EqualTo(expectedData));
            Assert.That(archive.AtEnd, Is.True);
            Assert.That(diagnostics.Status, Is.EqualTo(ReplayReadStatus.CompletedWithWarnings));
            Assert.That(diagnostics.TotalDiagnosticCount, Is.EqualTo(1));
            Assert.That(diagnostic.Code, Is.EqualTo(ReplayDiagnosticCode.RawPayloadFallback));
            Assert.That(diagnostic.Message, Does.Contain("field handle 99"));
            Assert.That(diagnostic.PacketId, Is.EqualTo(42));
            Assert.That(diagnostic.ChannelIndex, Is.EqualTo(7));
            Assert.That(diagnostic.TimeSeconds, Is.EqualTo(12.5f));
            Assert.That(diagnostic.ExportGroupPath, Is.EqualTo(context.ExportGroupPath));
            Assert.That(diagnostic.FieldName, Is.EqualTo(context.FieldName));
        });
    }

    private static IFieldDecoder Decoder() =>
        (IFieldDecoder)ValorantDescriptors.CreateCatalog()
            .ExportGroupDescriptors
            .Single(descriptor => descriptor.Path == "/Script/ShooterGame.OwnerExclusivePlayerInfo")
            .Fields
            .Single(field => field.ExportName == "RoundInfos")
            .Decoder!;

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
