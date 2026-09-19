using Replay.Encoding.Archives;
using Replay.Models.Errors;
using Replay.Models.Descriptors;
using Replay.Models.Events;
using Replay.Models.Replay;
using Replay.Unreal.Header;
using Replay.Unreal.Readers;
using Replay.Valorant;
using Snapshooter.NUnit;
using System.Runtime.ExceptionServices;

namespace Test.Integration;

[Category("Integration")]
public class ReplayReaderIntegrationTests
{
    private const string Replay12_08 = "c96127a8-f003-48db-a2cd-9c71de5aba15.12_08.vrf";
    private const string Replay13_00 = "12974d2b-848f-490d-80ba-5f03a033c2d5.13_00.vrf";
    private const string Branch12_08 = "++Ares-Core+release-12.08";

    [Test]
    public void ReadReplay_12_08_ReportsUnsupportedVersion() =>
        ReadReplayReportsUnsupportedVersion(Replay12_08, Branch12_08);

    [Test]
    public void ReadMetadata_12_08_ReturnsUnsupportedVersionWithoutReadingReplayData()
    {
        var archive = new FBinaryArchive(TestHelper.ReadReplayBytes(Replay12_08));

        var metadata = new ValorantReplayReader().ReadMetadata(archive);

        Assert.Multiple(() =>
        {
            Assert.That(metadata.FullParseSupportStatus, Is.EqualTo(ValorantReplaySupportStatus.UnsupportedVersion));
            Assert.That(metadata.FullParseUnsupportedReason, Does.Contain(Branch12_08));
            Assert.That(metadata.ReplayInfo.Chunks, Has.Count.EqualTo(1));
            Assert.That(metadata.ReplayInfo.DataChunks, Is.Empty);
            Assert.That(archive.AtEnd, Is.False);
        });
    }

    [Test]
    public void ReadReplayInfo_12_10_MatchesSnapshot() =>
        ReadReplayInfoMatchesSnapshot("9f8b32c5-c243-41ec-bbbb-832582edf652.12_10.vrf");

    [Test]
    public void ReadReplayInfo_12_11_MatchesSnapshot() =>
        ReadReplayInfoMatchesSnapshot("5c673443-5bdc-4576-b416-aab3f62471a5.12_11.vrf");

    [Test]
    public void ReadReplayInfo_13_00_MatchesSnapshot() =>
        ReadReplayInfoMatchesSnapshot(Replay13_00);

    [Test]
    public void ReadReplayHeader_12_10_MatchesSnapshot() =>
        ReadReplayHeaderMatchesSnapshot("9f8b32c5-c243-41ec-bbbb-832582edf652.12_10.vrf");

    [Test]
    public void ReadReplayHeader_12_11_MatchesSnapshot() =>
        ReadReplayHeaderMatchesSnapshot("5c673443-5bdc-4576-b416-aab3f62471a5.12_11.vrf");

    [Test]
    public void ReadReplayHeader_13_00_MatchesSnapshot() =>
        ReadReplayHeaderMatchesSnapshot(Replay13_00);

    [Test]
    public void DecompressReplayData_12_10_MaterializesExpectedSize() =>
        DecompressReplayDataMaterializesExpectedSize("9f8b32c5-c243-41ec-bbbb-832582edf652.12_10.vrf");

    [Test]
    public void DecompressReplayData_12_11_MaterializesExpectedSize() =>
        DecompressReplayDataMaterializesExpectedSize("5c673443-5bdc-4576-b416-aab3f62471a5.12_11.vrf");

    [Test]
    public void DecompressReplayData_13_00_MaterializesExpectedSize() =>
        DecompressReplayDataMaterializesExpectedSize(Replay13_00);

    [Test]
    public void ReadRawPackets_12_10_RecordsStats() =>
        ReadRawPacketsRecordsStats("9f8b32c5-c243-41ec-bbbb-832582edf652.12_10.vrf", expectedPartialErrors: 2, expectedMalformedPayloads: 0);

    [Test]
    public void ReadRawPackets_12_11_RecordsStats() =>
        ReadRawPacketsRecordsStats("5c673443-5bdc-4576-b416-aab3f62471a5.12_11.vrf", expectedPartialErrors: 2, expectedMalformedPayloads: 0);

    [Test]
    public void ReadRawPackets_13_00_RecordsStats() =>
        ReadRawPacketsRecordsStats(Replay13_00, expectedPartialErrors: 2, expectedMalformedPayloads: 0);

    [Test]
    public void ReadRawPackets_12_11_EmitsTimedParserEvents()
    {
        var replayBytes = TestHelper.ReadReplayBytes("5c673443-5bdc-4576-b416-aab3f62471a5.12_11.vrf");
        var eventSink = new CapturingReplayEventSink();
        var result = new ValorantReplayReader(eventSink: eventSink).Read(new FBinaryArchive(replayBytes));

        var spawnedEvents = eventSink.Events.OfType<ActorSpawned>().ToArray();
        var closedEvents = eventSink.Events.OfType<ActorClosed>().ToArray();
        var exportGroupEvents = eventSink.Events.OfType<ExportGroupReceived>().ToArray();
        Assert.Multiple(() =>
        {
            Assert.That(result.BunchPayloadStats.ActorChannelOpenCount, Is.GreaterThan(0));
            Assert.That(result.BunchPayloadStats.ContentBlockCount, Is.GreaterThan(0));
            Assert.That(result.ExportGroups, Is.Not.Empty);
            Assert.That(spawnedEvents, Is.Not.Empty);
            Assert.That(closedEvents, Is.Not.Empty);
            Assert.That(exportGroupEvents, Is.Not.Empty);
            Assert.That(spawnedEvents.All(replayEvent => replayEvent.TimeSeconds >= 0f), Is.True);
            Assert.That(spawnedEvents.All(replayEvent =>
                replayEvent.TimeSeconds <= result.Metadata.ReplayInfo.LengthInMs / 1000f + 1f), Is.True);
        });
    }

    [TestCase(false)]
    [TestCase(true)]
    public void Read_ConsumerSinkExceptionIdentityIsPreserved(bool archiveReadException)
    {
        Exception expectedException = archiveReadException
            ? new ArchiveReadException(ArchiveErrorCode.InvalidCount, "consumer sink", 0, 1, 1)
            : new InvalidOperationException("consumer sink failed");
        var sink = new ThrowingReplayEventSink(expectedException);
        var reader = new ValorantReplayReader(eventSink: sink);
        var archive = new FBinaryArchive(TestHelper.ReadReplayBytes("5c673443-5bdc-4576-b416-aab3f62471a5.12_11.vrf"));

        var thrown = Assert.Catch(() => reader.Read(archive));

        Assert.That(thrown, Is.SameAs(expectedException));
    }

    private static void ReadReplayInfoMatchesSnapshot(string replayFileName)
    {
        var replayBytes = TestHelper.ReadReplayBytes(replayFileName);
        var metadata = ReadReplayMetadata(replayBytes);

        Snapshot.Match(CreateReplayInfoSnapshot(replayFileName, metadata));
    }

    private static void ReadReplayReportsUnsupportedVersion(string replayFileName, string branch)
    {
        var replayBytes = TestHelper.ReadReplayBytes(replayFileName);
        var exception = Assert.Throws<InvalidReplayInfoException>(() => ReadReplay(replayBytes));

        Assert.Multiple(() =>
        {
            Assert.That(exception!.Message, Does.Contain("Unsupported VALORANT replay version"));
            Assert.That(exception.Message, Does.Contain(branch));
        });
    }

    private static void ReadReplayHeaderMatchesSnapshot(string replayFileName)
    {
        var replayBytes = TestHelper.ReadReplayBytes(replayFileName);
        var headerBytes = ReadHeaderPayload(replayBytes);
        var headerArchive = new FBinaryArchive(headerBytes);

        var readResult = new ReplayHeaderReader(headerArchive).Read();

        Snapshot.Match(CreateReplayHeaderSnapshot(replayFileName, readResult, headerArchive, headerBytes.Length));
    }

    private static void DecompressReplayDataMaterializesExpectedSize(string replayFileName)
    {
        var replayBytes = TestHelper.ReadReplayBytes(replayFileName);
        var archive = new FBinaryArchive(replayBytes);

        var result = new ValorantReplayReader(parseProfile: ParseProfile.Minimal).Read(archive);

        Assert.Multiple(() =>
        {
            Assert.That(result.Metadata.ReplayInfo.Compressed, Is.True);
            Assert.That(result.PacketStats.PacketCount, Is.GreaterThan(0));
            Assert.That(result.Metadata.ReplayInfo.DataChunks.Sum(chunk => chunk.MemorySizeInBytes),
                Is.EqualTo(result.Metadata.ReplayInfo.TotalDataSizeInBytes));
        });
    }

    private static void ReadRawPacketsRecordsStats(string replayFileName, int expectedPartialErrors, int expectedMalformedPayloads)
    {
        var replayBytes = TestHelper.ReadReplayBytes(replayFileName);
        var result = ReadReplay(replayBytes);
        var stats = result.PacketStats;
        var payloadStats = result.BunchPayloadStats;

        Assert.Multiple(() =>
        {
            Assert.That(result.ExportGroups, Is.Not.Empty);
            Assert.That(result.ExportGroups.Any(group => group.Fields.Count > 0), Is.True);
            Assert.That(stats.PacketCount, Is.GreaterThan(0));
            Assert.That(stats.TotalPacketBytes, Is.GreaterThan(0));
            Assert.That(stats.PacketsWithBunches, Is.GreaterThan(0));
            Assert.That(stats.BunchCount, Is.GreaterThan(0));
            Assert.That(stats.MalformedPacketCount, Is.EqualTo(0));
            Assert.That(stats.PartialErrorCount, Is.EqualTo(expectedPartialErrors));
            Assert.That(stats.MinTimeSeconds, Is.GreaterThanOrEqualTo(0f));
            Assert.That(stats.MaxTimeSeconds,
                Is.LessThanOrEqualTo(result.Metadata.ReplayInfo.LengthInMs / 1000f + 1f));
            Assert.That(payloadStats.PayloadBunchCount, Is.GreaterThan(0));
            Assert.That(payloadStats.ContentBlockCount, Is.GreaterThan(0));
            Assert.That(payloadStats.PartialErrorCount, Is.EqualTo(expectedPartialErrors));
            Assert.That(payloadStats.MalformedPayloadCount, Is.EqualTo(expectedMalformedPayloads),
                $"Exceptions={payloadStats.MalformedPayloadExceptionCount}, MustMap={payloadStats.MalformedMustBeMappedGuidCount}, ActorOpen={payloadStats.MalformedActorOpenCount}, Content={payloadStats.MalformedContentBlockCount}, Trailing={payloadStats.TrailingPayloadCount}");
        });
    }

    private static ValorantReplayReadResult ReadReplay(byte[] replayBytes)
    {
        var archive = new FBinaryArchive(replayBytes);
        return new ValorantReplayReader(parseProfile: ParseProfile.Minimal).Read(archive);
    }


    private static ValorantReplayMetadata ReadReplayMetadata(byte[] replayBytes)
    {
        var archive = new FBinaryArchive(replayBytes);
        return new ValorantReplayReader(parseProfile: ParseProfile.Minimal).Read(archive).Metadata;
    }



    private static byte[] ReadHeaderPayload(byte[] replayBytes)
    {
        var metadata = ReadReplayMetadata(replayBytes);

        if (metadata.ReplayInfo.HeaderChunkIndex == ReplayInfo.NoChunkIndex)
        {
            throw new InvalidOperationException("Replay info did not contain a header chunk.");
        }

        var headerChunk = metadata.ReplayInfo.Chunks[metadata.ReplayInfo.HeaderChunkIndex];
        return replayBytes
            .AsSpan(checked((int)headerChunk.DataOffset), headerChunk.SizeInBytes)
            .ToArray();
    }

    private static object CreateReplayInfoSnapshot(
        string replayFileName,
        ValorantReplayMetadata metadata)
    {
        var info = metadata.ReplayInfo;
        var serializationMetadata = metadata.ReplayInfoSerializationMetadata;

        return new
        {
            ReplayFileName = replayFileName,
            Info = new
            {
                info.LengthInMs,
                info.NetworkVersion,
                info.Changelist,
                info.FriendlyName,
                info.Timestamp,
                info.TotalDataSizeInBytes,
                info.IsLive,
                info.IsValid,
                info.Compressed,
                info.Encrypted,
                EncryptionKeyLength = info.EncryptionKey.Length,
                EncryptionKey = Convert.ToHexString(info.EncryptionKey),
                info.HeaderChunkIndex,
            },
            SerializationMetadata = new
            {
                serializationMetadata.FileVersion,
                serializationMetadata.FileFriendlyName,
                FileCustomVersions = serializationMetadata.FileCustomVersions.Versions
                    .Select(version => new
                    {
                        Key = version.Key.ToString("D"),
                        version.Version,
                        version.FriendlyName,
                    })
                    .ToArray(),
            },
            Scan = new
            {
                HeaderChunkPayloadOffset = info.Chunks[info.HeaderChunkIndex].DataOffset,
                ChunkCount = info.Chunks.Count,
                DataChunkCount = info.DataChunks.Count,
                HeaderChunk = ToChunkSnapshot(info.Chunks[info.HeaderChunkIndex]),
                FirstDataChunk = ToDataChunkSnapshot(info.DataChunks.FirstOrDefault()),
                LastDataChunk = ToDataChunkSnapshot(info.DataChunks.LastOrDefault()),
            },
        };
    }

    private static object CreateReplayHeaderSnapshot(
        string replayFileName,
        ReplayHeaderReadResult readResult,
        FBinaryArchive headerArchive,
        int headerPayloadLength)
    {
        var header = readResult.Header;
        var replayVersion = readResult.ReplayVersion;
        var ueVersion = readResult.UEVersion;

        return new
        {
            ReplayFileName = replayFileName,
            HeaderPayloadLength = headerPayloadLength,
            HeaderArchivePosition = headerArchive.Position,
            headerArchive.AtEnd,
            Header = new
            {
                header.NetworkVersion,
                header.NetworkChecksum,
                header.EngineNetworkProtocolVersion,
                header.GameNetworkProtocolVersion,
                Guid = header.Guid.ToString("D"),
                header.MinRecordHz,
                header.MaxRecordHz,
                header.FrameLimitInMs,
                header.CheckpointLimitInMs,
                LevelNamesAndTimes = header.LevelNamesAndTimes
                    .Select(level => new
                    {
                        level.LevelName,
                        level.TimeInMs,
                    })
                    .ToArray(),
                Flags = header.Flags.ToString(),
                header.Platform,
                header.BuildConfig,
                BuildTargetType = header.BuildTargetType.ToString(),
            },
            ReplayVersion = new
            {
                replayVersion.Major,
                replayVersion.Minor,
                replayVersion.Patch,
                replayVersion.Changelist,
                replayVersion.Branch,
            },
            UEVersion = new
            {
                ueVersion.UE4Version,
                ueVersion.UE5Version,
                ueVersion.PackageVersionLicense,
            },
        };
    }

    private static object? ToChunkSnapshot(ReplayChunkInfo? chunk)
    {
        if (chunk is null)
        {
            return null;
        }

        return new
        {
            ChunkType = chunk.ChunkType.ToString(),
            chunk.SizeInBytes,
            chunk.TypeOffset,
            chunk.DataOffset,
        };
    }

    private static object? ToDataChunkSnapshot(ReplayDataChunkInfo? chunk)
    {
        if (chunk is null)
        {
            return null;
        }

        return new
        {
            chunk.ChunkIndex,
            chunk.Time1,
            chunk.Time2,
            chunk.SizeInBytes,
            chunk.MemorySizeInBytes,
            chunk.ReplayDataOffset,
            chunk.StreamOffset,
        };
    }

    private sealed class CapturingReplayEventSink : IReplayEventSink
    {
        public List<ReplayEvent> Events { get; } = [];

        public void Emit(ReplayEvent replayEvent) => Events.Add(replayEvent);
    }

    private sealed class ThrowingReplayEventSink(Exception exception) : IReplayEventSink
    {
        public void Emit(ReplayEvent replayEvent) => ExceptionDispatchInfo.Capture(exception).Throw();
    }

}
