using System.Buffers.Binary;
using Replay.Encoding.Archives;
using Replay.Encoding.Compression;
using Replay.Models.Descriptors;
using Replay.Models.Diagnostics;
using Replay.Models.Errors;
using Replay.Models.Protocol;
using Replay.Models.Replay;
using Replay.Models.Unreal;
using Replay.Unreal.Chunks;
using Replay.Unreal.Readers;

namespace Replay.Valorant.Tests;

public class ValorantReplayReaderTests
{
    private const uint FileMagic = 0x43F4EFDD;
    private const uint LocalReplayGuidA = 0x95A4F03E;
    private const uint LocalReplayGuidB = 0x7E0B49E4;
    private const uint LocalReplayGuidC = 0xBA43D356;
    private const uint LocalReplayGuidD = 0x94FF87D9;
    private static readonly Guid HeaderGuid = Guid.Parse("00112233-4455-6677-8899-aabbccddeeff");

    [Test]
    public void Read_HeaderChunk_ParsesHeaderDuringChunkDispatch()
    {
        var archive = new FBinaryArchive(BuildReplayInfo(chunks: [HeaderChunk(BuildHeader())]));

        var context = new ValorantReplayReader(new FakeOodleDecompressor()).Read(archive);

        Assert.Multiple(() =>
        {
            Assert.That(context.Metadata.ReplayInfo.HeaderChunkIndex, Is.EqualTo(0));
            Assert.That(context.Metadata.ReplayHeader.Guid, Is.EqualTo(HeaderGuid));
            Assert.That(context.Metadata.ReplayVersion.Branch, Is.EqualTo("++Ares-Core+release-12.10"));
        });
    }

    [Test]
    public void Read_Stream_LeavesBorrowedStreamOpenAndResultUsableAfterDisposal()
    {
        var stream = new MemoryStream(BuildReplayInfo(chunks: [HeaderChunk(BuildHeader())]));
        var result = new ValorantReplayReader().Read(stream);

        Assert.Multiple(() =>
        {
            Assert.That(stream.CanRead, Is.True);
            Assert.That(stream.Position, Is.EqualTo(stream.Length));
            Assert.That(result.Metadata.ReplayHeader.Guid, Is.EqualTo(HeaderGuid));
            Assert.That(result.Status, Is.EqualTo(ReplayReadStatus.Completed));
        });

        stream.Dispose();

        Assert.That(result.Metadata.ReplayHeader.Guid, Is.EqualTo(HeaderGuid));
        Assert.That(result.Metadata.ReplayInfo.Chunks, Has.Count.EqualTo(1));
    }

    [Test]
    public void Read_ReaderCanBeReusedSequentially()
    {
        var replayBytes = BuildReplayInfo(chunks: [HeaderChunk(BuildHeader())]);
        var reader = new ValorantReplayReader();

        var firstResult = reader.Read(new FBinaryArchive(replayBytes));
        var secondResult = reader.Read(new FBinaryArchive(replayBytes));

        Assert.Multiple(() =>
        {
            Assert.That(firstResult.Metadata.ReplayHeader.Guid, Is.EqualTo(HeaderGuid));
            Assert.That(secondResult.Metadata.ReplayHeader.Guid, Is.EqualTo(HeaderGuid));
            Assert.That(firstResult.Status, Is.EqualTo(ReplayReadStatus.Completed));
            Assert.That(secondResult.Status, Is.EqualTo(ReplayReadStatus.Completed));
        });
    }

    [Test]
    public void SnapshotParseProfile_PreservesSelectionSetComparers()
    {
        var profile = new ParseProfile
        {
            IncludedPaths = new HashSet<string>(["/Game/Test.Path"], StringComparer.OrdinalIgnoreCase),
            ExcludedPaths = new HashSet<string>(["/Game/Other.Path"], StringComparer.OrdinalIgnoreCase),
            IncludedFields = new HashSet<string>(["SomeField"], StringComparer.OrdinalIgnoreCase),
        };

        var snapshot = ValorantReplayReader.SnapshotParseProfile(profile);

        Assert.Multiple(() =>
        {
            Assert.That(snapshot.IncludedPaths!.Contains("/game/test.path"), Is.True);
            Assert.That(snapshot.ExcludedPaths!.Contains("/game/other.path"), Is.True);
            Assert.That(snapshot.IncludedFields!.Contains("somefield"), Is.True);
        });
    }

    [Test]
    public void Read_ReentrantReadIsRejectedAndGuardResetsAfterFailure()
    {
        var decompressor = new CallbackOodleDecompressor([0xAA]);
        var reader = new ValorantReplayReader(decompressor, new NoOpReplayDataChunkHandler());
        var nestedReplay = BuildReplayInfo(chunks: [HeaderChunk(BuildHeader())]);
        using var reentrantReadStream = new MemoryStream(nestedReplay);
        using var reentrantMetadataStream = new MemoryStream(nestedReplay);
        InvalidOperationException? reentrantReadException = null;
        InvalidOperationException? reentrantMetadataException = null;
        decompressor.OnDecompress = () =>
        {
            reentrantReadException = Assert.Throws<InvalidOperationException>(() => reader.Read(reentrantReadStream));
            reentrantMetadataException = Assert.Throws<InvalidOperationException>(() => reader.ReadMetadata(reentrantMetadataStream));
            throw reentrantReadException!;
        };
        var replayBytes = BuildReplayInfo(
            compressed: true,
            chunks:
            [
                HeaderChunk(BuildHeader()),
                ReplayDataChunk(0, 10, BuildOodlePayload(1, [0x10])),
            ]);

        var exception = Assert.Throws<InvalidOperationException>(() => reader.Read(new FBinaryArchive(replayBytes)));
        var result = reader.Read(new FBinaryArchive(replayBytes));

        Assert.Multiple(() =>
        {
            Assert.That(exception!.Message, Does.Contain("concurrent or reentrant"));
            Assert.That(reentrantReadException!.Message, Does.Contain("concurrent or reentrant"));
            Assert.That(reentrantMetadataException!.Message, Does.Contain("concurrent or reentrant"));
            Assert.That(reentrantReadStream.Position, Is.Zero);
            Assert.That(reentrantMetadataStream.Position, Is.Zero);
            Assert.That(result.Metadata.ReplayInfo.DataChunks, Has.Count.EqualTo(1));
        });
    }

    [Test]
    public void ReadMetadata_HeaderChunk_ReturnsSupportedMetadataWithoutReadingReplayData()
    {
        var archive = new FBinaryArchive(BuildReplayInfo(chunks:
        [
            HeaderChunk(BuildHeader()),
            ReplayDataChunk(0, 10, BuildOodlePayload(1, [0x10])),
        ]));

        var metadata = new ValorantReplayReader(new FakeOodleDecompressor()).ReadMetadata(archive);

        Assert.Multiple(() =>
        {
            Assert.That(metadata.FullParseSupportStatus, Is.EqualTo(ValorantReplaySupportStatus.Supported));
            Assert.That(metadata.FullParseUnsupportedReason, Is.Null);
            Assert.That(metadata.ReplayHeader.Guid, Is.EqualTo(HeaderGuid));
            Assert.That(metadata.ReplayInfo.Chunks, Has.Count.EqualTo(1));
            Assert.That(metadata.ReplayInfo.DataChunks, Is.Empty);
            Assert.That(archive.AtEnd, Is.False);
        });
    }

    [Test]
    public void ReadMetadata_UnsupportedBranch_ReturnsExplicitSupportStatus()
    {
        var archive = new FBinaryArchive(BuildReplayInfo(chunks:
            [HeaderChunk(BuildHeader("++Ares-Core+release-12.08"))]));

        var metadata = new ValorantReplayReader().ReadMetadata(archive);

        Assert.Multiple(() =>
        {
            Assert.That(metadata.FullParseSupportStatus, Is.EqualTo(ValorantReplaySupportStatus.UnsupportedVersion));
            Assert.That(metadata.FullParseUnsupportedReason, Does.Contain("release-12.08"));
        });
    }

    [Test]
    public void Read_UnsupportedBranch_ThrowsBeforeReplayDataIsRead()
    {
        var handler = new CapturingReplayDataChunkHandler();
        var archive = new FBinaryArchive(BuildReplayInfo(chunks:
        [
            HeaderChunk(BuildHeader("++Ares-Core+release-12.08")),
            ReplayDataChunk(0, 10, [0x01], memorySizeInBytes: 1),
        ]));

        var exception = Assert.Throws<InvalidReplayInfoException>(() =>
            new ValorantReplayReader(new FakeOodleDecompressor(), chunkHandler: handler).Read(archive));

        Assert.Multiple(() =>
        {
            Assert.That(exception!.Message, Does.Contain("release-12.08"));
            Assert.That(handler.Payloads, Is.Empty);
        });
    }

    [Test]
    public void Read_ReplayDataBeforeHeader_ThrowsInvalidReplayInfoException()
    {
        var archive = new FBinaryArchive(BuildReplayInfo(chunks:
        [
            ReplayDataChunk(0, 10, [0x01], memorySizeInBytes: 1),
            HeaderChunk(BuildHeader()),
        ]));

        var exception = Assert.Throws<InvalidReplayInfoException>(() => new ValorantReplayReader().Read(archive));

        Assert.That(exception!.Message, Does.Contain("before the replay header"));
    }

    [Test]
    public void Read_ReplayDataChunk_DispatchesDecompressedPayloadToHandler()
    {
        var replayDataHandler = new CapturingReplayDataChunkHandler();
        var archive = new FBinaryArchive(BuildReplayInfo(compressed: true, chunks:
        [
            HeaderChunk(BuildHeader()),
            ReplayDataChunk(0, 10, BuildOodlePayload(2, [0x10])),
        ]));

        var context = new ValorantReplayReader(
            new FakeOodleDecompressor([0xAA, 0xBB]),
            replayDataHandler).Read(archive);

        Assert.Multiple(() =>
        {
            Assert.That(context.Metadata.ReplayInfo.DataChunks, Has.Count.EqualTo(1));
            Assert.That(replayDataHandler.Payloads, Is.EqualTo(new[] { new byte[] { 0xAA, 0xBB } }));
        });
    }

    [Test]
    public void Read_EncryptedReplayData_ThrowsInvalidReplayDataException()
    {
        var archive = new FBinaryArchive(BuildReplayInfo(
            compressed: true,
            encrypted: true,
            encryptionKey: [0x01],
            chunks:
            [
                HeaderChunk(BuildHeader()),
                ReplayDataChunk(0, 10, BuildOodlePayload(1, [0x10])),
            ]));

        var exception = Assert.Throws<InvalidReplayDataException>(() =>
            new ValorantReplayReader(new FakeOodleDecompressor([0xAA])).Read(archive));

        Assert.That(exception!.Message, Does.Contain("Encrypted VALORANT replay-data chunks are not supported"));
    }

    [Test]
    public void Read_DuplicateHeaderChunks_ThrowsInvalidReplayInfoException()
    {
        var archive = new FBinaryArchive(BuildReplayInfo(chunks:
            [HeaderChunk(BuildHeader()), HeaderChunk(BuildHeader())]));

        var exception = Assert.Throws<InvalidReplayInfoException>(() =>
            new ValorantReplayReader(new FakeOodleDecompressor()).Read(archive));

        Assert.That(exception!.Message, Does.Contain("multiple header chunks"));
    }

    [Test]
    public void Read_ReplayDataMetadataCannotReadPastDeclaredChunkSize_ThrowsInvalidReplayDataException()
    {
        var archive = new FBinaryArchive(BuildReplayInfo(chunks:
        [
            HeaderChunk(BuildHeader()),
            RawChunk(ReplayChunkType.ReplayData, 15, new byte[15]),
        ]));

        Assert.Throws<InvalidReplayDataException>(() =>
            new ValorantReplayReader(new FakeOodleDecompressor()).Read(archive));
    }

    [Test]
    public void Read_ReplayDataTimes_AreReadFromChunkMetadata()
    {
        var archive = new FBinaryArchive(BuildReplayInfo(chunks:
        [
            HeaderChunk(BuildHeader()),
            ReplayDataChunk(1, 10, [0x01, 0x02], memorySizeInBytes: 2),
            ReplayDataChunk(10, 20, [0x03, 0x04, 0x05], memorySizeInBytes: 3),
        ]));

        var context = new ValorantReplayReader(new FakeOodleDecompressor(), new NoOpReplayDataChunkHandler()).Read(archive);

        Assert.Multiple(() =>
        {
            Assert.That(context.Metadata.ReplayInfo.DataChunks, Has.Count.EqualTo(2));
            Assert.That(context.Metadata.ReplayInfo.DataChunks[0].Time1, Is.EqualTo(1u));
            Assert.That(context.Metadata.ReplayInfo.DataChunks[0].Time2, Is.EqualTo(10u));
            Assert.That(context.Metadata.ReplayInfo.DataChunks[1].Time1, Is.EqualTo(10u));
            Assert.That(context.Metadata.ReplayInfo.DataChunks[1].Time2, Is.EqualTo(20u));
            Assert.That(context.Metadata.ReplayInfo.TotalDataSizeInBytes, Is.EqualTo(5));
        });
    }

    [Test]
    public void Read_ValidChunks_TracksHeaderOffset()
    {
        var archive = new FBinaryArchive(BuildReplayInfo(chunks: [UnknownChunk([0x01]), HeaderChunk(BuildHeader())]));

        var context = new ValorantReplayReader().Read(archive);

        Assert.Multiple(() =>
        {
            Assert.That(context.Metadata.ReplayInfo.Chunks, Has.Count.EqualTo(2));
            Assert.That(context.Metadata.ReplayInfo.HeaderChunkIndex, Is.EqualTo(1));
            Assert.That(context.Metadata.ReplayInfo.Chunks[1].DataOffset,
                Is.GreaterThan(context.Metadata.ReplayInfo.Chunks[0].DataOffset));
        });
    }

    [Test]
    public void Read_ReplayDataFrame_RecordsPacketStats()
    {
        var packet = BuildRawPacket();
        var frame = BuildDemoFrame(packet);
        var archive = new FBinaryArchive(BuildReplayInfo(chunks:
        [
            HeaderChunk(BuildHeader()),
            ReplayDataChunk(100, 200, frame, memorySizeInBytes: frame.Length),
        ]));

        var context = new ValorantReplayReader().Read(archive);

        Assert.Multiple(() =>
        {
            Assert.That(context.PacketStats.PacketCount, Is.EqualTo(1));
            Assert.That(context.PacketStats.TotalPacketBytes, Is.EqualTo(packet.Length));
            Assert.That(context.PacketStats.BunchCount, Is.EqualTo(1));
            Assert.That(context.PacketStats.MinTimeSeconds, Is.EqualTo(12.5f));
        });
    }

    private static byte[] BuildReplayInfo(
        bool compressed = false,
        bool encrypted = false,
        byte[]? encryptionKey = null,
        params byte[][] chunks)
    {
        var bytes = new List<byte>();
        AddUInt32(bytes, FileMagic);
        AddUInt32(bytes, 7);
        AddInt32(bytes, 1);
        AddUnrealGuid(bytes, LocalReplayGuidA, LocalReplayGuidB, LocalReplayGuidC, LocalReplayGuidD);
        AddInt32(bytes, 7);
        AddInt32(bytes, 60000);
        AddUInt32(bytes, 19);
        AddUInt32(bytes, 1234);
        AddFString(bytes, "Replay");
        AddUInt32(bytes, 0);
        AddInt64(bytes, 42);
        AddUInt32(bytes, compressed ? 1u : 0u);
        AddUInt32(bytes, encrypted ? 1u : 0u);
        AddByteArray(bytes, encryptionKey ?? []);
        foreach (var chunk in chunks)
        {
            bytes.AddRange(chunk);
        }

        return bytes.ToArray();
    }

    private static byte[] HeaderChunk(byte[] payload) => RawChunk(ReplayChunkType.Header, payload);

    private static byte[] UnknownChunk(byte[] payload) => RawChunk(ReplayChunkType.Unknown, payload);

    private static byte[] ReplayDataChunk(
        uint startTime,
        uint endTime,
        byte[] payload,
        int? memorySizeInBytes = null)
    {
        var data = new List<byte>();
        AddUInt32(data, startTime);
        AddUInt32(data, endTime);
        AddInt32(data, payload.Length);
        AddInt32(data,
            memorySizeInBytes ??
            (payload.Length == 0 ? 0 : BinaryPrimitives.ReadInt32LittleEndian(payload.AsSpan(0, 4))));
        data.AddRange(payload);
        return RawChunk(ReplayChunkType.ReplayData, data.ToArray());
    }

    private static byte[] RawChunk(ReplayChunkType chunkType, int sizeInBytes, byte[] payload)
    {
        var bytes = new List<byte>();
        AddUInt32(bytes, (uint)chunkType);
        AddInt32(bytes, sizeInBytes);
        bytes.AddRange(payload);
        return bytes.ToArray();
    }

    private static byte[] RawChunk(ReplayChunkType chunkType, byte[] payload)
    {
        var bytes = new List<byte>();
        AddUInt32(bytes, (uint)chunkType);
        AddInt32(bytes, payload.Length);
        bytes.AddRange(payload);
        return bytes.ToArray();
    }

    private static byte[] BuildHeader(string branch = "++Ares-Core+release-12.10")
    {
        var bytes = new List<byte>();
        AddUInt32(bytes, Constants.NetworkMagic);
        AddUInt32(bytes, Constants.ExpectedNetworkVersion);
        AddInt32(bytes, 0);
        AddUInt32(bytes, 0x11223344u);
        AddUInt32(bytes, Constants.ExpectedEngineNetworkProtocolVersion);
        AddUInt32(bytes, 0);
        AddUnrealGuid(bytes, 0x00112233u, 0x44556677u, 0x8899AABBu, 0xCCDDEEFFu);
        AddUInt16(bytes, 5);
        AddUInt16(bytes, 3);
        AddUInt16(bytes, 2);
        AddUInt32(bytes, 123456u);
        AddFString(bytes, branch);
        AddUInt32(bytes, 3);
        bytes.AddRange([49, 56, 0]);
        AddUInt32(bytes, 522u);
        AddUInt32(bytes, 1009u);
        AddUInt32(bytes, branch == "++Ares-Core+release-13.00" ? 80u : 77u);
        AddInt32(bytes, 0);
        AddUInt32(bytes, 0);
        AddInt32(bytes, 0);
        AddUInt32(bytes, 0);
        AddUInt32(bytes, 0);
        AddUInt32(bytes, 0);
        AddUInt32(bytes, 0);
        AddFString(bytes, "Windows");
        bytes.Add(7);
        bytes.Add((byte)BuildTargetType.Client);
        return bytes.ToArray();
    }

    private static byte[] BuildOodlePayload(int decompressedSize, byte[] compressed)
    {
        var bytes = new byte[8 + compressed.Length];
        BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(0, 4), decompressedSize);
        BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(4, 4), compressed.Length);
        compressed.CopyTo(bytes.AsSpan(8));
        return bytes;
    }

    private static byte[] BuildDemoFrame(byte[] packet)
    {
        var bytes = new List<byte>();
        AddInt32(bytes, 7);
        AddSingle(bytes, 12.5f);
        AddIntPacked(bytes, 0);
        AddIntPacked(bytes, 0);
        AddIntPacked(bytes, 0);
        AddIntPacked(bytes, 0);
        AddInt32(bytes, packet.Length);
        bytes.AddRange(packet);
        AddInt32(bytes, 0);
        return bytes.ToArray();
    }

    private static byte[] BuildRawPacket()
    {
        var bits = new List<bool>();
        AddBit(bits, false); // bControl
        AddBit(bits, false); // bIsReplicationPaused
        AddBit(bits, false); // bReliable
        AddIntPackedBits(bits, 0);
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

    private static void AddByteArray(List<byte> bytes, byte[] value)
    {
        AddInt32(bytes, value.Length);
        bytes.AddRange(value);
    }

    private static void AddUnrealGuid(List<byte> bytes, uint a, uint b, uint c, uint d)
    {
        AddUInt32(bytes, a);
        AddUInt32(bytes, b);
        AddUInt32(bytes, c);
        AddUInt32(bytes, d);
    }

    private static void AddUInt16(List<byte> bytes, ushort value)
    {
        Span<byte> buffer = stackalloc byte[2];
        BinaryPrimitives.WriteUInt16LittleEndian(buffer, value);
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

    private static void AddInt64(List<byte> bytes, long value)
    {
        Span<byte> buffer = stackalloc byte[8];
        BinaryPrimitives.WriteInt64LittleEndian(buffer, value);
        bytes.AddRange(buffer.ToArray());
    }

    private static void AddSingle(List<byte> bytes, float value)
    {
        Span<byte> buffer = stackalloc byte[4];
        BinaryPrimitives.WriteSingleLittleEndian(buffer, value);
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

    private sealed class FakeOodleDecompressor : IOodleDecompressor
    {
        private readonly Queue<byte[]> _outputs;

        public FakeOodleDecompressor(params byte[][] outputs)
        {
            _outputs = new Queue<byte[]>(outputs);
        }

        public ReadOnlyMemory<byte> Decompress(ReadOnlySpan<byte> compressed, int decompressedSize)
        {
            var output = _outputs.Dequeue();
            Assert.That(output.Length, Is.EqualTo(decompressedSize));
            return output;
        }
    }

    private sealed class CallbackOodleDecompressor(byte[] output) : IOodleDecompressor
    {
        public Action? OnDecompress { get; set; }

        public ReadOnlyMemory<byte> Decompress(ReadOnlySpan<byte> compressed, int decompressedSize)
        {
            var callback = OnDecompress;
            OnDecompress = null;
            callback?.Invoke();
            Assert.That(output.Length, Is.EqualTo(decompressedSize));
            return output;
        }
    }

    private sealed class CapturingReplayDataChunkHandler : IReplayDataChunkHandler
    {
        public List<byte[]> Payloads { get; } = [];

        public void Handle(ReplayReaderContext context, FBinaryArchive replayDataArchive)
        {
            Payloads.Add(replayDataArchive.ReadBytes((int)replayDataArchive.Remaining).ToArray());
        }
    }
}
