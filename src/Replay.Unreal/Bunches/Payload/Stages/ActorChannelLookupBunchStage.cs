namespace Replay.Unreal.Bunches.Payload.Stages;

internal sealed class ActorChannelLookupBunchStage : IBunchPayloadStage
{
    public BunchStageResult Process(BunchPayloadContext context)
    {
        context.ReaderContext.ChannelStates.TryGetValue(context.Header.ChIndex, out var channel);
        context.Channel = channel;
        return BunchStageResult.Continue;
    }
}