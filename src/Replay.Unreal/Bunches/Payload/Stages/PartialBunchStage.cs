using Replay.Encoding.Archives;

namespace Replay.Unreal.Bunches.Payload.Stages;

internal sealed class PartialBunchStage : IBunchPayloadStage, IResettableBunchPayloadStage
{
    private readonly IPartialBunchAccumulator _accumulator;

    public PartialBunchStage(IPartialBunchAccumulator accumulator)
    {
        _accumulator = accumulator;
    }

    public BunchStageResult Process(BunchPayloadContext context)
    {
        if (!context.Header.bPartial)
        {
            return BunchStageResult.Continue;
        }

        var result = _accumulator.AddFragment(context.Header.ChIndex, context.Header, context.Payload, context.Stats);
        context.Header = result.Header;

        if (!result.ShouldProcessCompletePayload)
        {
            return BunchStageResult.Stop;
        }

        if (!_accumulator.TryComplete(
                context.Header.ChIndex,
                out var stitchedBuffer,
                out var stitchedBitCount,
                out var stitchedHeader))
            return context.Header.IsPartialCompleted
                ? BunchStageResult.Continue
                : BunchStageResult.Stop;
        context.Header = stitchedHeader;
        context.UseOwnedPayload(new BitArchiveReader(stitchedBuffer, stitchedBitCount));
        return BunchStageResult.Continue;

    }

    public void Reset()
    {
        _accumulator.Reset();
    }
}