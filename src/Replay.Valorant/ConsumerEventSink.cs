using System.Runtime.ExceptionServices;
using Replay.Models.Events;

namespace Replay.Valorant;

internal sealed class ConsumerEventSink : IReplayEventSink
{
    private readonly IReplayEventSink _inner;

    public ConsumerEventSink(IReplayEventSink inner)
    {
        _inner = inner;
    }

    public void Emit(ReplayEvent replayEvent)
    {
        try
        {
            _inner.Emit(replayEvent);
        }
        catch (Exception exception)
        {
            throw new ConsumerEventSinkException(exception);
        }
    }
}

internal sealed class ConsumerEventSinkException : Exception
{
    private readonly ExceptionDispatchInfo _dispatchInfo;

    public ConsumerEventSinkException(Exception exception)
        : base("A replay event consumer failed.", exception)
    {
        _dispatchInfo = ExceptionDispatchInfo.Capture(exception);
    }

    public void RethrowOriginal() => _dispatchInfo.Throw();
}
