using Microsoft.Extensions.Logging;
using Replay.Encoding.Archives;
using Replay.Encoding.Compression;
using Replay.Encoding.PayloadEncryption;
using Replay.Models.Descriptors;
using Replay.Models.Errors;
using Replay.Models.Events;
using Replay.Unreal.Chunks;
using Replay.Unreal.Readers;
using Replay.Valorant.Combat;
using Replay.Valorant.Descriptors;
using Replay.Valorant.Flashes;
using Replay.Valorant.Nearsights;
using Replay.Valorant.Walls;

namespace Replay.Valorant;

public sealed class ValorantReplayReader
{
    private const ushort ExpectedReplayMajorVersion = 5;
    private const ushort ExpectedReplayMinorVersion = 3;
    private const ushort ExpectedReplayPatchVersion = 2;
    private const uint ExpectedGameNetworkProtocolVersion = 0;
    private const uint ExpectedUE4Version = 522;
    private const uint ExpectedUE5Version = 1009;

    private static readonly PayloadTransformRegistry PayloadTransforms = PayloadTransformRegistry.CreateDefault();

    private readonly ReplayChunkDispatcher _chunkDispatcher;
    private readonly IReplayEventSink _eventSink;
    private readonly DescriptorCatalog? _descriptorCatalog;
    private readonly ParseProfile _parseProfile;
    private readonly ILoggerFactory? _loggerFactory;

    public ValorantReplayReader(
        IOodleDecompressor? oodleDecompressor = null,
        IReplayDataChunkHandler? replayDataChunkHandler = null,
        IReplayEventSink? eventSink = null,
        DescriptorCatalog? descriptorCatalog = null,
        ParseProfile? parseProfile = null,
        ILoggerFactory? loggerFactory = null)
    {
        _chunkDispatcher = new ReplayChunkDispatcher(oodleDecompressor, replayDataChunkHandler);
        _eventSink = eventSink ?? NullReplayEventSink.Instance;
        _descriptorCatalog = descriptorCatalog;
        _parseProfile = parseProfile ?? ParseProfile.Default;
        _loggerFactory = loggerFactory;
    }
    
    public static ValorantReplayReader CreateMinimal(
        ILoggerFactory? loggerFactory = null,
        ParseProfile? parseProfile = null)
    {
        return CreateDefault(loggerFactory, null, parseProfile);
    }

    public static ValorantReplayReader CreateDefault(
        ILoggerFactory? loggerFactory,
        IReplayEventSink? eventSink = null,
        ParseProfile? parseProfile = null)
    {
        return new ValorantReplayReader(
            new OozSharpOodleDecompressor(),
            new PlaybackPacketReplayDataChunkHandler(),
            eventSink,
            ValorantDescriptors.CreateCatalog(),
            parseProfile,
            loggerFactory);
    }

    public ReplayReaderContext Read(FBinaryArchive archive)
    {
        var preamble = _chunkDispatcher.ReadPreamble(archive);
        var metadata = CreateMetadata(preamble);
        EnsureFullParseSupported(metadata);

        var netGuidCache = new Encoding.Net.NetGuidCache();
        var wallEventEnricher = new ValorantWallEventEnricher(_eventSink);
        var nearsightEventEnricher = new ValorantNearsightEventEnricher(wallEventEnricher, netGuidCache);
        var flashEventEnricher = new ValorantFlashEventEnricher(nearsightEventEnricher, netGuidCache);
        var eventSink = new ValorantShotEventEnricher(flashEventEnricher, netGuidCache);
        var context = new ReplayReaderContext(
            archive,
            eventSink,
            _descriptorCatalog,
            _parseProfile,
            _loggerFactory,
            netGuidCache);
        context.ReplayInfo = metadata.ReplayInfo;
        context.ReplayInfoSerializationMetadata = metadata.ReplayInfoSerializationMetadata;
        context.ReplayHeader = metadata.ReplayHeader;
        context.ReplayVersion = metadata.ReplayVersion;
        context.UEVersion = metadata.UEVersion;

        _chunkDispatcher.DispatchRemaining(context);
        flashEventEnricher.Complete();
        nearsightEventEnricher.Complete();
        wallEventEnricher.Complete();

        return context;
    }

    public ValorantReplayMetadata ReadMetadata(FBinaryArchive archive) =>
        CreateMetadata(_chunkDispatcher.ReadPreamble(archive));

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
