using Replay.Encoding.Archives;

namespace Replay.Unreal.Bunches.Payload.Stages;

internal sealed class ContentBlocksBunchStage : IBunchPayloadStage
{
    private readonly ContentBlockFramer _contentBlockFramer;

    public ContentBlocksBunchStage(ContentBlockFramer contentBlockFramer)
    {
        _contentBlockFramer = contentBlockFramer;
    }

    public BunchStageResult Process(ref BunchPayloadContext context)
    {
        if (context.Payload.AtEnd)
        {
            return BunchStageResult.Continue;
        }

        if (context.Channel is null)
        {
            context.Payload.SkipRemaining();
            return BunchStageResult.Continue;
        }

        try
        {
            _contentBlockFramer.FrameContentBlocks(
                context.Payload,
                context.Channel,
                context.Stats,
                context.ReaderContext.CurrentTimeSeconds,
                context.Header.PacketId,
                context.ReaderContext.ReplayVersion.Branch);
            return BunchStageResult.Continue;
        }
        catch (ArchiveReadException)
        {
            context.Stats.MalformedPayloadCount++;
            context.Stats.MalformedPayloadExceptionCount++;
            return BunchStageResult.Stop;
        }
    }
}
