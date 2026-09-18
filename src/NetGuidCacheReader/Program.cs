using System.Text;
using Replay.Encoding.Archives;
using Microsoft.Extensions.Logging;
using NetGuidCacheReader.Logging;
using Replay.Models.Descriptors;
using Replay.Models.Errors;
using Replay.Valorant;
using Serilog;

Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Debug()
    .WriteTo.Console(
        outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] {SourceContext}: {Message:lj}{NewLine}{Exception}")
    .CreateLogger();

using var loggerFactory = LoggerFactory.Create(builder => builder
    .SetMinimumLevel(LogLevel.Debug)
    .AddProvider(new SerilogLoggerProvider(Log.Logger)));
var logger = loggerFactory.CreateLogger("NetGuidCacheReader");

try
{
    if (args.Length != 2)
    {
        logger.LogError("Usage: NetGuidCacheReader <replay-path> <output>");
        return 1;
    }

    var replayPath = args[0];
    logger.LogInformation("Reading replay {ReplayPath}", replayPath);

    await using var file = File.OpenRead(replayPath);
    using var archive = new FBinaryArchive(file);

    var context = ValorantReplayReader.CreateDefault(
        loggerFactory,
        null,
        ParseProfile.Default).Read(archive);


    var guidCache = context.NetGuidCache;
    var outPath = args[1];

    var guidCacheString = string.Join("\n", guidCache.ExportGroupsByPath.Values.Select(_ =>
        $"{_.PathName}\n\t{string.Join("\n\t", _.NetFieldExports.Where(__ => __?.Name != null).Select(__ => $" {__!.Name} ({__.Handle})"))}"));

    await using var writer = File.Create(outPath);
    await writer.WriteAsync(Encoding.UTF8.GetBytes(guidCacheString));
    return 0;
}
catch (ReplayParseException exception)
{
    logger.LogError(exception, "Failed to parse replay.");
    return 1;
}
finally
{
    await Log.CloseAndFlushAsync();
}
