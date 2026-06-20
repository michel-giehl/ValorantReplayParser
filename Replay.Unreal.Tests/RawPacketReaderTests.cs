using Replay.Models;

namespace Replay.Unreal.Tests;

public class RawPacketReaderTests
{
    [Test]
    public void ReadPacket_LastByteZero_ReturnsMalformed()
    {
        var reader = new RawPacketReader();
        var result = reader.ReadPacket(new byte[] { 0x00, 0x00, 0x00 }, 0, static (ref RawBunchHeader _) => { });

        Assert.Multiple(() =>
        {
            Assert.That(result.IsMalformed, Is.True);
            Assert.That(result.BunchCount, Is.EqualTo(0));
        });
    }

    [Test]
    public void ReadPacket_EmptyData_ReturnsZeroBunches()
    {
        var reader = new RawPacketReader();
        var result = reader.ReadPacket(new byte[0], 0, static (ref RawBunchHeader _) => { });
        Assert.That(result.BunchCount, Is.EqualTo(0));
    }

    [Test]
    public void ReadPacket_SingleBunch_ParsesHeaderFields()
    {
        var reader = new RawPacketReader();
        var packet = BuildPacket(w =>
        {
            w.WriteBit(false); // bControl = false
            w.WriteBit(false); // bIsReplicationPaused = false
            w.WriteBit(false); // bReliable = false
            w.WriteIntPacked(7); // ChIndex = 7
            w.WriteBit(false); // bHasPackageMapExports = false
            w.WriteBit(false); // bHasMustBeMappedGUIDs = false
            w.WriteBit(false); // bPartial = false
            w.WriteBit(false); // hasChannelName = false
            w.WritePayloadSize(0); // PayloadBitCount = 0
        });

        RawBunchHeader? captured = null;
        reader.ReadPacket(packet, 2, (ref RawBunchHeader header) => captured = header);

        Assert.That(captured, Is.Not.Null);
        var h = captured!.Value;
        Assert.Multiple(() =>
        {
            Assert.That(h.PacketId, Is.EqualTo(2));
            Assert.That(h.ChIndex, Is.EqualTo(7));
            Assert.That(h.bOpen, Is.False);
            Assert.That(h.bClose, Is.False);
            Assert.That(h.bIsReplicationPaused, Is.False);
            Assert.That(h.bReliable, Is.False);
            Assert.That(h.bPartial, Is.False);
            Assert.That(h.bHasPackageMapExports, Is.False);
            Assert.That(h.bHasMustBeMappedGUIDs, Is.False);
            Assert.That(h.PayloadBitCount, Is.EqualTo(0));
            Assert.That(h.ChName, Is.Null);
        });
    }

    [Test]
    public void ReadPacket_MultipleBunches_ParsesAll()
    {
        var reader = new RawPacketReader();
        var packet = BuildPacket(
            w =>
            {
                w.WriteBit(false); w.WriteBit(false); w.WriteBit(false);
                w.WriteIntPacked(0);
                w.WriteBit(false); w.WriteBit(false); w.WriteBit(false);
                w.WriteBit(false);
                w.WritePayloadSize(0);
            },
            w =>
            {
                w.WriteBit(false); w.WriteBit(false); w.WriteBit(false);
                w.WriteIntPacked(1);
                w.WriteBit(false); w.WriteBit(false); w.WriteBit(false);
                w.WriteBit(false);
                w.WritePayloadSize(0);
            });

        var bunchIndices = new List<uint>();
        reader.ReadPacket(packet, 3, (ref RawBunchHeader header) => bunchIndices.Add(header.ChIndex));

        Assert.That(bunchIndices, Is.EqualTo(new uint[] { 0, 1 }));
    }

    [Test]
    public void ReadPacket_ControlBunchWithClose_ParsesCloseReason()
    {
        var reader = new RawPacketReader();
        var packet = BuildPacket(w =>
        {
            w.WriteBit(true);  // bControl = true
            w.WriteBit(false); // bOpen = false
            w.WriteBit(true);  // bClose = true
            w.WriteSerializedInt((uint)ChannelCloseReason.Dormancy, (int)ChannelCloseReason.MAX);
            w.WriteBit(false); // bIsReplicationPaused = false
            w.WriteBit(false); // bReliable = false
            w.WriteIntPacked(3);
            w.WriteBit(false); w.WriteBit(false); w.WriteBit(false);
            w.WriteBit(false);
            w.WritePayloadSize(0);
        });

        RawBunchHeader? captured = null;
        reader.ReadPacket(packet, 1, (ref RawBunchHeader header) => captured = header);

        var h = captured!.Value;
        Assert.Multiple(() =>
        {
            Assert.That(h.bOpen, Is.False);
            Assert.That(h.bClose, Is.True);
            Assert.That(h.bDormant, Is.True);
            Assert.That(h.CloseReason, Is.EqualTo(ChannelCloseReason.Dormancy));
        });
    }

    [Test]
    public void ReadPacket_PartialInitialThenFinal_CompletesBunch()
    {
        var reader = new RawPacketReader();
        var packet = BuildPacket(
            w =>
            {
                w.WriteBit(false); w.WriteBit(false); w.WriteBit(true);
                w.WriteIntPacked(2);
                w.WriteBit(false); w.WriteBit(false);
                w.WriteBit(true); w.WriteBit(true); w.WriteBit(false);
                w.WriteBit(false);
                w.WriteFName(1);
                w.WritePayloadSize(8);
                w.WritePayloadBits(8);
            },
            w =>
            {
                w.WriteBit(false); w.WriteBit(false); w.WriteBit(true);
                w.WriteIntPacked(2);
                w.WriteBit(false); w.WriteBit(false);
                w.WriteBit(true); w.WriteBit(false); w.WriteBit(true);
                w.WriteBit(false);
                w.WriteFName(1);
                w.WritePayloadSize(4);
                w.WritePayloadBits(4);
            });

        var headers = new List<RawBunchHeader>();
        var result = reader.ReadPacket(packet, 0, (ref RawBunchHeader header) => headers.Add(header));

        Assert.Multiple(() =>
        {
            Assert.That(headers, Has.Count.EqualTo(2));
            Assert.That(result.PartialErrorCount, Is.EqualTo(0));
            Assert.That(headers[0].bPartialInitial, Is.True);
            Assert.That(headers[0].HasPartialError, Is.False);
            Assert.That(headers[1].bPartialFinal, Is.True);
            Assert.That(headers[1].IsPartialCompleted, Is.True);
            Assert.That(headers[1].HasPartialError, Is.False);
        });
    }

    [Test]
    public void ReadPacket_ContinuationWithoutInitial_ReportsError()
    {
        var reader = new RawPacketReader();
        var packet = BuildPacket(w =>
        {
            w.WriteBit(false); w.WriteBit(false); w.WriteBit(true);
            w.WriteIntPacked(5);
            w.WriteBit(false); w.WriteBit(false);
            w.WriteBit(true); w.WriteBit(false); w.WriteBit(true);
            w.WriteBit(false);
            w.WriteFName(1);
            w.WritePayloadSize(0);
        });

        var headers = new List<RawBunchHeader>();
        var result = reader.ReadPacket(packet, 2, (ref RawBunchHeader header) => headers.Add(header));

        Assert.Multiple(() =>
        {
            Assert.That(result.PartialErrorCount, Is.EqualTo(1));
            Assert.That(headers[0].HasPartialError, Is.True);
        });
    }

    [Test]
    public void ReadPacket_ReliabilityMismatchOnPartial_ReportsError()
    {
        var reader = new RawPacketReader();
        var packet = BuildPacket(
            w =>
            {
                w.WriteBit(false); w.WriteBit(false); w.WriteBit(true); // reliable
                w.WriteIntPacked(2);
                w.WriteBit(false); w.WriteBit(false);
                w.WriteBit(true); w.WriteBit(true); w.WriteBit(false);
                w.WriteBit(false);
                w.WriteFName(1);
                w.WritePayloadSize(0);
            },
            w =>
            {
                w.WriteBit(false); w.WriteBit(false); w.WriteBit(false); // NOT reliable
                w.WriteIntPacked(2);
                w.WriteBit(false); w.WriteBit(false);
                w.WriteBit(true); w.WriteBit(false); w.WriteBit(true);
                w.WriteBit(false);
                w.WritePayloadSize(0);
            });

        var headers = new List<RawBunchHeader>();
        var result = reader.ReadPacket(packet, 2, (ref RawBunchHeader header) => headers.Add(header));

        Assert.Multiple(() =>
        {
            Assert.That(result.PartialErrorCount, Is.EqualTo(1));
            Assert.That(headers[1].HasPartialError, Is.True);
        });
    }

    [Test]
    public void ReadPacket_PayloadBitsSkipped_StreamRemainsAligned()
    {
        var reader = new RawPacketReader();
        var packet = BuildPacket(w =>
        {
            w.WriteBit(false); w.WriteBit(false); w.WriteBit(false);
            w.WriteIntPacked(0);
            w.WriteBit(false); w.WriteBit(false); w.WriteBit(false);
            w.WriteBit(false);
            w.WritePayloadSize(17);
            w.WritePayloadBits(17);
        });

        var bunchCount = 0;
        reader.ReadPacket(packet, 0, (ref RawBunchHeader _) => bunchCount++);

        Assert.That(bunchCount, Is.EqualTo(1));
    }

    [Test]
    public void ReadPacket_PayloadBitOffset_PointsToPayloadStart()
    {
        var reader = new RawPacketReader();
        var packet = BuildPacket(w =>
        {
            w.WriteBit(false); w.WriteBit(false); w.WriteBit(false);
            w.WriteIntPacked(0);
            w.WriteBit(false); w.WriteBit(false); w.WriteBit(false);
            w.WriteBit(false);
            w.WritePayloadSize(17);
            w.WritePayloadBits(17);
        });

        RawBunchHeader? captured = null;
        reader.ReadPacket(packet, 0, (ref RawBunchHeader header) => captured = header);

        Assert.That(captured, Is.Not.Null);
        Assert.That(captured!.Value.PayloadBitOffset, Is.EqualTo(ComputePacketBitSize(packet) - 17));
    }

    [Test]
    public void ReadPacket_PayloadOverrun_ReturnsMalformedWithoutCallback()
    {
        var reader = new RawPacketReader();
        var packet = BuildPacket(w =>
        {
            w.WriteBit(false); w.WriteBit(false); w.WriteBit(false);
            w.WriteIntPacked(0);
            w.WriteBit(false); w.WriteBit(false); w.WriteBit(false);
            w.WriteBit(false);
            w.WritePayloadSize(17);
        });

        var callbackCount = 0;
        var result = reader.ReadPacket(packet, 0, (ref RawBunchHeader _) => callbackCount++);

        Assert.Multiple(() =>
        {
            Assert.That(result.IsMalformed, Is.True);
            Assert.That(result.BunchCount, Is.EqualTo(0));
            Assert.That(callbackCount, Is.EqualTo(0));
        });
    }

    [Test]
    public void ReadPacket_Reset_ClearsPartialState()
    {
        var reader = new RawPacketReader();

        var p1 = BuildPacket(w =>
        {
            w.WriteBit(false); w.WriteBit(false); w.WriteBit(true);
            w.WriteIntPacked(4);
            w.WriteBit(false); w.WriteBit(false);
            w.WriteBit(true); w.WriteBit(true); w.WriteBit(false);
            w.WriteBit(false);
            w.WriteFName(1);
            w.WritePayloadSize(0);
        });
        reader.ReadPacket(p1, 0, static (ref RawBunchHeader _) => { });

        reader.Reset();

        var p2 = BuildPacket(w =>
        {
            w.WriteBit(false); w.WriteBit(false); w.WriteBit(true);
            w.WriteIntPacked(4);
            w.WriteBit(false); w.WriteBit(false);
            w.WriteBit(true); w.WriteBit(false); w.WriteBit(true);
            w.WriteBit(false);
            w.WriteFName(1);
            w.WritePayloadSize(0);
        });
        var headers = new List<RawBunchHeader>();
        var result = reader.ReadPacket(p2, 1, (ref RawBunchHeader header) => headers.Add(header));

        Assert.Multiple(() =>
        {
            Assert.That(result.PartialErrorCount, Is.EqualTo(1));
            Assert.That(headers[0].HasPartialError, Is.True);
        });
    }

    private static byte[] BuildPacket(params Action<PacketBuilder>[] writeBunches)
    {
        var totalBits = new List<bool>();
        foreach (var write in writeBunches)
        {
            var builder = new PacketBuilder();
            write(builder);
            totalBits.AddRange(builder.HeaderBits);
        }

        var totalDataBits = totalBits.Count;
        var byteCount = (totalDataBits + 1 + 7) / 8;
        var packet = new byte[byteCount];
        for (var i = 0; i < totalBits.Count; i++)
            if (totalBits[i])
                packet[i >> 3] |= (byte)(1 << (i & 7));
        packet[totalDataBits >> 3] |= (byte)(1 << (totalDataBits & 7));
        return packet;
    }

    private static int ComputePacketBitSize(byte[] packet)
    {
        var lastByte = packet[^1];
        var bitSize = packet.Length * 8 - 1;
        while ((lastByte & 0x80) == 0)
        {
            lastByte <<= 1;
            bitSize--;
        }

        return bitSize;
    }

    private sealed class PacketBuilder
    {
        public List<bool> HeaderBits { get; } = [];
        public int PayloadBitCount { get; private set; }

        public void WriteBit(bool value) => HeaderBits.Add(value);

        public void WriteIntPacked(uint value)
        {
            do
            {
                var nextByte = (byte)((value & 0x7F) << 1);
                value >>= 7;
                if (value != 0)
                    nextByte |= 1;
                for (var i = 0; i < 8; i++)
                    HeaderBits.Add((nextByte & (1 << i)) != 0);
            } while (value != 0);
        }

        public void WriteSerializedInt(uint value, int maxValue)
        {
            uint mask = 1;
            while ((value + mask) < (uint)maxValue)
            {
                HeaderBits.Add((value & mask) != 0);
                mask <<= 1;
            }
        }

        public void WritePayloadSize(uint value)
        {
            PayloadBitCount = (int)value;
            WriteSerializedInt(value, Constants.MaxPacketSizeInBits);
        }

        public void WritePayloadBits(int count)
        {
            for (var i = 0; i < count; i++)
                HeaderBits.Add(false);
        }

        public void WriteFName(uint nameIndex)
        {
            WriteBit(true);
            WriteIntPacked(nameIndex);
        }
    }
}
