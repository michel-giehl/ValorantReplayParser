using Replay.Encoding.Archives;
using Replay.Model;
using Replay.Model.Contracts;
using Replay.Unreal.Contracts;

namespace Replay.Unreal.Pipeline;

public sealed class ScanReplayChunks<TContext> : IReplayMiddleware<TContext>
    where TContext : IHaveArchive, IHaveReplayInfo, IHaveErrors
{
    private readonly ReplayInfoChunkScanFlags _flags;

    public ScanReplayChunks(ReplayInfoChunkScanFlags flags = ReplayInfoChunkScanFlags.None)
    {
        _flags = flags;
    }

    public void Execute(TContext context, ReplayPipelineDelegate<TContext> next)
    {
        try
        {
            var result = new ReplayInfoChunkScanner(context.Archive).Scan(context.ReplayInfo, _flags);

            if (result.HeaderChunkPayloadOffset is { } headerChunkPayloadOffset)
            {
                context.Archive.Seek(headerChunkPayloadOffset);
            }

            next(context);
        }
        catch (Exception exception) when (IsReplayInfoParseException(exception))
        {
            context.Errors.Add(new InvalidReplayInfoError("Invalid replay info chunks.", exception));
        }
    }

    private static bool IsReplayInfoParseException(Exception exception) =>
        exception is InvalidReplayInfoException or ArchiveReadException or OverflowException;
}
