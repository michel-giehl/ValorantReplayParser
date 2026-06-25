using Replay.Encoding.Archives;
using Replay.Encoding.Compression;
using Replay.Models.Errors;
using Replay.Models.Events;
using Replay.Models.Replay;
using Replay.Unreal.Chunks;
using Replay.Unreal.Header;
using Replay.Unreal.Readers;
using Replay.Valorant.Descriptors;
using Snapshooter.NUnit;

namespace Test.Integration;

[Category("Integration")]
public class ReplayReaderIntegrationTests
{
    private const string Replay12_08 = "c96127a8-f003-48db-a2cd-9c71de5aba15.12_08.vrf";
    private const string Branch12_08 = "++Ares-Core+release-12.08";

    [Test]
    public void ReadReplayInfo_12_08_ReportsUnsupportedPayloadTransform() =>
        ReadReplayReportsUnsupportedPayloadTransform(Replay12_08, Branch12_08);

    [Test]
    public void ReadReplayInfo_12_10_MatchesSnapshot() =>
        ReadReplayInfoMatchesSnapshot("9f8b32c5-c243-41ec-bbbb-832582edf652.12_10.vrf");

    [Test]
    public void ReadReplayInfo_12_11_MatchesSnapshot() =>
        ReadReplayInfoMatchesSnapshot("5c673443-5bdc-4576-b416-aab3f62471a5.12_11.vrf");

    [Test]
    public void ReadReplayHeader_12_08_ReportsUnsupportedPayloadTransform() =>
        ReadReplayReportsUnsupportedPayloadTransform(Replay12_08, Branch12_08);

    [Test]
    public void ReadReplayHeader_12_10_MatchesSnapshot() =>
        ReadReplayHeaderMatchesSnapshot("9f8b32c5-c243-41ec-bbbb-832582edf652.12_10.vrf");

    [Test]
    public void ReadReplayHeader_12_11_MatchesSnapshot() =>
        ReadReplayHeaderMatchesSnapshot("5c673443-5bdc-4576-b416-aab3f62471a5.12_11.vrf");

    [Test]
    public void DecompressReplayData_12_08_MaterializesExpectedSize() =>
        DecompressReplayDataMaterializesExpectedSize(Replay12_08);

    [Test]
    public void DecompressReplayData_12_10_MaterializesExpectedSize() =>
        DecompressReplayDataMaterializesExpectedSize("9f8b32c5-c243-41ec-bbbb-832582edf652.12_10.vrf");

    [Test]
    public void DecompressReplayData_12_11_MaterializesExpectedSize() =>
        DecompressReplayDataMaterializesExpectedSize("5c673443-5bdc-4576-b416-aab3f62471a5.12_11.vrf");

    [Test]
    public void ReadRawPackets_12_08_ReportsUnsupportedPayloadTransform() =>
        ReadReplayReportsUnsupportedPayloadTransform(Replay12_08, Branch12_08);

    [Test]
    public void ReadRawPackets_12_10_RecordsStats() =>
        ReadRawPacketsRecordsStats("9f8b32c5-c243-41ec-bbbb-832582edf652.12_10.vrf", expectedPartialErrors: 2, expectedMalformedPayloads: 0);

    [Test]
    public void ReadRawPackets_12_11_RecordsStats() =>
        ReadRawPacketsRecordsStats("5c673443-5bdc-4576-b416-aab3f62471a5.12_11.vrf", expectedPartialErrors: 2, expectedMalformedPayloads: 0);

    [Test]
    public void ReadBaseReplayController_12_08_ReportsUnsupportedPayloadTransform() =>
        ReadReplayReportsUnsupportedPayloadTransform(Replay12_08, Branch12_08);

    [Test]
    public void ReadRawPackets_12_11_BuildsWorldStateAndEmitsTimedActorEvents()
    {
        var replayBytes = ReadReplayBytes("5c673443-5bdc-4576-b416-aab3f62471a5.12_11.vrf");
        var eventSink = new CapturingReplayEventSink();
        var context = new ValorantReplayReader(
            new OozSharpOodleDecompressor(),
            eventSink: eventSink,
            descriptorCatalog: ValorantDescriptors.CreateCatalog()).Read(new FBinaryArchive(replayBytes));

        var openedEvents = eventSink.Events.OfType<ActorOpened>().ToArray();
        var spawnedEvents = eventSink.Events.OfType<ActorSpawned>().ToArray();
        Assert.Multiple(() =>
        {
            Assert.That(context.Errors, Is.Empty);
            Assert.That(context.WorldState.Channels, Is.Not.Empty);
            Assert.That(context.WorldState.ActorsByNetGuid, Is.Not.Empty);
            Assert.That(context.WorldState.ObjectsByNetGuid, Is.Not.Empty);
            Assert.That(context.WorldState.ActorChannelHistory, Is.Not.Empty);
            Assert.That(context.BunchPayloadStats.ActorCreatedCount, Is.GreaterThan(0));
            Assert.That(context.BunchPayloadStats.SubobjectCreatedCount, Is.GreaterThan(0));
            Assert.That(openedEvents, Is.Not.Empty);
            Assert.That(spawnedEvents, Is.Not.Empty);
            Assert.That(openedEvents.All(replayEvent => replayEvent.TimeSeconds >= 0f), Is.True);
            Assert.That(openedEvents.All(replayEvent =>
                replayEvent.TimeSeconds <= context.ReplayInfo.LengthInMs / 1000f + 1f), Is.True);
        });
    }

    private static void ReadReplayInfoMatchesSnapshot(string replayFileName)
    {
        var replayBytes = ReadReplayBytes(replayFileName);
        var context = ReadReplay(replayBytes);

        Assert.That(context.Errors, Is.Empty);

        Snapshot.Match(CreateReplayInfoSnapshot(replayFileName, context));
    }

    private static void ReadReplayReportsUnsupportedPayloadTransform(string replayFileName, string branch)
    {
        var replayBytes = ReadReplayBytes(replayFileName);
        var context = ReadReplay(replayBytes);

        var error = context.Errors.Single();
        Assert.Multiple(() =>
        {
            Assert.That(error, Is.TypeOf<InvalidReplayInfoError>());
            Assert.That(error.Exception, Is.TypeOf<InvalidReplayInfoException>());
            Assert.That(error.Exception!.Message, Does.Contain("Unsupported VALORANT property payload transform"));
            Assert.That(error.Exception.Message, Does.Contain(branch));
        });
    }

    private static void ReadReplayHeaderMatchesSnapshot(string replayFileName)
    {
        var replayBytes = ReadReplayBytes(replayFileName);
        var headerBytes = ReadHeaderPayload(replayBytes);
        var headerArchive = new FBinaryArchive(headerBytes);

        var readResult = new ReplayHeaderReader(headerArchive).Read();

        Snapshot.Match(CreateReplayHeaderSnapshot(replayFileName, readResult, headerArchive, headerBytes.Length));
    }

    private static void DecompressReplayDataMaterializesExpectedSize(string replayFileName)
    {
        var replayBytes = ReadReplayBytes(replayFileName);
        var replayDataHandler = new CountingReplayDataChunkHandler();
        var archive = new FBinaryArchive(replayBytes);

        var context = new ValorantReplayReader(new OozSharpOodleDecompressor(), replayDataHandler).Read(archive);

        Assert.Multiple(() =>
        {
            Assert.That(context.Errors, Is.Empty);
            Assert.That(context.ReplayInfo.Compressed, Is.True);
            Assert.That(replayDataHandler.TotalPayloadBytes, Is.EqualTo(context.ReplayInfo.TotalDataSizeInBytes));
        });
    }

    private static void ReadRawPacketsRecordsStats(string replayFileName, int expectedPartialErrors, int expectedMalformedPayloads)
    {
        var replayBytes = ReadReplayBytes(replayFileName);
        var context = ReadReplay(replayBytes);
        var stats = context.PacketStats;
        var payloadStats = context.BunchPayloadStats;

        Assert.Multiple(() =>
        {
            Assert.That(context.Errors, Is.Empty);
            Assert.That(context.NetGuidCache.ExportGroupsByPath, Is.Not.Empty);
            Assert.That(context.NetGuidCache.PathByNetGuid, Is.Not.Empty);
            Assert.That(stats.PacketCount, Is.GreaterThan(0));
            Assert.That(stats.TotalPacketBytes, Is.GreaterThan(0));
            Assert.That(stats.PacketsWithBunches, Is.GreaterThan(0));
            Assert.That(stats.BunchCount, Is.GreaterThan(0));
            Assert.That(stats.MalformedPacketCount, Is.EqualTo(0));
            Assert.That(stats.PartialErrorCount, Is.EqualTo(expectedPartialErrors));
            Assert.That(stats.MinTimeSeconds, Is.GreaterThanOrEqualTo(0f));
            Assert.That(stats.MaxTimeSeconds,
                Is.LessThanOrEqualTo(context.ReplayInfo.LengthInMs / 1000f + 1f));
            Assert.That(payloadStats.PayloadBunchCount, Is.GreaterThan(0));
            Assert.That(payloadStats.ContentBlockCount, Is.GreaterThan(0));
            Assert.That(payloadStats.PartialErrorCount, Is.EqualTo(expectedPartialErrors));
            Assert.That(payloadStats.MalformedPayloadCount, Is.EqualTo(expectedMalformedPayloads),
                $"Exceptions={payloadStats.MalformedPayloadExceptionCount}, MustMap={payloadStats.MalformedMustBeMappedGuidCount}, ActorOpen={payloadStats.MalformedActorOpenCount}, Content={payloadStats.MalformedContentBlockCount}, Trailing={payloadStats.TrailingPayloadCount}");
        });
    }

    private static void ReadRawPacketsDecodesBaseReplayControllerSpawnLocation(string replayFileName)
    {
        var replayBytes = ReadReplayBytes(replayFileName);
        var context = ReadReplay(replayBytes);
        var replayControllers = context.ActorChannelOpens.Where(state =>
            state.ArchetypePath?.EndsWith("Default__BaseReplayController_C", StringComparison.Ordinal) == true).ToArray();
        var replayController = replayControllers.FirstOrDefault();

        Assert.That(replayControllers, Is.Not.Empty, string.Join(", ",
            context.ActorChannelOpens.Select(state => state.ArchetypePath).Where(path => path is not null)));

        var location = replayController!.SpawnLocation;
        Assert.Multiple(() =>
        {
            Assert.That(context.Errors, Is.Empty);
            Assert.That(location, Is.Not.Null);
            Assert.That(location!.Value.X, Is.EqualTo(2382.2d));
            Assert.That(location.Value.Y, Is.EqualTo(-10417.9));
            Assert.That(location.Value.Z, Is.EqualTo(400.0d));
            Assert.That(location.Value.Bits, Is.EqualTo(18));
            Assert.That(location.Value.ScaleFactor, Is.EqualTo(10));
        });
    }

    private static ReplayReaderContext ReadReplay(byte[] replayBytes)
    {
        var archive = new FBinaryArchive(replayBytes);
        return ValorantReplayReader.CreateDefault(ValorantDescriptors.CreateCatalog()).Read(archive);
    }

    private static byte[] ReadReplayBytes(string replayFileName)
    {
        var path = Path.Combine(TestContext.CurrentContext.TestDirectory, "Replays", replayFileName);
        return File.ReadAllBytes(path);
    }

    private static byte[] ReadHeaderPayload(byte[] replayBytes)
    {
        var context = ReadReplay(replayBytes);
        Assert.That(context.Errors, Is.Empty);

        if (context.ReplayInfo.HeaderChunkIndex == ReplayInfo.NoChunkIndex)
        {
            throw new InvalidOperationException("Replay info did not contain a header chunk.");
        }

        var headerChunk = context.ReplayInfo.Chunks[context.ReplayInfo.HeaderChunkIndex];
        return replayBytes
            .AsSpan(checked((int)headerChunk.DataOffset), headerChunk.SizeInBytes)
            .ToArray();
    }

    private static object CreateReplayInfoSnapshot(
        string replayFileName,
        ReplayReaderContext context)
    {
        var info = context.ReplayInfo;
        var metadata = context.ReplayInfoSerializationMetadata;

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
                metadata.FileVersion,
                metadata.FileFriendlyName,
                FileCustomVersions = metadata.FileCustomVersions.Versions
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

    private sealed class CountingReplayDataChunkHandler : IReplayDataChunkHandler
    {
        public long TotalPayloadBytes { get; private set; }

        public void Handle(ReplayReaderContext context, ReplayDataChunkInfo dataChunk, FBinaryArchive replayDataArchive)
        {
            TotalPayloadBytes += replayDataArchive.Length;
        }
    }

    private sealed class CapturingReplayEventSink : IReplayEventSink
    {
        public List<ReplayEvent> Events { get; } = [];

        public void Emit(ReplayEvent replayEvent) => Events.Add(replayEvent);
    }

}
