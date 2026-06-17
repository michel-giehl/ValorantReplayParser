using Replay.Encoding.Archives;
using Replay.Encoding.Compression;
using Replay.Models;
using Replay.Models.Contracts;
using Replay.Unreal.Contracts;

namespace Replay.Unreal.Pipeline;

public sealed class DecompressReplayData<TContext> : IReplayMiddleware<TContext>
    where TContext : IHaveArchive, IHaveReplayInfo, IHaveReplayDataStream, IHaveErrors
{
    private readonly IOodleDecompressor _oodleDecompressor;

    public DecompressReplayData(IOodleDecompressor oodleDecompressor)
    {
        _oodleDecompressor = oodleDecompressor;
    }

    public void Execute(TContext context, ReplayPipelineDelegate<TContext> next)
    {
        try
        {
            context.ReplayDataStream = new ReplayDataStreamMaterializer(context.Archive, _oodleDecompressor)
                .Materialize(context.ReplayInfo);

            next(context);
        }
        catch (Exception exception) when (IsReplayDataParseException(exception))
        {
            context.Errors.Add(new InvalidReplayDataError("Invalid replay data.", exception));
        }
    }

    private static bool IsReplayDataParseException(Exception exception) =>
        exception is InvalidReplayInfoException or ArchiveReadException or OodleDecompressionException or OverflowException;
}
