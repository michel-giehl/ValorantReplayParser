using Replay.Encoding.Archives;
using Replay.Models.Net;
using Replay.Unreal.Bunches.Payload.Stages;
using Replay.Unreal.PackageMap;
using Replay.Unreal.Readers;

namespace Replay.Unreal.Bunches.Payload;

public sealed class BunchPayloadPipeline
{
    private readonly ReplayReaderContext _context;
    private readonly IBunchPayloadProcessor _processor;

    public BunchPayloadPipeline(ReplayReaderContext context)
    {
        _context = context;
        _processor = CreateProcessor(context);
    }

    public void HandleBunchPayload(ref RawBunchHeader header, FBitArchive payload)
    {
        using var context = new BunchPayloadContext(_context, header, payload);
        _processor.Process(context);
        header = context.Header;
    }

    internal void Reset()
    {
        _processor.Reset();
        _context.WorldState.Reset();
    }

    private static BunchPayloadProcessor CreateProcessor(ReplayReaderContext context)
    {
        var packageMapReader = new PackageMapReader(context.NetGuidCache);
        var partialBunchAccumulator = new PartialBunchAccumulator();
        var contentBlockFramer = new ContentBlockFramer(
            packageMapReader,
            context.NetGuidCache,
            context.WorldState,
            context.EventSink,
            context.ExportBindingRegistry);
        var newActorSerializer = new NewActorSerializer(packageMapReader, context.NetGuidCache);
        var lifecycleService = new ActorChannelLifecycleService(context);

        return new BunchPayloadProcessor([
            new BunchStatsStage(),
            new PackageMapExportBunchStage(packageMapReader),
            new PartialBunchStage(partialBunchAccumulator),
            new MustBeMappedGuidsBunchStage(),
            new ActorChannelOpenBunchStage(newActorSerializer, lifecycleService),
            new ActorChannelLookupBunchStage(),
            new DynamicActorOpenPayloadBunchStage(),
            new ContentBlocksBunchStage(contentBlockFramer),
            new ActorChannelCloseBunchStage(lifecycleService),
            new TrailingPayloadBunchStage(),
        ]);
    }
}
