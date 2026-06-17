using Replay.Encoding.Archives;
using Replay.Unreal;
using Replay.Unreal.Pipeline;

var replayPath = args[0];

using var file = File.OpenRead(replayPath);
using var archive = new FBinaryArchive(file);

var context = new ReplayReaderContext(archive);

var readReplayInfo = new ReadReplayInfo<ReplayReaderContext>();
var scanReplayChunks = new ScanReplayChunks<ReplayReaderContext>();
var readReplayHeader = new ReadReplayHeader<ReplayReaderContext>();

readReplayInfo.Execute(context, ctx =>
    scanReplayChunks.Execute(ctx, scanCtx =>
        readReplayHeader.Execute(scanCtx, _ => { })));

if (context.Errors.Count > 0)
{
    foreach (var error in context.Errors)
    {
        Console.Error.WriteLine(error.Message);
        if (error.Exception is not null)
        {
            Console.Error.WriteLine(error.Exception.Message);
        }
    }

    return 1;
}

Console.WriteLine($"Read replay: {context.ReplayInfo.FriendlyName}");
Console.WriteLine($"Version: {context.ReplayVersion.Branch}");
Console.WriteLine($"Chunks: {context.ReplayInfo.Chunks.Count}");
Console.WriteLine($"Header GUID: {context.ReplayHeader.Guid}");

return 0;
