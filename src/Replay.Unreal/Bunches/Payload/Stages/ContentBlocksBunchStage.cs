using Replay.Encoding.Archives;
using Replay.Models.Errors;

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
        catch (ArchiveReadException exception)
        {
            throw new InvalidReplayDataException(
                $"Malformed content blocks in packet {context.Header.PacketId} on channel {context.Header.ChIndex} " +
                $"at payload position {context.Payload.Position} of {context.Payload.Length} bits: {exception.Message}",
                exception);
        }
    }
}
