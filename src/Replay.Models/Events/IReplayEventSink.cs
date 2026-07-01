namespace Replay.Models.Events;

public interface IReplayEventSink
{
    void Emit(ReplayEvent replayEvent);
}