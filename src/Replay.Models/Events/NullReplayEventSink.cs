namespace Replay.Models.Events;

public sealed class NullReplayEventSink : IReplayEventSink
{
    public static NullReplayEventSink Instance { get; } = new();

    private NullReplayEventSink()
    {
    }

    public void Emit(ReplayEvent replayEvent)
    {
    }
}