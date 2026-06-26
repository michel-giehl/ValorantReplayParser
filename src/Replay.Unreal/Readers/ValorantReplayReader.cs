using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Replay.Encoding.Archives;
using Replay.Encoding.Compression;
using Replay.Models.Descriptors;
using Replay.Models.Errors;
using Replay.Models.Events;
using Replay.Models.Replay;
using Replay.Unreal.Chunks;
using Replay.Unreal.Info;

namespace Replay.Unreal.Readers;

public sealed class ValorantReplayReader
{
    private readonly ReplayChunkDispatcher _chunkDispatcher;
    private readonly ILogger<ValorantReplayReader> _logger;
    private readonly IReplayEventSink _eventSink;
    private readonly DescriptorCatalog? _descriptorCatalog;
    private readonly ParseProfile _parseProfile;

    public ValorantReplayReader(
        IOodleDecompressor? oodleDecompressor = null,
        IReplayDataChunkHandler? replayDataChunkHandler = null,
        ILogger<ValorantReplayReader>? logger = null,
        ILogger<ReplayChunkDispatcher>? chunkDispatcherLogger = null,
        IReplayEventSink? eventSink = null,
        DescriptorCatalog? descriptorCatalog = null,
        ParseProfile? parseProfile = null)
    {
        _chunkDispatcher = new ReplayChunkDispatcher(oodleDecompressor, replayDataChunkHandler, chunkDispatcherLogger);
        _logger = logger ?? NullLogger<ValorantReplayReader>.Instance;
        _eventSink = eventSink ?? NullReplayEventSink.Instance;
        _descriptorCatalog = descriptorCatalog;
        _parseProfile = parseProfile ?? ParseProfile.Default;
    }

    public static ValorantReplayReader CreateDefault(
        DescriptorCatalog? descriptorCatalog = null,
        ParseProfile? parseProfile = null) =>
        new(new OozSharpOodleDecompressor(), descriptorCatalog: descriptorCatalog, parseProfile: parseProfile);

    public static ValorantReplayReader CreateDefault(
        ILoggerFactory loggerFactory,
        IReplayEventSink? eventSink = null,
        DescriptorCatalog? descriptorCatalog = null,
        ParseProfile? parseProfile = null)
    {
        return new ValorantReplayReader(
            new OozSharpOodleDecompressor(),
            new PlaybackPacketReplayDataChunkHandler(
                loggerFactory.CreateLogger<PlaybackPacketReplayDataChunkHandler>(),
                loggerFactory),
            loggerFactory.CreateLogger<ValorantReplayReader>(),
            loggerFactory.CreateLogger<ReplayChunkDispatcher>(),
            eventSink,
            descriptorCatalog,
            parseProfile);
    }

    public ReplayReaderContext Read(FBinaryArchive archive)
    {
        var context = new ReplayReaderContext(archive, _eventSink, _descriptorCatalog, _parseProfile);
        try
        {
            var info = new ReplayInfo();
            var metadata = new ReplayInfoSerializationMetadata();
            var replayInfoResult = new ReplayInfoReader(archive).Read(info, metadata);

            context.ReplayInfo = replayInfoResult.Info;
            context.ReplayInfoSerializationMetadata = replayInfoResult.SerializationMetadata;

            _chunkDispatcher.DispatchAll(context);
            context.ReplayInfo.IsValid = context.ReplayInfo.HeaderChunkIndex != ReplayInfo.NoChunkIndex;
            if (!context.ReplayInfo.IsValid)
            {
                throw new InvalidReplayInfoException("Replay info does not contain a valid header chunk.");
            }
        }
        catch (Exception exception) when (IsReplayParseException(exception))
        {
            context.Errors.Add(ToParseError(exception));
        }

        return context;
    }

    private static bool IsReplayParseException(Exception exception) =>
        exception is InvalidReplayInfoException
            or InvalidReplayHeaderException
            or ArchiveReadException
            or OodleDecompressionException
            or OverflowException;

    private static ReplayParseError ToParseError(Exception exception) => exception switch
    {
        InvalidReplayHeaderException => new InvalidReplayHeaderError("Invalid replay header.", exception),
        OodleDecompressionException => new InvalidReplayDataError("Invalid replay data.", exception),
        _ => new InvalidReplayInfoError("Invalid replay info.", exception),
    };
}
