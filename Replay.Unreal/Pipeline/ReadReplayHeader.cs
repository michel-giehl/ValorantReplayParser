using Replay.Encoding.Archives;
using Replay.Model;
using Replay.Model.Contracts;
using Replay.Unreal.Contracts;

namespace Replay.Unreal.Pipeline;

public sealed class ReadReplayHeader<TContext> : IReplayMiddleware<TContext>
    where TContext : IHaveArchive, IHaveReplayHeader, IHaveReplayVersion, IHaveErrors
{
    public void Execute(TContext context, ReplayPipelineDelegate<TContext> next)
    {
        try
        {
            var result = new ReplayHeaderReader(context.Archive).Read();
            context.ReplayHeader = result.Header;
            context.ReplayVersion = result.ReplayVersion;
            context.UEVersion = result.UEVersion;
            next(context);
        }
        catch (Exception exception) when (IsHeaderParseException(exception))
        {
            context.Errors.Add(new InvalidReplayHeaderError("Invalid replay header.", exception));
        }
    }

    private static bool IsHeaderParseException(Exception exception) =>
        exception is InvalidReplayHeaderException or ArchiveReadException or OverflowException;
}
