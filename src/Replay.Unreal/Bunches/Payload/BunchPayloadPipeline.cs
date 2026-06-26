using Replay.Encoding.Archives;
using Replay.Encoding.PayloadEncryption;
using Replay.Models.Net;
using Replay.Unreal.Bunches.Payload.Stages;
using Replay.Unreal.PackageMap;
using Replay.Unreal.Readers;

namespace Replay.Unreal.Bunches.Payload;

public sealed class BunchPayloadPipeline
{
    private readonly ReplayReaderContext _context;
    private readonly BunchPayloadProcessor _processor;

    public BunchPayloadPipeline(ReplayReaderContext context)
    {
        _context = context;
        _processor = CreateProcessor(context);
    }

    public void HandleBunchPayload(ref RawBunchHeader header, FBitArchive payload)
    {
        var context = new BunchPayloadContext(_context, header, payload);
        try
        {
            _processor.Process(ref context);
            header = context.Header;
        }
        finally
        {
            context.Dispose();
        }
    }

    private BunchPayloadProcessor CreateProcessor(ReplayReaderContext context)
    {
        var packageMapReader = new PackageMapReader(context.NetGuidCache);
        var partialBunchAccumulator = new PartialBunchAccumulator();
        var propertyPayloadDecoder = new PropertyPayloadDecoder(PayloadTransformRegistry.CreateDefault());

        var contentBlockFramer = new ContentBlockFramer(
            packageMapReader,
            context,
            propertyPayloadDecoder);
        var newActorSerializer = new NewActorSerializer(packageMapReader, context.NetGuidCache);
        var lifecycleService = new ActorChannelLifecycleService(context);

        return new BunchPayloadProcessor([
            new BunchStatsStage(),
            new PackageMapExportBunchStage(packageMapReader),
            new PartialBunchStage(partialBunchAccumulator),
            new MustBeMappedGuidsBunchStage(),
            new ActorChannelOpenBunchStage(newActorSerializer, lifecycleService),
            new ActorChannelLookupBunchStage(),
            new ReadNetPlayerIndexStage(),
            new ContentBlocksBunchStage(contentBlockFramer),
            new ActorChannelCloseBunchStage(lifecycleService),
            new TrailingPayloadBunchStage(),
        ]);
    }
}
