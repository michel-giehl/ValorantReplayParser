using Replay.Encoding.Archives;
using Replay.Models.Diagnostics;
using Replay.Models.Errors;
using Replay.Models.Net;
using Replay.Unreal.Bunches.Payload.Stages;
using Replay.Unreal.PackageMap;
using Replay.Unreal.Readers;

namespace Replay.Unreal.Bunches.Payload;

internal sealed class BunchPayloadPipeline : IDisposable
{
    private readonly ReplayReaderContext _context;
    private readonly BunchPayloadProcessor _processor;
    private readonly IPartialBunchAccumulator _partialBunchAccumulator;
    private bool _isDisposed;

    public BunchPayloadPipeline(ReplayReaderContext context)
    {
        _context = context;
        (_processor, _partialBunchAccumulator) = CreateProcessor(context);
    }

    public void HandleBunchPayload(ref RawBunchHeader header, FBitArchive payload)
    {
        ObjectDisposedException.ThrowIf(_isDisposed, this);
        var context = new BunchPayloadContext(_context, header, payload);
        try
        {
            _processor.Process(ref context);
            header = context.Header;
        }
        catch (ArchiveReadException exception)
        {
            throw new InvalidReplayDataException(
                $"Malformed bunch payload in packet {context.Header.PacketId} on channel {context.Header.ChIndex} " +
                $"at payload position {context.Payload.Position} of {context.Payload.Length} bits: {exception.Message}",
                exception);
        }
        catch (OverflowException exception)
        {
            throw new InvalidReplayDataException(
                $"Bunch payload size overflow in packet {context.Header.PacketId} on channel {context.Header.ChIndex} " +
                $"at payload position {context.Payload.Position} of {context.Payload.Length} bits.",
                exception);
        }
        finally
        {
            context.Dispose();
        }
    }

    public int FinalizePending() => _partialBunchAccumulator.FinalizePending();

    public void Dispose()
    {
        if (_isDisposed)
        {
            return;
        }

        _isDisposed = true;
        _partialBunchAccumulator.Dispose();
    }

    private static (BunchPayloadProcessor Processor, IPartialBunchAccumulator Accumulator) CreateProcessor(
        ReplayReaderContext context)
    {
        var packageMapReader = new PackageMapReader(context.NetGuidCache);
        var partialBunchAccumulator = new PartialBunchAccumulator(
            diagnosticCallback: context.Diagnostics.Add);
        var propertyPayloadDecoder = new PropertyPayloadDecoder(PayloadTransformSupport.DefaultRegistry);

        var contentBlockFramer = new ContentBlockFramer(
            packageMapReader,
            context,
            propertyPayloadDecoder);
        var newActorSerializer = new NewActorSerializer(packageMapReader, context.NetGuidCache);
        var lifecycleService = new ActorChannelLifecycleService(context);

        var processor = new BunchPayloadProcessor([
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
        return (processor, partialBunchAccumulator);
    }
}
