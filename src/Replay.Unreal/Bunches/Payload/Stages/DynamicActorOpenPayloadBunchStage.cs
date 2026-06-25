namespace Replay.Unreal.Bunches.Payload.Stages;

internal sealed class DynamicActorOpenPayloadBunchStage : IBunchPayloadStage
{
    public BunchStageResult Process(BunchPayloadContext context)
    {
        if (!context.OpenedDynamicActor || context.Payload.AtEnd)
        {
            return BunchStageResult.Continue;
        }

        context.Stats.DynamicOpenPayloadBunchCount++;
        context.Stats.DynamicOpenPayloadBitsSkipped += context.Payload.BitsRemaining;
        context.Payload.SkipRemaining();
        return BunchStageResult.Continue;
    }
}