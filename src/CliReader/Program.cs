using CliReader;
using CliReader.Logging;
using Replay.Encoding.Archives;
using Microsoft.Extensions.Logging;
using Replay.Unreal.Readers;
using Replay.Valorant.Descriptors;
using Serilog;

Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Information()
    .WriteTo.Console(outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] {SourceContext}: {Message:lj}{NewLine}{Exception}")
    .CreateLogger();

try
{
    if (args.Length != 1)
    {
        Log.Error("Usage: CliReader <replay-path>");
        return 1;
    }

    var replayPath = args[0];
    Log.Information("Reading replay {ReplayPath}", replayPath);

    using var file = File.OpenRead(replayPath);
    using var archive = new FBinaryArchive(file);

    using var loggerFactory = LoggerFactory.Create(builder => builder
        .SetMinimumLevel(LogLevel.Information)
        .AddProvider(new SerilogLoggerProvider(Log.Logger)));
    var actorEventLogger = new ActorEventLogger(Log.Logger);
    var context = ValorantReplayReader.CreateDefault(
        loggerFactory,
        actorEventLogger,
        ValorantDescriptors.CreateCatalog()).Read(archive);

    if (context.Errors.Count > 0)
    {
        foreach (var error in context.Errors)
        {
            if (error.Exception is null)
            {
                Log.Error("{ReplayParseError}", error.Message);
                continue;
            }

            Log.Error(error.Exception, "{ReplayParseError}", error.Message);
        }

        return 1;
    }

    Log.Information("Read replay {ReplayName}", context.ReplayInfo.FriendlyName);
    Log.Information("Version {ReplayVersion}", context.ReplayVersion.Branch);
    Log.Information("Chunks {ChunkCount}", context.ReplayInfo.Chunks.Count);
    Log.Information("Timestamp {Timestamp}", context.ReplayInfo.Timestamp);
    Log.Information("Duration {Duration}", TimeSpan.FromMilliseconds(context.ReplayInfo.LengthInMs));
    Log.Information("File Size {FileSize} MB", file.Length / 1000_000);
    Log.Information("Packet Stats: Bunch Count={BunchCount}\tPacket Count={PC}\tMalformedPacketCount={MFPC}\tPartialErrorCount={PEC}\tTTL Bytes={TTLB} MB", context.PacketStats.BunchCount, context.PacketStats.PacketCount,
        context.PacketStats.MalformedPacketCount, context.PacketStats.PartialErrorCount, context.PacketStats.TotalPacketBytes / 1_000_000);
    actorEventLogger.LogSummary(context.WorldState);

    return 0;
}
catch (Exception exception)
{
    Log.Fatal(exception, "Failed to read replay.");
    return 1;
}
finally
{
    Log.CloseAndFlush();
}
