using System.Buffers.Binary;
using Replay.Encoding.Archives;
using Replay.Encoding.Net;
using Replay.Models;

namespace Replay.Unreal.Tests;

public class PlaybackPacketReaderTests
{
    [Test]
    public void Read_FrameWithTwoPackets_RecordsPacketStats()
    {
        var packet0 = BuildRawPacket(0);
        var packet1 = BuildRawPacket(1);
        var context = CreateContext();
        var archive = new FBinaryArchive(BuildFrame([packet0, packet1], exportData: [], externalData: []));

        new PlaybackPacketReader(context, CreateDataChunk(), archive).Read();

        Assert.Multiple(() =>
        {
            Assert.That(context.PacketStats.PacketCount, Is.EqualTo(2));
            Assert.That(context.PacketStats.TotalPacketBytes, Is.EqualTo(packet0.Length + packet1.Length));
            Assert.That(context.PacketStats.PacketsWithBunches, Is.EqualTo(2));
            Assert.That(context.PacketStats.BunchCount, Is.EqualTo(2));
            Assert.That(context.PacketStats.MalformedPacketCount, Is.EqualTo(0));
            Assert.That(context.PacketStats.PartialErrorCount, Is.EqualTo(0));
            Assert.That(context.PacketStats.MinTimeSeconds, Is.EqualTo(12.5f));
            Assert.That(context.PacketStats.MaxTimeSeconds, Is.EqualTo(12.5f));
            Assert.That(archive.AtEnd, Is.True);
        });
    }

    [Test]
    public void Read_FrameWithExportData_KeepsPacketAlignment()
    {
        var packet = BuildRawPacket();
        var context = CreateContext();
        var bytes = BuildFrame(packet, exportData: BuildExportData());
        var archive = new FBinaryArchive(bytes);

        new PlaybackPacketReader(context, CreateDataChunk(), archive).Read();

        Assert.Multiple(() =>
        {
            Assert.That(context.PacketStats.PacketCount, Is.EqualTo(1));
            Assert.That(context.PacketStats.TotalPacketBytes, Is.EqualTo(packet.Length));
            Assert.That(context.PacketStats.BunchCount, Is.EqualTo(1));
            Assert.That(archive.AtEnd, Is.True);
        });
    }

    [Test]
    public void Read_FrameWithExternalData_KeepsPacketAlignment()
    {
        var packet = BuildRawPacket();
        var context = CreateContext();
        var bytes = BuildFrame(packet, exportData: [], externalData: BuildExternalData());
        var archive = new FBinaryArchive(bytes);

        new PlaybackPacketReader(context, CreateDataChunk(), archive).Read();

        Assert.Multiple(() =>
        {
            Assert.That(context.PacketStats.PacketCount, Is.EqualTo(1));
            Assert.That(context.PacketStats.TotalPacketBytes, Is.EqualTo(packet.Length));
            Assert.That(context.PacketStats.BunchCount, Is.EqualTo(1));
            Assert.That(archive.AtEnd, Is.True);
        });
    }

    [Test]
    public void Read_NegativePacketSize_ThrowsInvalidReplayInfoException()
    {
        var context = CreateContext();
        var archive = new FBinaryArchive(BuildFrameWithPacketSize(-1));

        Assert.Throws<InvalidReplayInfoException>(() =>
            new PlaybackPacketReader(context, CreateDataChunk(), archive).Read());
    }

    [Test]
    public void Read_OversizedPacket_ThrowsInvalidReplayInfoException()
    {
        var context = CreateContext();
        var archive = new FBinaryArchive(BuildFrameWithPacketSize((Constants.MaxPacketSizeInBits / 8) + 1));

        Assert.Throws<InvalidReplayInfoException>(() =>
            new PlaybackPacketReader(context, CreateDataChunk(), archive).Read());
    }

    private static ReplayReaderContext CreateContext()
    {
        return new ReplayReaderContext(new FBinaryArchive(ReadOnlyMemory<byte>.Empty))
        {
            ReplayHeader = new ReplayHeader
            {
                NetworkVersion = Constants.ExpectedNetworkVersion,
                Flags = ReplayHeaderFlags.HasStreamingFixes,
            },
        };
    }

    private static ReplayDataChunkInfo CreateDataChunk() => new()
    {
        ChunkIndex = 4,
    };

    private static byte[] BuildFrame(params byte[][] packets) =>
        BuildFrame(packets, exportData: [], externalData: []);

    private static byte[] BuildFrame(byte[] packet, byte[] exportData, byte[]? externalData = null) =>
        BuildFrame([packet], exportData, externalData ?? []);

    private static byte[] BuildFrame(byte[][] packets, byte[] exportData, byte[] externalData)
    {
        var bytes = new List<byte>();
        AddInt32(bytes, 7);
        AddSingle(bytes, 12.5f);
        bytes.AddRange(exportData.Length == 0 ? BuildEmptyExportData() : exportData);
        AddIntPacked(bytes, 0);
        AddUInt64(bytes, 0);
        bytes.AddRange(externalData.Length == 0 ? [0] : externalData);

        var seenLevelIndex = 3u;
        foreach (var packet in packets)
        {
            AddIntPacked(bytes, seenLevelIndex);
            AddInt32(bytes, packet.Length);
            bytes.AddRange(packet);
            seenLevelIndex += 2;
        }

        AddIntPacked(bytes, seenLevelIndex);
        AddInt32(bytes, 0);
        return bytes.ToArray();
    }

    private static byte[] BuildFrameWithPacketSize(int packetSize)
    {
        var bytes = new List<byte>();
        AddInt32(bytes, 7);
        AddSingle(bytes, 12.5f);
        bytes.AddRange(BuildEmptyExportData());
        AddIntPacked(bytes, 0);
        AddUInt64(bytes, 0);
        AddIntPacked(bytes, 0);
        AddIntPacked(bytes, 3);
        AddInt32(bytes, packetSize);
        return bytes.ToArray();
    }

    private static byte[] BuildEmptyExportData()
    {
        var bytes = new List<byte>();
        AddIntPacked(bytes, 0);
        AddIntPacked(bytes, 0);
        return bytes.ToArray();
    }

    private static byte[] BuildExportData()
    {
        var bytes = new List<byte>();
        AddIntPacked(bytes, 1);
        AddIntPacked(bytes, 11);
        AddIntPacked(bytes, 1);
        AddFString(bytes, "/Game/Test.Test_C");
        AddIntPacked(bytes, 3);
        bytes.Add(1);
        AddIntPacked(bytes, 2);
        AddUInt32(bytes, 0xAABBCCDD);
        bytes.Add(0);
        AddFString(bytes, "FieldName");
        AddInt32(bytes, 0);
        AddIntPacked(bytes, 1);
        var netGuidPayload = BuildNetGuidPayload();
        AddInt32(bytes, netGuidPayload.Length);
        bytes.AddRange(netGuidPayload);
        return bytes.ToArray();
    }

    private static byte[] BuildNetGuidPayload()
    {
        var bytes = new List<byte>();
        AddIntPacked(bytes, 17);
        bytes.Add((byte)ExportFlags.HasPath);
        AddIntPacked(bytes, 0);
        AddFString(bytes, "/Game/Test.Test_C");
        return bytes.ToArray();
    }

    private static byte[] BuildExternalData()
    {
        var bytes = new List<byte>();
        AddIntPacked(bytes, 9);
        AddIntPacked(bytes, 42);
        bytes.AddRange([0xAB, 0xCD]);
        AddIntPacked(bytes, 0);
        return bytes.ToArray();
    }

    private static byte[] BuildRawPacket(uint channelIndex = 0)
    {
        var bits = new List<bool>();
        AddBit(bits, false); // bControl
        AddBit(bits, false); // bIsReplicationPaused
        AddBit(bits, false); // bReliable
        AddIntPackedBits(bits, channelIndex);
        AddBit(bits, false); // bHasPackageMapExports
        AddBit(bits, false); // bHasMustBeMappedGUIDs
        AddBit(bits, false); // bPartial
        AddBit(bits, false); // Valorant specific bit
        AddSerializedIntBits(bits, 0, Constants.MaxPacketSizeInBits);

        var packet = new byte[(bits.Count + 1 + 7) / 8];
        for (var i = 0; i < bits.Count; i++)
        {
            if (bits[i])
            {
                packet[i >> 3] |= (byte)(1 << (i & 7));
            }
        }

        packet[bits.Count >> 3] |= (byte)(1 << (bits.Count & 7));
        return packet;
    }

    private static void AddBit(List<bool> bits, bool value) => bits.Add(value);

    private static void AddIntPackedBits(List<bool> bits, uint value)
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

    private static void AddSerializedIntBits(List<bool> bits, uint value, int maxValue)
    {
        for (uint mask = 1; value + mask < maxValue; mask <<= 1)
        {
            bits.Add((value & mask) != 0);
        }
    }

    private static void AddFString(List<byte> bytes, string value)
    {
        var encoded = System.Text.Encoding.UTF8.GetBytes(value + '\0');
        AddInt32(bytes, encoded.Length);
        bytes.AddRange(encoded);
    }

    private static void AddSingle(List<byte> bytes, float value)
    {
        Span<byte> buffer = stackalloc byte[4];
        BinaryPrimitives.WriteSingleLittleEndian(buffer, value);
        bytes.AddRange(buffer.ToArray());
    }

    private static void AddUInt32(List<byte> bytes, uint value)
    {
        Span<byte> buffer = stackalloc byte[4];
        BinaryPrimitives.WriteUInt32LittleEndian(buffer, value);
        bytes.AddRange(buffer.ToArray());
    }

    private static void AddInt32(List<byte> bytes, int value)
    {
        Span<byte> buffer = stackalloc byte[4];
        BinaryPrimitives.WriteInt32LittleEndian(buffer, value);
        bytes.AddRange(buffer.ToArray());
    }

    private static void AddUInt64(List<byte> bytes, ulong value)
    {
        Span<byte> buffer = stackalloc byte[8];
        BinaryPrimitives.WriteUInt64LittleEndian(buffer, value);
        bytes.AddRange(buffer.ToArray());
    }

    private static void AddIntPacked(List<byte> bytes, uint value)
    {
        do
        {
            var nextByte = (byte)((value & 0x7F) << 1);
            value >>= 7;
            if (value != 0)
            {
                nextByte |= 1;
            }

            bytes.Add(nextByte);
        } while (value != 0);
    }
}
