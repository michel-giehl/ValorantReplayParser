using Replay.Encoding.Archives;

namespace Replay.Unreal.Bunches.Payload.Stages;

internal sealed class TrailingPayloadBunchStage : IBunchPayloadStage
{
    public BunchStageResult Process(ref BunchPayloadContext context)
    {
        if (context.Payload.AtEnd)
        {
            return BunchStageResult.Continue;
        }

        var unconsumed = context.Payload.BitsRemaining;
        throw new ArchiveReadException(
            ArchiveErrorCode.UnexpectedTrailingData,
            nameof(TrailingPayloadBunchStage),
            context.Payload.Position,
            context.Payload.Length,
            unconsumed,
            $"Bunch has {unconsumed} unexpected trailing bits in packet {context.Header.PacketId} " +
            $"on channel {context.Header.ChIndex}.");
    }
}
