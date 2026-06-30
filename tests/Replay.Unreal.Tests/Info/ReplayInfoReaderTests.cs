using System.Buffers.Binary;
using Replay.Encoding.Archives;
using Replay.Models.Errors;
using Replay.Models.Replay;
using Replay.Unreal.Info;

namespace Replay.Unreal.Tests.Info;

public class ReplayInfoReaderTests
{
    private const uint FileMagic = 0x43F4EFDD;
    private const uint BadMagic = 0xDEADBEEF;
    private const uint LocalReplayGuidA = 0x95A4F03E;
    private const uint LocalReplayGuidB = 0x7E0B49E4;
    private const uint LocalReplayGuidC = 0xBA43D356;
    private const uint LocalReplayGuidD = 0x94FF87D9;
    private static readonly Guid LocalReplayGuid = Guid.Parse("95A4F03E-7E0B-49E4-BA43-D35694FF87D9");

    [Test]
    public void Read_EmptyArchive_ThrowsInvalidReplayInfoException()
    {
        var archive = new FBinaryArchive(ReadOnlySpan<byte>.Empty);

        Assert.Throws<InvalidReplayInfoException>(() => Read(archive));
    }

    [Test]
    public void Read_BadMagicWithoutSkipHeader_ThrowsInvalidReplayInfoException()
    {
        var archive = new FBinaryArchive(BuildReplayInfo(magic: BadMagic, chunks: []));

        Assert.Throws<InvalidReplayInfoException>(() => Read(archive));
    }

    [Test]
    public void Read_LegacyFileVersion_ThrowsInvalidReplayInfoException()
    {
        var archive = new FBinaryArchive(BuildReplayInfo(fileVersion: 0, chunks: [HeaderChunk([0xAA])]));

        Assert.Throws<InvalidReplayInfoException>(() => Read(archive));
    }

    [Test]
    public void Read_CustomVersionPath_ParsesSummary()
    {
        var archive = new FBinaryArchive(BuildReplayInfo(
            friendlyName: "Match  ",
            timestamp: 123456789,
            chunks: [UnknownChunk([0x01, 0x02]), HeaderChunk([0xAA, 0xBB])]));

        var result = Read(archive);

        Assert.Multiple(() =>
        {
            Assert.That(result.SerializationMetadata.FileVersion, Is.EqualTo(7u));
            Assert.That(result.SerializationMetadata.FileCustomVersions.GetVersion(LocalReplayGuid), Is.EqualTo(7));
            Assert.That(result.SerializationMetadata.FileFriendlyName, Is.EqualTo("Match  "));
            Assert.That(result.Info.LengthInMs, Is.EqualTo(60000));
            Assert.That(result.Info.NetworkVersion, Is.EqualTo(19u));
            Assert.That(result.Info.Changelist, Is.EqualTo(1234u));
            Assert.That(result.Info.FriendlyName, Is.EqualTo("Match"));
            Assert.That(result.Info.Timestamp, Is.EqualTo(123456789));
            Assert.That(result.Info.Chunks, Is.Empty);
        });
    }

    [Test]
    public void Read_MissingCustomVersion_ThrowsInvalidReplayInfoException()
    {
        var archive = new FBinaryArchive(BuildReplayInfo(customVersion: null, chunks: [HeaderChunk([0xAA])]));

        Assert.Throws<InvalidReplayInfoException>(() => Read(archive));
    }

    [Test]
    public void Read_NewerCustomVersion_ThrowsInvalidReplayInfoException()
    {
        var archive = new FBinaryArchive(BuildReplayInfo(customVersion: 8, chunks: [HeaderChunk([0xAA])]));

        Assert.Throws<InvalidReplayInfoException>(() => Read(archive));
    }

    [Test]
    public void Read_FriendlyNameExceedsBound_ThrowsInvalidReplayInfoException()
    {
        var bytes = new List<byte>();
        AddReplayInfoPrefix(bytes, fileVersion: 7, customVersion: 7);
        AddInt32(bytes, 60000);
        AddUInt32(bytes, 19);
        AddUInt32(bytes, 1234);
        AddInt32(bytes, 64 * 1024 + 1);

        var archive = new FBinaryArchive(bytes.ToArray());

        Assert.Throws<InvalidReplayInfoException>(() => Read(archive));
    }

    [Test]
    public void Read_OlderCustomVersion_ThrowsInvalidReplayInfoException()
    {
        var archive = new FBinaryArchive(BuildReplayInfo(
            customVersion: 6,
            chunks: [HeaderChunk([0xAA])]));

        Assert.Throws<InvalidReplayInfoException>(() => Read(archive));
    }

    [Test]
    public void Read_EncryptionKeyTooLarge_ThrowsInvalidReplayInfoException()
    {
        var bytes = BuildReplayInfo(
            encrypted: true,
            encryptionKeySizeOverride: 4097,
            chunks: [HeaderChunk([0xAA])]);
        var archive = new FBinaryArchive(bytes);

        Assert.Throws<InvalidReplayInfoException>(() => Read(archive));
    }

    [Test]
    public void Read_CompletedEncryptedReplayWithoutKey_ThrowsInvalidReplayInfoException()
    {
        var archive = new FBinaryArchive(BuildReplayInfo(
            isLive: false,
            encrypted: true,
            encryptionKey: [],
            chunks: [HeaderChunk([0xAA])]));

        Assert.Throws<InvalidReplayInfoException>(() => Read(archive));
    }

    private static ReplayInfoReadResult Read(FBinaryArchive archive) =>
        new ReplayInfoReader(archive).Read();

    private static byte[] BuildReplayInfo(
        uint magic = FileMagic,
        uint fileVersion = 7,
        int? customVersion = 7,
        string friendlyName = "Replay",
        bool isLive = false,
        long timestamp = 42,
        bool compressed = false,
        bool encrypted = false,
        byte[]? encryptionKey = null,
        int? encryptionKeySizeOverride = null,
        params byte[][] chunks)
    {
        var bytes = new List<byte>();
        AddReplayInfoPrefix(bytes, magic, fileVersion, customVersion);

        AddInt32(bytes, 60000);
        AddUInt32(bytes, 19);
        AddUInt32(bytes, 1234);
        AddFString(bytes, friendlyName);
        AddUInt32(bytes, isLive ? 1u : 0u);
        AddInt64(bytes, timestamp);
        AddUInt32(bytes, compressed ? 1u : 0u);
        AddUInt32(bytes, encrypted ? 1u : 0u);
        if (encryptionKeySizeOverride is { } keySize)
        {
            AddInt32(bytes, keySize);
        }
        else
        {
            AddByteArray(bytes, encryptionKey ?? []);
        }

        foreach (var chunk in chunks)
        {
            bytes.AddRange(chunk);
        }

        return bytes.ToArray();
    }

    private static void AddReplayInfoPrefix(
        List<byte> bytes,
        uint magic = FileMagic,
        uint fileVersion = 7,
        int? customVersion = 7)
    {
        AddUInt32(bytes, magic);
        AddUInt32(bytes, fileVersion);

        if (fileVersion < 7)
        {
            return;
        }

        if (customVersion is { } version)
        {
            AddInt32(bytes, 1);
            AddUnrealGuid(bytes, LocalReplayGuidA, LocalReplayGuidB, LocalReplayGuidC, LocalReplayGuidD);
            AddInt32(bytes, version);
        }
        else
        {
            AddInt32(bytes, 0);
        }
    }

    private static byte[] HeaderChunk(byte[] payload) => RawChunk(ReplayChunkType.Header, payload.Length, payload);

    private static byte[] UnknownChunk(byte[] payload) => RawChunk(ReplayChunkType.Unknown, payload.Length, payload);

    private static byte[] RawChunk(ReplayChunkType chunkType, int sizeInBytes, byte[] payload)
    {
        var bytes = new List<byte>();
        AddUInt32(bytes, (uint)chunkType);
        AddInt32(bytes, sizeInBytes);
        bytes.AddRange(payload);
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
}
