namespace Replay.Unreal.Pipeline;

public interface IReplayMiddleware<TContext>
{
    void Execute(TContext context, ReplayPipelineDelegate<TContext> next);
}
