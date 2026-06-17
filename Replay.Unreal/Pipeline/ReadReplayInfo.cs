using Replay.Encoding.Archives;
using Replay.Models;
using Replay.Models.Contracts;
using Replay.Unreal.Contracts;

namespace Replay.Unreal.Pipeline;

public sealed class ReadReplayInfo<TContext> : IReplayMiddleware<TContext>
    where TContext : IHaveArchive, IHaveReplayInfo, IHaveErrors
{
    public void Execute(TContext context, ReplayPipelineDelegate<TContext> next)
    {
        try
        {
            var info = new ReplayInfo();
            var metadata = new ReplayInfoSerializationMetadata();
            var result = new ReplayInfoReader(context.Archive).Read(info, metadata);

            context.ReplayInfo = result.Info;
            context.ReplayInfoSerializationMetadata = result.SerializationMetadata;

            next(context);
        }
        catch (Exception exception) when (IsReplayInfoParseException(exception))
        {
            context.Errors.Add(new InvalidReplayInfoError("Invalid replay info.", exception));
        }
    }

    private static bool IsReplayInfoParseException(Exception exception) =>
        exception is InvalidReplayInfoException or ArchiveReadException or OverflowException;
}
