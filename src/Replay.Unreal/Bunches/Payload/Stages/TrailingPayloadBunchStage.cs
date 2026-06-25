namespace Replay.Unreal.Bunches.Payload.Stages;

internal sealed class TrailingPayloadBunchStage : IBunchPayloadStage
{
    public BunchStageResult Process(BunchPayloadContext context)
    {
        if (context.Payload.AtEnd)
        {
            return BunchStageResult.Continue;
        }

        var unconsumed = context.Payload.BitsRemaining;
        context.Payload.SkipBits(unconsumed);
        context.Stats.MalformedPayloadCount++;
        context.Stats.TrailingPayloadCount++;
        return BunchStageResult.Continue;
    }
}