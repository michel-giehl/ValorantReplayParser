using Replay.Encoding.Archives;

namespace Replay.Encoding.Tests.Archives;

public class ByteReaderTests
{
    [Test]
    public void ReadsLittleEndianPrimitiveValues()
    {
        var reader = new ByteArchiveReader([
            0x34, 0x12,
            0x78, 0x56, 0x34, 0x12,
            0x00, 0x00, 0x80, 0x3F
        ]);

        Assert.That(reader.ReadUInt16(), Is.EqualTo(0x1234));
        Assert.That(reader.ReadUInt32(), Is.EqualTo(0x12345678));
        Assert.That(reader.ReadSingle(), Is.EqualTo(1.0f));
        Assert.That(reader.Position, Is.EqualTo(10));
        Assert.That(reader.AtEnd, Is.True);
    }

    [Test]
    public void ReadIntPacked_DecodesUnrealPackedIntegerBytes()
    {
        var reader = new ByteArchiveReader([0x09, 0x06]);

        Assert.That(reader.ReadIntPacked(), Is.EqualTo(388u));
        Assert.That(reader.AtEnd, Is.True);
    }

    [Test]
    public void BoundsFailure_ThrowsAndDoesNotAdvance()
    {
        var reader = new ByteArchiveReader([0x01]);

        var exception = Assert.Throws<ArchiveReadException>(() => reader.ReadUInt32());

        Assert.That(exception!.ErrorCode, Is.EqualTo(ArchiveErrorCode.EndOfArchive));
        Assert.That(reader.Position, Is.EqualTo(0));
    }

    [Test]
    public void TryReadFailure_DoesNotAdvance()
    {
        var reader = new ByteArchiveReader([0x01]);

        Assert.That(reader.TryReadUInt32(out _), Is.False);
        Assert.That(reader.Position, Is.EqualTo(0));
    }

    [Test]
    public void SeekAndSkip_UseBytePositions()
    {
        var reader = new ByteArchiveReader([0x10, 0x20, 0x30, 0x40]);

        reader.Skip(2);
        Assert.That(reader.ReadByte(), Is.EqualTo(0x30));

        reader.Seek(1);
        Assert.That(reader.ReadByte(), Is.EqualTo(0x20));

        reader.Seek(1, SeekOrigin.End);
        Assert.That(reader.ReadByte(), Is.EqualTo(0x40));
    }
}
