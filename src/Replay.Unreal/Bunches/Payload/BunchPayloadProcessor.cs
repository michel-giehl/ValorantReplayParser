namespace Replay.Unreal.Bunches.Payload;

internal sealed class BunchPayloadProcessor : IBunchPayloadProcessor
{
    private readonly IReadOnlyList<IBunchPayloadStage> _stages;

    public BunchPayloadProcessor(IReadOnlyList<IBunchPayloadStage> stages)
    {
        _stages = stages;
    }

    public void Process(ref BunchPayloadContext context)
    {
        foreach (var stage in _stages)
        {
            if (!stage.Process(ref context).ShouldContinue)
            {
                return;
            }
        }
    }
}
