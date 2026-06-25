namespace Replay.Unreal.Bunches.Payload;

internal readonly struct BunchStageResult
{
    private BunchStageResult(bool shouldContinue)
    {
        ShouldContinue = shouldContinue;
    }

    public bool ShouldContinue { get; }

    public static BunchStageResult Continue { get; } = new(true);

    public static BunchStageResult Stop { get; } = new(false);
}
