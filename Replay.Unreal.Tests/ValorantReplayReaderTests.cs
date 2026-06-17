using System.Buffers.Binary;
using Replay.Encoding.Archives;
using Replay.Encoding.Compression;
using Replay.Models;

namespace Replay.Unreal.Tests;

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
            Assert.That(context.Errors, Is.Empty);
            Assert.That(context.ReplayInfo.HeaderChunkIndex, Is.EqualTo(0));
            Assert.That(context.ReplayHeader.Guid, Is.EqualTo(HeaderGuid));
            Assert.That(context.ReplayVersion.Branch, Is.EqualTo("++Ares+Release-12.10"));
        });
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
            Assert.That(context.Errors, Is.Empty);
            Assert.That(context.ReplayInfo.DataChunks, Has.Count.EqualTo(1));
            Assert.That(replayDataHandler.Payloads, Is.EqualTo(new[] { new byte[] { 0xAA, 0xBB } }));
        });
    }

    [Test]
    public void Read_EncryptedReplayData_RecordsInvalidReplayInfoError()
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

        var context = new ValorantReplayReader(new FakeOodleDecompressor([0xAA])).Read(archive);

        Assert.Multiple(() =>
        {
            Assert.That(context.Errors, Has.Count.EqualTo(1));
            Assert.That(context.Errors[0], Is.TypeOf<InvalidReplayInfoError>());
            Assert.That(context.Errors[0].Exception?.Message, Does.Contain("Encrypted VALORANT replay-data chunks are not supported"));
        });
    }

    [Test]
    public void Read_DuplicateHeaderChunks_RecordsInvalidReplayInfoError()
    {
        var archive = new FBinaryArchive(BuildReplayInfo(chunks: [HeaderChunk(BuildHeader()), HeaderChunk(BuildHeader())]));

        var context = new ValorantReplayReader(new FakeOodleDecompressor()).Read(archive);

        Assert.Multiple(() =>
        {
            Assert.That(context.Errors, Has.Count.EqualTo(1));
            Assert.That(context.Errors[0], Is.TypeOf<InvalidReplayInfoError>());
            Assert.That(context.Errors[0].Exception?.Message, Does.Contain("multiple header chunks"));
        });
    }

    [Test]
    public void Read_ReplayDataMetadataCannotReadPastDeclaredChunkSize_RecordsInvalidReplayInfoError()
    {
        var archive = new FBinaryArchive(BuildReplayInfo(chunks:
        [
            HeaderChunk(BuildHeader()),
            RawChunk(ReplayChunkType.ReplayData, 15, new byte[15]),
        ]));

        var context = new ValorantReplayReader(new FakeOodleDecompressor()).Read(archive);

        Assert.Multiple(() =>
        {
            Assert.That(context.Errors, Has.Count.EqualTo(1));
            Assert.That(context.Errors[0], Is.TypeOf<InvalidReplayInfoError>());
            Assert.That(context.Errors[0].Exception, Is.TypeOf<ArchiveReadException>());
        });
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

        var context = new ValorantReplayReader(replayDataChunkHandler: new NoOpReplayDataChunkHandler()).Read(archive);

        Assert.Multiple(() =>
        {
            Assert.That(context.Errors, Is.Empty);
            Assert.That(context.ReplayInfo.DataChunks, Has.Count.EqualTo(2));
            Assert.That(context.ReplayInfo.DataChunks[0].Time1, Is.EqualTo(1u));
            Assert.That(context.ReplayInfo.DataChunks[0].Time2, Is.EqualTo(10u));
            Assert.That(context.ReplayInfo.DataChunks[1].Time1, Is.EqualTo(10u));
            Assert.That(context.ReplayInfo.DataChunks[1].Time2, Is.EqualTo(20u));
            Assert.That(context.ReplayInfo.TotalDataSizeInBytes, Is.EqualTo(5));
        });
    }

    [Test]
    public void Read_ValidChunks_TracksHeaderOffset()
    {
        var archive = new FBinaryArchive(BuildReplayInfo(chunks: [UnknownChunk([0x01]), HeaderChunk(BuildHeader())]));

        var context = new ValorantReplayReader().Read(archive);

        Assert.Multiple(() =>
        {
            Assert.That(context.Errors, Is.Empty);
            Assert.That(context.ReplayInfo.Chunks, Has.Count.EqualTo(2));
            Assert.That(context.ReplayInfo.HeaderChunkIndex, Is.EqualTo(1));
            Assert.That(context.ReplayInfo.Chunks[1].DataOffset, Is.GreaterThan(context.ReplayInfo.Chunks[0].DataOffset));
        });
    }

    [Test]
    public void Read_ReplayDataFrame_AddsPlaybackPacket()
    {
        var packet = new byte[] { 0xAA, 0xBB, 0xCC };
        var frame = BuildDemoFrame(packet);
        var archive = new FBinaryArchive(BuildReplayInfo(chunks:
        [
            HeaderChunk(BuildHeader()),
            ReplayDataChunk(100, 200, frame, memorySizeInBytes: frame.Length),
        ]));

        var context = new ValorantReplayReader().Read(archive);

        Assert.Multiple(() =>
        {
            Assert.That(context.Errors, Is.Empty);
            Assert.That(context.PlaybackPackets, Has.Count.EqualTo(1));
            Assert.That(context.PlaybackPackets[0].ReplayDataChunkIndex, Is.EqualTo(1));
            Assert.That(context.PlaybackPackets[0].CurrentLevelIndex, Is.EqualTo(7));
            Assert.That(context.PlaybackPackets[0].TimeSeconds, Is.EqualTo(12.5f));
            Assert.That(context.PlaybackPackets[0].Data, Is.EqualTo(packet));
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
        AddInt32(data, memorySizeInBytes ?? (payload.Length == 0 ? 0 : BinaryPrimitives.ReadInt32LittleEndian(payload.AsSpan(0, 4))));
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

    private static byte[] BuildHeader()
    {
        var bytes = new List<byte>();
        AddUInt32(bytes, Constants.NetworkMagic);
        AddUInt32(bytes, Constants.ExpectedNetworkVersion);
        AddInt32(bytes, 0);
        AddUInt32(bytes, 0x11223344u);
        AddUInt32(bytes, Constants.ExpectedEngineNetworkProtocolVersion);
        AddUInt32(bytes, 0x55667788u);
        AddUnrealGuid(bytes, 0x00112233u, 0x44556677u, 0x8899AABBu, 0xCCDDEEFFu);
        AddUInt16(bytes, 12);
        AddUInt16(bytes, 10);
        AddUInt16(bytes, 1);
        AddUInt32(bytes, 123456u);
        AddFString(bytes, "++Ares+Release-12.10");
        AddUInt32(bytes, 3);
        bytes.AddRange([49, 56, 0]);
        AddUInt32(bytes, 1001u);
        AddUInt32(bytes, 1002u);
        AddUInt32(bytes, 1003u);
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

        public int Decompress(ReadOnlySpan<byte> compressed, Span<byte> destination)
        {
            var output = _outputs.Dequeue();
            output.CopyTo(destination);
            return output.Length;
        }
    }

    private sealed class CapturingReplayDataChunkHandler : IReplayDataChunkHandler
    {
        public List<byte[]> Payloads { get; } = [];

        public void Handle(ReplayReaderContext context, ReplayDataChunkInfo dataChunk, FBinaryArchive replayDataArchive)
        {
            Payloads.Add(replayDataArchive.ReadBytes((int)replayDataArchive.Remaining).ToArray());
        }
    }
}
