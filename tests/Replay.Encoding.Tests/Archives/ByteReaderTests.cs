using System.Buffers;
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
            0x00, 0x00, 0x80, 0x3F,
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

        reader.Seek(-1, SeekOrigin.End);
        Assert.That(reader.ReadByte(), Is.EqualTo(0x40));
    }

    [Test]
    public void OwnerBackedReader_DisposeIsIdempotentAndRejectsFurtherAccess()
    {
        var owner = new CountingMemoryOwner([0x10, 0x20]);
        var reader = new ByteArchiveReader(owner, 2);

        reader.Dispose();
        reader.Dispose();

        Assert.Multiple(() =>
        {
            Assert.That(owner.DisposeCount, Is.EqualTo(1));
            Assert.Throws<ObjectDisposedException>(() => reader.ReadByte());
            Assert.Throws<ObjectDisposedException>(() => reader.TryReadByte(out _));
            Assert.Throws<ObjectDisposedException>(() => reader.ReadBytes(1));
            Assert.Throws<ObjectDisposedException>(() => reader.TryReadBytes(1, out _));
            Assert.Throws<ObjectDisposedException>(() => reader.Seek(0));
            Assert.Throws<ObjectDisposedException>(() => reader.Skip(0));
        });
    }

    private sealed class CountingMemoryOwner(byte[] buffer) : IMemoryOwner<byte>
    {
        public int DisposeCount { get; private set; }

        public Memory<byte> Memory => buffer;

        public void Dispose() => DisposeCount++;
    }
}
