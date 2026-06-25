namespace Replay.Unreal.Bunches.Payload.Stages;

internal sealed class BunchStatsStage : IBunchPayloadStage
{
    public BunchStageResult Process(ref BunchPayloadContext context)
    {
        context.Stats.BunchCount++;

        if (context.Payload.BitLength > 0)
        {
            context.Stats.PayloadBunchCount++;
        }

        return BunchStageResult.Continue;
    }
}
