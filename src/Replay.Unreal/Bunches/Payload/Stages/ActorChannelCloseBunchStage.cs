namespace Replay.Unreal.Bunches.Payload.Stages;

internal sealed class ActorChannelCloseBunchStage : IBunchPayloadStage
{
    private readonly IActorChannelLifecycleService _lifecycleService;

    public ActorChannelCloseBunchStage(IActorChannelLifecycleService lifecycleService)
    {
        _lifecycleService = lifecycleService;
    }

    public BunchStageResult Process(BunchPayloadContext context)
    {
        if (context.Header.bClose && context.Channel is not null)
        {
            _lifecycleService.CloseActorChannel(context.Channel, context.Header, context.Stats);
        }

        return BunchStageResult.Continue;
    }
}