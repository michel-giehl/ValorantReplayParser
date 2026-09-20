using Microsoft.Extensions.Logging;
using Replay.Encoding.Archives;
using Replay.Encoding.Compression;
using Replay.Encoding.Net;
using Replay.Encoding.PayloadEncryption;
using Replay.Models.Descriptors;
using Replay.Models.Diagnostics;
using Replay.Models.Errors;
using Replay.Models.Events;
using Replay.Models.Net;
using Replay.Models.Replay;
using Replay.Models.Results;
using Replay.Unreal.Chunks;
using Replay.Unreal.Readers;
using System.Threading;
using Replay.Valorant.Combat;
using Replay.Valorant.Descriptors;
using Replay.Valorant.Flashes;
using Replay.Valorant.Nearsights;
using Replay.Valorant.Walls;

namespace Replay.Valorant;

/// <summary>
/// Reads VALORANT replay containers and emits the gameplay events understood by this parser version.
/// </summary>
/// <remarks>
/// This is a synchronous, VALORANT-specific reader. Instances support sequential reuse, but not concurrent
/// or reentrant reads. Events are delivered as parsing proceeds and cannot be rolled back if a later parse
/// error occurs. Custom catalog or descriptor mutation during a read is unsupported.
/// </remarks>
public sealed class ValorantReplayReader
{
    private const ushort ExpectedReplayMajorVersion = 5;
    private const ushort ExpectedReplayMinorVersion = 3;
    private const ushort ExpectedReplayPatchVersion = 2;
    private const uint ExpectedGameNetworkProtocolVersion = 0;
    private const uint ExpectedUE4Version = 522;
    private const uint ExpectedUE5Version = 1009;

    private static readonly PayloadTransformRegistry PayloadTransforms = PayloadTransformRegistry.CreateDefault();

    private readonly IOodleDecompressor? _injectedDecompressor;
    private readonly IReplayDataChunkHandler? _injectedChunkHandler;
    private readonly IReplayEventSink _eventSink;
    private readonly DescriptorCatalog? _descriptorCatalog;
    private readonly ParseProfile _parseProfile;
    private readonly ILoggerFactory? _loggerFactory;
    private int _activeRead;

    /// <summary>
    /// Creates a reader using the built-in VALORANT catalog unless a complete replacement catalog is supplied.
    /// </summary>
    /// <param name="loggerFactory">Optional logger factory. Logging is not required to inspect parse diagnostics.</param>
    /// <param name="eventSink">Optional synchronous event sink. When omitted, emitted events are discarded.</param>
    /// <param name="parseProfile">Optional field-selection profile. When omitted, <see cref="ParseProfile.Default"/> is used.</param>
    /// <param name="descriptorCatalog">
    /// Optional complete descriptor catalog replacement. To extend the built-in catalog, start with
    /// <see cref="ValorantDescriptors.CreateCatalog()"/> and add descriptors to it.
    /// </param>
    /// <remarks>
    /// Mutating the supplied parse profile or descriptor catalog while a read is in progress is unsupported.
    /// Parse profile selection sets are snapshotted at the beginning of each read.
    /// </remarks>
    public ValorantReplayReader(
        ILoggerFactory? loggerFactory = null,
        IReplayEventSink? eventSink = null,
        ParseProfile? parseProfile = null,
        DescriptorCatalog? descriptorCatalog = null)
    {
        _injectedDecompressor = null;
        _injectedChunkHandler = null;
        _eventSink = eventSink ?? NullReplayEventSink.Instance;
        _descriptorCatalog = descriptorCatalog ?? ValorantDescriptors.CreateCatalog();
        _parseProfile = parseProfile ?? ParseProfile.Default;
        _loggerFactory = loggerFactory;
    }

    internal ValorantReplayReader(
        IOodleDecompressor decompressor,
        IReplayDataChunkHandler? chunkHandler = null,
        IReplayEventSink? sink = null,
        DescriptorCatalog? catalog = null,
        ParseProfile? profile = null,
        ILoggerFactory? loggerFactory = null)
    {
        _injectedDecompressor = decompressor;
        _injectedChunkHandler = chunkHandler;
        _eventSink = sink ?? NullReplayEventSink.Instance;
        _descriptorCatalog = catalog ?? ValorantDescriptors.CreateCatalog();
        _parseProfile = profile ?? ParseProfile.Default;
        _loggerFactory = loggerFactory;
    }

    /// <summary>
    /// Creates a reader with the same defaults as <see cref="ValorantReplayReader(ILoggerFactory, IReplayEventSink, ParseProfile, DescriptorCatalog)"/>.
    /// </summary>
    /// <param name="loggerFactory">Optional logger factory.</param>
    /// <param name="eventSink">Optional synchronous event sink.</param>
    /// <param name="parseProfile">Optional parse profile; defaults to <see cref="ParseProfile.Default"/>.</param>
    /// <returns>A new reader configured with the default VALORANT descriptors and replay-data handler.</returns>
    public static ValorantReplayReader CreateDefault(
        ILoggerFactory? loggerFactory = null,
        IReplayEventSink? eventSink = null,
        ParseProfile? parseProfile = null)
    {
        return new ValorantReplayReader(loggerFactory, eventSink, parseProfile);
    }

    /// <summary>
    /// Parses the replay starting at the archive's current position and returns detached metadata and statistics.
    /// </summary>
    /// <param name="archive">Caller-owned archive positioned at the start of a replay.</param>
    /// <returns>A detached snapshot containing metadata, counters, warnings, and export-group summaries.</returns>
    /// <remarks>
    /// The archive remains open and is left at the position consumed by parsing. The reader does not rewind or
    /// dispose it. Events may already have been delivered if this operation later throws. A result indicates
    /// successful structural traversal under the reader's policy; it does not promise complete semantic coverage.
    /// </remarks>
    /// <exception cref="ArgumentNullException"><paramref name="archive"/> is null.</exception>
    /// <exception cref="InvalidOperationException">This reader is already performing a read.</exception>
    /// <exception cref="ReplayParseException">The replay has unsupported metadata or malformed required data.</exception>
    public ValorantReplayReadResult Read(FBinaryArchive archive)
    {
        ArgumentNullException.ThrowIfNull(archive);
        EnterRead();
        try
        {
            return ReadAndRethrowConsumerException(archive);
        }
        finally
        {
            Volatile.Write(ref _activeRead, 0);
        }
    }

    /// <summary>
    /// Parses the replay bytes remaining in a stream and returns detached metadata and statistics.
    /// </summary>
    /// <param name="stream">Caller-owned readable stream positioned at the beginning of the replay.</param>
    /// <returns>A detached snapshot containing metadata, counters, warnings, and export-group summaries.</returns>
    /// <remarks>
    /// The current stream implementation buffers all remaining bytes before parsing. The stream is left at EOF
    /// and remains open; the reader owns and disposes only the archive it creates. Reopen or explicitly rewind
    /// the stream before another read. This reader instance supports sequential reuse only.
    /// </remarks>
    /// <exception cref="ArgumentNullException"><paramref name="stream"/> is null.</exception>
    /// <exception cref="InvalidOperationException">This reader is already performing a read.</exception>
    /// <exception cref="ReplayParseException">The replay has unsupported metadata or malformed required data.</exception>
    public ValorantReplayReadResult Read(Stream stream)
    {
        ArgumentNullException.ThrowIfNull(stream);
        EnterRead();
        try
        {
            using var archive = new FBinaryArchive(stream);
            return ReadAndRethrowConsumerException(archive);
        }
        finally
        {
            Volatile.Write(ref _activeRead, 0);
        }
    }

    /// <summary>
    /// Reads replay metadata and the header from the archive without dispatching replay-data payloads.
    /// </summary>
    /// <param name="archive">Caller-owned archive positioned at the start of a replay.</param>
    /// <returns>A detached metadata snapshot, including whether the current full parser supports its version.</returns>
    /// <remarks>
    /// The archive remains open and is left immediately after the metadata/header preamble. This overload can
    /// report an unsupported full-parse version in the returned metadata. It still throws for malformed metadata.
    /// Reposition the archive before a later full read.
    /// </remarks>
    /// <exception cref="ArgumentNullException"><paramref name="archive"/> is null.</exception>
    /// <exception cref="InvalidOperationException">This reader is already performing a read.</exception>
    /// <exception cref="ReplayParseException">Replay metadata or the header is malformed.</exception>
    public ValorantReplayMetadata ReadMetadata(FBinaryArchive archive)
    {
        ArgumentNullException.ThrowIfNull(archive);
        EnterRead();
        try
        {
            return ReadMetadataCore(archive);
        }
        finally
        {
            Volatile.Write(ref _activeRead, 0);
        }
    }

    /// <summary>
    /// Reads replay metadata and the header from the remaining stream bytes without dispatching replay-data payloads.
    /// </summary>
    /// <param name="stream">Caller-owned readable stream positioned at the beginning of the replay.</param>
    /// <returns>A detached metadata snapshot, including whether the current full parser supports its version.</returns>
    /// <remarks>
    /// The current stream implementation buffers all remaining bytes, so the supplied stream advances to EOF even
    /// though replay-data payloads are not parsed. The stream remains open; the reader disposes only its archive.
    /// Reopen or explicitly rewind the stream for a full read or another metadata read.
    /// </remarks>
    /// <exception cref="ArgumentNullException"><paramref name="stream"/> is null.</exception>
    /// <exception cref="InvalidOperationException">This reader is already performing a read.</exception>
    /// <exception cref="ReplayParseException">Replay metadata or the header is malformed.</exception>
    public ValorantReplayMetadata ReadMetadata(Stream stream)
    {
        ArgumentNullException.ThrowIfNull(stream);
        EnterRead();
        try
        {
            using var archive = new FBinaryArchive(stream);
            return ReadMetadataCore(archive);
        }
        finally
        {
            Volatile.Write(ref _activeRead, 0);
        }
    }

    private ValorantReplayReadResult ReadAndRethrowConsumerException(FBinaryArchive archive)
    {
        try
        {
            return ReadCore(archive);
        }
        catch (ConsumerEventSinkException exception)
        {
            exception.RethrowOriginal();
            throw;
        }
    }

    private static ValorantReplayMetadata ReadMetadataCore(FBinaryArchive archive) =>
        CloneMetadata(CreateMetadata(new ReplayChunkDispatcher().ReadPreamble(archive)));

    private void EnterRead()
    {
        if (Interlocked.CompareExchange(ref _activeRead, 1, 0) != 0)
        {
            throw new InvalidOperationException("ValorantReplayReader does not support concurrent or reentrant reads.");
        }
    }

    private ValorantReplayReadResult ReadCore(FBinaryArchive archive)
    {
        var dispatcher = new ReplayChunkDispatcher(
            _injectedDecompressor ?? new OozSharpOodleDecompressor(),
            _injectedChunkHandler ?? new PlaybackPacketReplayDataChunkHandler());
        var preamble = dispatcher.ReadPreamble(archive);
        var metadata = CreateMetadata(preamble);
        EnsureFullParseSupported(metadata);
        var replayReleaseVersion = ValorantReleaseVersionParser.ParseRequired(metadata.ReplayVersion.Branch);

        var netGuidCache = new NetGuidCache();
        var consumerSink = new ConsumerEventSink(_eventSink);
        var wallEventEnricher = new ValorantWallEventEnricher(consumerSink);
        var nearsightEventEnricher = new ValorantNearsightEventEnricher(wallEventEnricher, netGuidCache);
        var flashEventEnricher = new ValorantFlashEventEnricher(nearsightEventEnricher, netGuidCache);
        var eventSink = new ValorantShotEventEnricher(flashEventEnricher, netGuidCache);
        var context = new ReplayReaderContext(
            archive,
            eventSink,
            _descriptorCatalog,
            SnapshotParseProfile(_parseProfile),
            _loggerFactory,
            netGuidCache,
            replayReleaseVersion: replayReleaseVersion);
        context.ReplayInfo = metadata.ReplayInfo;
        context.ReplayInfoSerializationMetadata = metadata.ReplayInfoSerializationMetadata;
        context.ReplayHeader = metadata.ReplayHeader;
        context.ReplayVersion = metadata.ReplayVersion;
        context.UEVersion = metadata.UEVersion;

        try
        {
            dispatcher.DispatchRemaining(context);
            context.BunchPayloadPipeline.FinalizePending();
            flashEventEnricher.Complete();
            nearsightEventEnricher.Complete();
            wallEventEnricher.Complete();
            return CreateReadResult(context);
        }
        finally
        {
            context.Dispose();
        }
    }

    internal static ParseProfile SnapshotParseProfile(ParseProfile profile) => new()
    {
        EnabledCategories = profile.EnabledCategories,
        IncludedPaths = SnapshotSet(profile.IncludedPaths),
        ExcludedPaths = SnapshotSet(profile.ExcludedPaths),
        IncludedFields = SnapshotSet(profile.IncludedFields),
        CaptureDiagnosticFields = profile.CaptureDiagnosticFields,
    };

    private static HashSet<string>? SnapshotSet(HashSet<string>? values) =>
        values is null ? null : new HashSet<string>(values, values.Comparer);

    private static ValorantReplayReadResult CreateReadResult(ReplayReaderContext context)
    {
        var groups = context.NetGuidCache.ExportGroupsByPath.Values
            .OrderBy(group => group.PathName, StringComparer.Ordinal)
            .Select(group => new ReplayExportGroupSummary(
                group.PathName,
                group.PathNameIndex,
                Array.AsReadOnly(group.NetFieldExports
                    .Where(field => field is not null)
                    .Select(field => new ReplayExportFieldSummary(field!.Handle, field.Name, field.CompatibleChecksum))
                    .OrderBy(field => field.Handle)
                    .ToArray())))
            .ToArray();

        return new ValorantReplayReadResult(
            CloneMetadata(new ValorantReplayMetadata(
                context.ReplayInfo,
                context.ReplayInfoSerializationMetadata,
                context.ReplayHeader,
                context.ReplayVersion,
                context.UEVersion,
                ValorantReplaySupportStatus.Supported,
                null)),
            Snapshot(context.PacketStats),
            Snapshot(context.BunchPayloadStats),
            context.ReadStatus,
            Array.AsReadOnly(context.ReadDiagnostics.ToArray()),
            context.TotalDiagnosticCount,
            context.SuppressedDiagnosticCount,
            Array.AsReadOnly(groups));
    }

    private static ReplayPacketStatistics Snapshot(Replay.Unreal.Packets.RawPacketStats stats) => new(
        stats.PacketCount,
        stats.TotalPacketBytes,
        stats.PacketsWithBunches,
        stats.BunchCount,
        stats.MalformedPacketCount,
        stats.PartialErrorCount,
        stats.MinTimeSeconds,
        stats.MaxTimeSeconds);

    private static ReplayBunchStatistics Snapshot(Replay.Unreal.Bunches.BunchPayloadStats stats) => new(
        stats.PacketCount,
        stats.BunchCount,
        stats.PayloadBunchCount,
        stats.PackageMapExportBunchCount,
        stats.ExportedNetGuidCount,
        stats.MustBeMappedGuidCount,
        stats.PartialFragmentCount,
        stats.CompletedPartialBunchCount,
        stats.PartialErrorCount,
        stats.ActorChannelOpenCount,
        stats.ActorChannelCloseCount,
        stats.ActorSerializeNewActorCount,
        stats.DynamicOpenPayloadBunchCount,
        stats.DynamicOpenPayloadBitsSkipped,
        stats.ContentBlockCount,
        stats.ActorContentBlockCount,
        stats.SubobjectContentBlockCount,
        stats.DeletedContentBlockCount,
        stats.RepLayoutContentBlockCount,
        stats.ContentPayloadBitsSkipped,
        stats.ContentPayloadBitsParsed,
        stats.MalformedPayloadCount,
        stats.MalformedPayloadExceptionCount,
        stats.MalformedMustBeMappedGuidCount,
        stats.MalformedActorOpenCount,
        stats.MalformedContentBlockCount,
        stats.TrailingPayloadCount);

    private static ValorantReplayMetadata CloneMetadata(ValorantReplayMetadata metadata)
    {
        var sourceInfo = metadata.ReplayInfo;
        var info = new ReplayInfo
        {
            LengthInMs = sourceInfo.LengthInMs,
            NetworkVersion = sourceInfo.NetworkVersion,
            Changelist = sourceInfo.Changelist,
            FriendlyName = sourceInfo.FriendlyName,
            Timestamp = sourceInfo.Timestamp,
            TotalDataSizeInBytes = sourceInfo.TotalDataSizeInBytes,
            IsLive = sourceInfo.IsLive,
            IsValid = sourceInfo.IsValid,
            Compressed = sourceInfo.Compressed,
            Encrypted = sourceInfo.Encrypted,
            EncryptionKey = sourceInfo.EncryptionKey.ToArray(),
            HeaderChunkIndex = sourceInfo.HeaderChunkIndex,
        };
        info.Chunks.AddRange(sourceInfo.Chunks.Select(chunk => new ReplayChunkInfo
        {
            ChunkType = chunk.ChunkType,
            SizeInBytes = chunk.SizeInBytes,
            TypeOffset = chunk.TypeOffset,
            DataOffset = chunk.DataOffset,
        }));
        info.DataChunks.AddRange(sourceInfo.DataChunks.Select(chunk => new ReplayDataChunkInfo
        {
            ChunkIndex = chunk.ChunkIndex,
            Time1 = chunk.Time1,
            Time2 = chunk.Time2,
            SizeInBytes = chunk.SizeInBytes,
            MemorySizeInBytes = chunk.MemorySizeInBytes,
            ReplayDataOffset = chunk.ReplayDataOffset,
            StreamOffset = chunk.StreamOffset,
        }));

        var serialization = new ReplayInfoSerializationMetadata
        {
            FileVersion = metadata.ReplayInfoSerializationMetadata.FileVersion,
            FileFriendlyName = metadata.ReplayInfoSerializationMetadata.FileFriendlyName,
            FileCustomVersions = metadata.ReplayInfoSerializationMetadata.FileCustomVersions.Clone(),
        };
        var header = metadata.ReplayHeader;
        return metadata with
        {
            ReplayInfo = info,
            ReplayInfoSerializationMetadata = serialization,
            ReplayHeader = new ReplayHeader
            {
                NetworkVersion = header.NetworkVersion,
                NetworkChecksum = header.NetworkChecksum,
                EngineNetworkProtocolVersion = header.EngineNetworkProtocolVersion,
                GameNetworkProtocolVersion = header.GameNetworkProtocolVersion,
                Guid = header.Guid,
                MinRecordHz = header.MinRecordHz,
                MaxRecordHz = header.MaxRecordHz,
                FrameLimitInMs = header.FrameLimitInMs,
                CheckpointLimitInMs = header.CheckpointLimitInMs,
                LevelNamesAndTimes = header.LevelNamesAndTimes.ToArray(),
                Flags = header.Flags,
                GameSpecificData = header.GameSpecificData.ToArray(),
                Platform = header.Platform,
                BuildConfig = header.BuildConfig,
                BuildTargetType = header.BuildTargetType,
            },
            ReplayVersion = new ReplayVersion
            {
                Major = metadata.ReplayVersion.Major,
                Minor = metadata.ReplayVersion.Minor,
                Patch = metadata.ReplayVersion.Patch,
                Changelist = metadata.ReplayVersion.Changelist,
                Branch = metadata.ReplayVersion.Branch,
            },
            UEVersion = new UEVersion
            {
                UE4Version = metadata.UEVersion.UE4Version,
                UE5Version = metadata.UEVersion.UE5Version,
                PackageVersionLicense = metadata.UEVersion.PackageVersionLicense,
            },
        };
    }

    private static ValorantReplayMetadata CreateMetadata(ReplayPreambleReadResult preamble)
    {
        var unsupportedReason = GetUnsupportedReason(preamble);
        return new ValorantReplayMetadata(
            preamble.ReplayInfo,
            preamble.ReplayInfoSerializationMetadata,
            preamble.ReplayHeader,
            preamble.ReplayVersion,
            preamble.UEVersion,
            unsupportedReason is null
                ? ValorantReplaySupportStatus.Supported
                : ValorantReplaySupportStatus.UnsupportedVersion,
            unsupportedReason);
    }

    private static void EnsureFullParseSupported(ValorantReplayMetadata metadata)
    {
        if (metadata.FullParseSupportStatus == ValorantReplaySupportStatus.Supported)
        {
            return;
        }

        throw new InvalidReplayInfoException(
            $"Unsupported VALORANT replay version: {metadata.FullParseUnsupportedReason}");
    }

    private static string? GetUnsupportedReason(ReplayPreambleReadResult preamble)
    {
        var replayVersion = preamble.ReplayVersion;
        if (replayVersion is not
            {
                Major: ExpectedReplayMajorVersion,
                Minor: ExpectedReplayMinorVersion,
                Patch: ExpectedReplayPatchVersion,
            })
        {
            return $"expected replay version {ExpectedReplayMajorVersion}.{ExpectedReplayMinorVersion}.{ExpectedReplayPatchVersion}, got {replayVersion.Major}.{replayVersion.Minor}.{replayVersion.Patch} for branch '{replayVersion.Branch}'.";
        }

        if (preamble.ReplayHeader.GameNetworkProtocolVersion != ExpectedGameNetworkProtocolVersion)
        {
            return $"expected game network protocol version {ExpectedGameNetworkProtocolVersion}, got {preamble.ReplayHeader.GameNetworkProtocolVersion} for branch '{replayVersion.Branch}'.";
        }

        if (preamble.UEVersion.UE4Version != ExpectedUE4Version || preamble.UEVersion.UE5Version != ExpectedUE5Version)
        {
            return $"expected UE versions {ExpectedUE4Version}/{ExpectedUE5Version}, got {preamble.UEVersion.UE4Version}/{preamble.UEVersion.UE5Version} for branch '{replayVersion.Branch}'.";
        }

        try
        {
            _ = PayloadTransforms.GetRequired(replayVersion.Branch);
        }
        catch (UnsupportedPayloadTransformVersionException)
        {
            return $"no payload transform is registered for replay branch '{replayVersion.Branch}'.";
        }

        return null;
    }
}
