namespace Replay.Unreal.Bunches.Payload;

internal sealed class BunchPayloadProcessor : IBunchPayloadProcessor
{
    private readonly IReadOnlyList<IBunchPayloadStage> _stages;

    public BunchPayloadProcessor(IReadOnlyList<IBunchPayloadStage> stages)
    {
        _stages = stages;
    }

    public void Process(BunchPayloadContext context)
    {
        foreach (var stage in _stages)
        {
            if (!stage.Process(context).ShouldContinue)
            {
                return;
            }
        }
    }

    public void Reset()
    {
        foreach (var stage in _stages)
        {
            if (stage is IResettableBunchPayloadStage resettable)
            {
                resettable.Reset();
            }
        }
    }
}
