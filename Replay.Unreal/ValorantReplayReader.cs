using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Replay.Encoding.Archives;
using Replay.Encoding.Compression;
using Replay.Models;

namespace Replay.Unreal;

public sealed class ValorantReplayReader
{
    private readonly ReplayChunkDispatcher _chunkDispatcher;
    private readonly ILogger<ValorantReplayReader> _logger;

    public ValorantReplayReader(
        IOodleDecompressor? oodleDecompressor = null,
        IReplayDataChunkHandler? replayDataChunkHandler = null,
        ILogger<ValorantReplayReader>? logger = null,
        ILogger<ReplayChunkDispatcher>? chunkDispatcherLogger = null)
    {
        _chunkDispatcher = new ReplayChunkDispatcher(oodleDecompressor, replayDataChunkHandler, chunkDispatcherLogger);
        _logger = logger ?? NullLogger<ValorantReplayReader>.Instance;
    }

    public static ValorantReplayReader CreateDefault() => new(new OozSharpOodleDecompressor());

    public static ValorantReplayReader CreateDefault(ILoggerFactory loggerFactory)
    {
        ArgumentNullException.ThrowIfNull(loggerFactory);

        return new ValorantReplayReader(
            new OozSharpOodleDecompressor(),
            new PlaybackPacketReplayDataChunkHandler(
                loggerFactory.CreateLogger<PlaybackPacketReplayDataChunkHandler>(),
                loggerFactory),
            loggerFactory.CreateLogger<ValorantReplayReader>(),
            loggerFactory.CreateLogger<ReplayChunkDispatcher>());
    }

    public ReplayReaderContext Read(FBinaryArchive archive)
    {
        ArgumentNullException.ThrowIfNull(archive);

        var context = new ReplayReaderContext(archive);
        try
        {
            _logger.LogDebug("Reading VALORANT replay info.");
            var info = new ReplayInfo();
            var metadata = new ReplayInfoSerializationMetadata();
            var replayInfoResult = new ReplayInfoReader(archive).Read(info, metadata);

            context.ReplayInfo = replayInfoResult.Info;
            context.ReplayInfoSerializationMetadata = replayInfoResult.SerializationMetadata;

            _logger.LogDebug("Dispatching VALORANT replay chunks.");
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
