using Replay.Encoding.Archives;

namespace Replay.Encoding.Tests.Archives;

public class BitReaderTests
{
    [Test]
    public void ReadBit_ReadsLeastSignificantBitFirst()
    {
        var reader = new BitArchiveReader([0b1010_0101]);

        Assert.That(reader.ReadBit(), Is.True);
        Assert.That(reader.ReadBit(), Is.False);
        Assert.That(reader.ReadBit(), Is.True);
        Assert.That(reader.ReadBit(), Is.False);
        Assert.That(reader.BitPosition, Is.EqualTo(4));
    }

    [Test]
    public void ReadByte_ReadsAcrossUnalignedBitPosition()
    {
        var reader = new BitArchiveReader([0b1111_0000, 0b0000_1111]);

        reader.SkipBits(4);

        Assert.That(reader.ReadByte(), Is.EqualTo(0xFF));
        Assert.That(reader.BitPosition, Is.EqualTo(12));
    }

    [Test]
    public void ReadBitsToLong_PreservesLsbFirstValueOrder()
    {
        var reader = new BitArchiveReader([0b1101_0110]);

        Assert.That(reader.ReadBitsToUInt64(5), Is.EqualTo(0b1_0110UL));
        Assert.That(reader.BitPosition, Is.EqualTo(5));
    }

    [Test]
    public void ReadSerializedInt_ReadsUntilMaskWouldReachMaxValue()
    {
        var reader = new BitArchiveReader([0b0000_1101]);

        Assert.That(reader.ReadSerializedInt(8), Is.EqualTo(5u));
        Assert.That(reader.BitPosition, Is.EqualTo(3));
    }

    [Test]
    public void ReadIntPacked_DecodesFromUnalignedPosition()
    {
        var reader = new BitArchiveReader([0x48, 0x30, 0x00]);

        reader.SkipBits(3);

        Assert.That(reader.ReadIntPacked(), Is.EqualTo(388u));
        Assert.That(reader.BitPosition, Is.EqualTo(19));
    }

    [Test]
    public void ReadSubArchive_LimitsChildAndAdvancesParent()
    {
        var reader = new BitArchiveReader([0xAA, 0xBB, 0xCC]);

        reader.SkipBits(4);
        var child = reader.ReadSubArchive(8);

        Assert.That(reader.BitPosition, Is.EqualTo(12));
        Assert.That(child.BitsRemaining, Is.EqualTo(8));
        Assert.That(child.ReadByte(), Is.EqualTo(0xBA));

        var exception = Assert.Throws<ArchiveReadException>(() => child.ReadBit());
        Assert.That(exception!.ErrorCode, Is.EqualTo(ArchiveErrorCode.EndOfArchive));

        Assert.That(reader.ReadByte(), Is.EqualTo(0xCB));
    }

    [Test]
    public void Checkpoint_RollsBackUnlessCommitted()
    {
        var reader = new BitArchiveReader([0xFF]);

        using (reader.CreateCheckpoint())
        {
            reader.SkipBits(4);
        }

        Assert.That(reader.BitPosition, Is.EqualTo(0));

        using (var checkpoint = reader.CreateCheckpoint())
        {
            reader.SkipBits(4);
            checkpoint.Commit();
        }

        Assert.That(reader.BitPosition, Is.EqualTo(4));
    }

    [Test]
    public void BitBufferBuilder_AppendsExactBitCounts()
    {
        var first = new BitArchiveReader([0b0000_1111], 4);
        var second = new BitArchiveReader([0b0000_0010], 2);
        var builder = new BitBufferBuilder();

        builder.Append(first, 4);
        builder.Append(second, 2);

        var reader = builder.BuildReader();

        Assert.That(reader.BitLength, Is.EqualTo(6));
        Assert.That(reader.ReadBitsToUInt64(6), Is.EqualTo(0b10_1111UL));
    }
}
