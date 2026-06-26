using Microsoft.Extensions.Logging;
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
            new PlaybackPacketReplayDataChunkHandler(),
            eventSink,
            descriptorCatalog,
            parseProfile,
            loggerFactory);
    }

    public ReplayReaderContext Read(FBinaryArchive archive)
    {
        var context = new ReplayReaderContext(archive, _eventSink, _descriptorCatalog, _parseProfile, _loggerFactory);
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

        return context;
    }
}
