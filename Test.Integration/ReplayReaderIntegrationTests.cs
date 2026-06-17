using Replay.Encoding.Archives;
using Replay.Models;
using Replay.Unreal;
using Snapshooter.NUnit;

namespace Test.Integration;

[Category("Integration")]
public class ReplayReaderIntegrationTests
{
    [Test]
    public void ReadReplayInfo_12_08_MatchesSnapshot() =>
        ReadReplayInfoMatchesSnapshot("c96127a8-f003-48db-a2cd-9c71de5aba15.12_08.vrf");

    [Test]
    public void ReadReplayInfo_12_10_MatchesSnapshot() =>
        ReadReplayInfoMatchesSnapshot("9f8b32c5-c243-41ec-bbbb-832582edf652.12_10.vrf");

    [Test]
    public void ReadReplayInfo_12_11_MatchesSnapshot() =>
        ReadReplayInfoMatchesSnapshot("5c673443-5bdc-4576-b416-aab3f62471a5.12_11.vrf");

    [Test]
    public void ReadReplayHeader_12_08_MatchesSnapshot() =>
        ReadReplayHeaderMatchesSnapshot("c96127a8-f003-48db-a2cd-9c71de5aba15.12_08.vrf");

    [Test]
    public void ReadReplayHeader_12_10_MatchesSnapshot() =>
        ReadReplayHeaderMatchesSnapshot("9f8b32c5-c243-41ec-bbbb-832582edf652.12_10.vrf");

    [Test]
    public void ReadReplayHeader_12_11_MatchesSnapshot() =>
        ReadReplayHeaderMatchesSnapshot("5c673443-5bdc-4576-b416-aab3f62471a5.12_11.vrf");

    private static void ReadReplayInfoMatchesSnapshot(string replayFileName)
    {
        var replayBytes = ReadReplayBytes(replayFileName);
        var archive = new FBinaryArchive(replayBytes);

        var readResult = new ReplayInfoReader(archive).Read(new ReplayInfo(), new ReplayInfoSerializationMetadata());
        var scanResult = new ReplayInfoChunkScanner(archive).Scan(readResult.Info);

        Snapshot.Match(CreateReplayInfoSnapshot(replayFileName, readResult, scanResult));
    }

    private static void ReadReplayHeaderMatchesSnapshot(string replayFileName)
    {
        var replayBytes = ReadReplayBytes(replayFileName);
        var headerBytes = ReadHeaderPayload(replayBytes);
        var headerArchive = new FBinaryArchive(headerBytes);

        var readResult = new ReplayHeaderReader(headerArchive).Read();

        Snapshot.Match(CreateReplayHeaderSnapshot(replayFileName, readResult, headerArchive, headerBytes.Length));
    }

    private static byte[] ReadReplayBytes(string replayFileName)
    {
        var path = Path.Combine(TestContext.CurrentContext.TestDirectory, "Replays", replayFileName);
        return File.ReadAllBytes(path);
    }

    private static byte[] ReadHeaderPayload(byte[] replayBytes)
    {
        var replayArchive = new FBinaryArchive(replayBytes);
        var readResult = new ReplayInfoReader(replayArchive).Read(new ReplayInfo(), new ReplayInfoSerializationMetadata());
        var scanResult = new ReplayInfoChunkScanner(replayArchive).Scan(readResult.Info);

        if (scanResult.HeaderChunkPayloadOffset is not { } headerPayloadOffset)
        {
            throw new InvalidOperationException("Replay info did not contain a header chunk.");
        }

        var headerChunk = readResult.Info.Chunks[readResult.Info.HeaderChunkIndex];
        return replayBytes
            .AsSpan(checked((int)headerPayloadOffset), headerChunk.SizeInBytes)
            .ToArray();
    }

    private static object CreateReplayInfoSnapshot(
        string replayFileName,
        ReplayInfoReadResult readResult,
        ReplayInfoChunkScanResult scanResult)
    {
        var info = readResult.Info;
        var metadata = readResult.SerializationMetadata;

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
                scanResult.HeaderChunkPayloadOffset,
                ChunkCount = info.Chunks.Count,
                CheckpointCount = info.Checkpoints.Count,
                EventCount = info.Events.Count,
                DataChunkCount = info.DataChunks.Count,
                HeaderChunk = ToChunkSnapshot(info.Chunks[info.HeaderChunkIndex]),
                FirstDataChunk = ToDataChunkSnapshot(info.DataChunks.FirstOrDefault()),
                LastDataChunk = ToDataChunkSnapshot(info.DataChunks.LastOrDefault()),
                FirstCheckpoint = ToEventSnapshot(info.Checkpoints.FirstOrDefault()),
                LastCheckpoint = ToEventSnapshot(info.Checkpoints.LastOrDefault()),
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

    private static object? ToEventSnapshot(ReplayEventInfo? replayEvent)
    {
        if (replayEvent is null)
        {
            return null;
        }

        return new
        {
            replayEvent.ChunkIndex,
            replayEvent.Id,
            replayEvent.Group,
            replayEvent.Metadata,
            replayEvent.Time1,
            replayEvent.Time2,
            replayEvent.SizeInBytes,
            replayEvent.EventDataOffset,
        };
    }
}
