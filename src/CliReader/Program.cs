using CliReader;
using CliReader.Logging;
using Replay.Encoding.Archives;
using Microsoft.Extensions.Logging;
using Replay.Models.Errors;
using Replay.Unreal.Readers;
using Replay.Valorant.Descriptors;
using Serilog;

Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Information()
    .WriteTo.Console(outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] {SourceContext}: {Message:lj}{NewLine}{Exception}")
    .CreateLogger();

using var loggerFactory = LoggerFactory.Create(builder => builder
    .SetMinimumLevel(LogLevel.Information)
    .AddProvider(new SerilogLoggerProvider(Log.Logger)));
var logger = loggerFactory.CreateLogger("CliReader");

try
{
    if (args.Length != 1)
    {
        logger.LogError("Usage: CliReader <replay-path>");
        return 1;
    }

    var replayPath = args[0];
    logger.LogInformation("Reading replay {ReplayPath}", replayPath);

    using var file = File.OpenRead(replayPath);
    using var archive = new FBinaryArchive(file);

    var actorEventLogger = new ActorEventLogger(loggerFactory.CreateLogger<ActorEventLogger>());
    var context = ValorantReplayReader.CreateDefault(
        loggerFactory,
        actorEventLogger,
        ValorantDescriptors.CreateCatalog()).Read(archive);

    logger.LogInformation("Read replay {ReplayName}", context.ReplayInfo.FriendlyName);
    logger.LogInformation("Version {ReplayVersion}", context.ReplayVersion.Branch);
    logger.LogInformation("Chunks {ChunkCount}", context.ReplayInfo.Chunks.Count);
    logger.LogInformation("Timestamp {Timestamp}", context.ReplayInfo.Timestamp);
    logger.LogInformation("Duration {Duration}", TimeSpan.FromMilliseconds(context.ReplayInfo.LengthInMs));
    logger.LogInformation("File Size {FileSize} MB", file.Length / 1000_000);
    logger.LogInformation(
        "Packet Stats: Bunch Count={BunchCount}\tPacket Count={PacketCount}\tMalformedPacketCount={MalformedPacketCount}\tPartialErrorCount={PartialErrorCount}\tTTL Bytes={TotalBytes} MB",
        context.PacketStats.BunchCount,
        context.PacketStats.PacketCount,
        context.PacketStats.MalformedPacketCount,
        context.PacketStats.PartialErrorCount,
        context.PacketStats.TotalPacketBytes / 1_000_000);
    actorEventLogger.LogSummary();

    return 0;
}
catch (ReplayParseException exception)
{
    logger.LogError(exception, "Failed to parse replay.");
    return 1;
}
finally
{
    Log.CloseAndFlush();
}