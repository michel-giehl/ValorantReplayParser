using System.Buffers.Binary;
using Replay.Encoding.Archives;
using Replay.Models.Errors;
using Replay.Models.Protocol;
using Replay.Models.Replay;
using Replay.Models.Unreal;
using Replay.Unreal.Header;

namespace Replay.Unreal.Tests.Header;

public class ReplayHeaderReaderTests
{
    private static readonly Guid HeaderGuid = Guid.Parse("00112233-4455-6677-8899-aabbccddeeff");
    private static readonly byte[] HeaderSkip12_10 = [3, 0, 0, 0, 49, 56, 0];
    private static readonly byte[] HeaderSkip12_11 = [2, 0, 0, 0, 57, 0];

    [Test]
    public void Read_ParsesSupportedValorantHeader()
    {
        var archive = new FBinaryArchive(BuildHeader());

        var result = new ReplayHeaderReader(archive).Read();

        Assert.Multiple(() =>
        {
            Assert.That(result.Header.NetworkVersion, Is.EqualTo(Constants.ExpectedNetworkVersion));
            Assert.That(result.Header.NetworkChecksum, Is.EqualTo(0x11223344u));
            Assert.That(result.Header.EngineNetworkProtocolVersion,
                Is.EqualTo(Constants.ExpectedEngineNetworkProtocolVersion));
            Assert.That(result.Header.GameNetworkProtocolVersion, Is.EqualTo(0x55667788u));
            Assert.That(result.Header.Guid, Is.EqualTo(HeaderGuid));
            Assert.That(result.ReplayVersion.Major, Is.EqualTo(12));
            Assert.That(result.ReplayVersion.Minor, Is.EqualTo(10));
            Assert.That(result.ReplayVersion.Patch, Is.EqualTo(1));
            Assert.That(result.ReplayVersion.Changelist, Is.EqualTo(123456u));
            Assert.That(result.ReplayVersion.Branch, Is.EqualTo("++Ares-Core+release-12.10"));
            Assert.That(result.UEVersion.UE4Version, Is.EqualTo(1001u));
            Assert.That(result.UEVersion.UE5Version, Is.EqualTo(1002u));
            Assert.That(result.UEVersion.PackageVersionLicense, Is.EqualTo(1003u));
            Assert.That(result.Header.LevelNamesAndTimes, Is.EqualTo(new[] { ("Ascent", 42u) }));
            Assert.That(result.Header.Flags,
                Is.EqualTo(ReplayHeaderFlags.HasStreamingFixes | ReplayHeaderFlags.GameSpecificFrameData));
            Assert.That(result.Header.GameSpecificData, Is.EqualTo(new[] { "valorant", "competitive" }));
            Assert.That(result.Header.MinRecordHz, Is.EqualTo(15.0f));
            Assert.That(result.Header.MaxRecordHz, Is.EqualTo(30.0f));
            Assert.That(result.Header.FrameLimitInMs, Is.EqualTo(33.3f));
            Assert.That(result.Header.CheckpointLimitInMs, Is.EqualTo(250.0f));
            Assert.That(result.Header.Platform, Is.EqualTo("Windows"));
            Assert.That(result.Header.BuildConfig, Is.EqualTo(7));
            Assert.That(result.Header.BuildTargetType, Is.EqualTo(BuildTargetType.Client));
            Assert.That(archive.AtEnd, Is.True);
        });
    }

    [Test]
    public void Read_ParsesAlternateValorantHeaderSkipBytes()
    {
        var archive = new FBinaryArchive(BuildHeader(replayVersionSkipBytes: HeaderSkip12_11));

        var result = new ReplayHeaderReader(archive).Read();

        Assert.Multiple(() =>
        {
            Assert.That(result.Header.NetworkVersion, Is.EqualTo(Constants.ExpectedNetworkVersion));
            Assert.That(result.ReplayVersion.Branch, Is.EqualTo("++Ares-Core+release-12.10"));
            Assert.That(result.UEVersion.UE4Version, Is.EqualTo(1001u));
            Assert.That(archive.AtEnd, Is.True);
        });
    }

    [Test]
    public void Read_InvalidNetworkMagic_ThrowsInvalidReplayHeaderException()
    {
        var bytes = BuildHeader(networkMagic: 0xDEADBEEFu);
        var archive = new FBinaryArchive(bytes);

        Assert.Throws<InvalidReplayHeaderException>(() => new ReplayHeaderReader(archive).Read());
    }

    [Test]
    public void Read_NegativeCustomVersionCount_ThrowsInvalidReplayHeaderException()
    {
        var bytes = BuildHeader(customVersionCount: -1);
        var archive = new FBinaryArchive(bytes);

        Assert.Throws<InvalidReplayHeaderException>(() => new ReplayHeaderReader(archive).Read());
    }

    private static byte[] BuildHeader(
        uint networkMagic = Constants.NetworkMagic,
        int customVersionCount = 0,
        byte[]? replayVersionSkipBytes = null)
    {
        var bytes = new List<byte>();
        AddUInt32(bytes, networkMagic);
        AddUInt32(bytes, Constants.ExpectedNetworkVersion);
        AddInt32(bytes, customVersionCount);
        for (var i = 0; i < customVersionCount; i++)
        {
            bytes.AddRange(new byte[20]);
        }

        AddUInt32(bytes, 0x11223344u);
        AddUInt32(bytes, Constants.ExpectedEngineNetworkProtocolVersion);
        AddUInt32(bytes, 0x55667788u);
        AddUnrealGuid(bytes, 0x00112233u, 0x44556677u, 0x8899AABBu, 0xCCDDEEFFu);

        AddUInt16(bytes, 12);
        AddUInt16(bytes, 10);
        AddUInt16(bytes, 1);
        AddUInt32(bytes, 123456u);
        AddFString(bytes, "++Ares-Core+release-12.10");

        bytes.AddRange(replayVersionSkipBytes ?? HeaderSkip12_10);

        AddUInt32(bytes, 1001u);
        AddUInt32(bytes, 1002u);
        AddUInt32(bytes, 1003u);

        AddInt32(bytes, 1);
        AddFString(bytes, "Ascent");
        AddUInt32(bytes, 42u);

        AddUInt32(bytes, (uint)(ReplayHeaderFlags.HasStreamingFixes | ReplayHeaderFlags.GameSpecificFrameData));

        AddInt32(bytes, 2);
        AddFString(bytes, "valorant");
        AddFString(bytes, "competitive");

        AddSingle(bytes, 15.0f);
        AddSingle(bytes, 30.0f);
        AddSingle(bytes, 33.3f);
        AddSingle(bytes, 250.0f);
        AddFString(bytes, "Windows");
        bytes.Add(7);
        bytes.Add((byte)BuildTargetType.Client);

        return bytes.ToArray();
    }

    private static void AddFString(List<byte> bytes, string value)
    {
        var encoded = System.Text.Encoding.UTF8.GetBytes(value + '\0');
        AddInt32(bytes, encoded.Length);
        bytes.AddRange(encoded);
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

    private static void AddSingle(List<byte> bytes, float value)
    {
        AddUInt32(bytes, BitConverter.SingleToUInt32Bits(value));
    }

    private static void AddUnrealGuid(List<byte> bytes, uint a, uint b, uint c, uint d)
    {
        AddUInt32(bytes, a);
        AddUInt32(bytes, b);
        AddUInt32(bytes, c);
        AddUInt32(bytes, d);
    }
}
