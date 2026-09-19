using System.Diagnostics;
using Microsoft.Extensions.Logging;
using Replay.Encoding.Archives;
using Replay.Models.Descriptors;
using Replay.Valorant;

namespace CliReader;

internal sealed class ReplayLogRunner
{
    private readonly ILoggerFactory _loggerFactory;
    private readonly ILogger _logger;

    public ReplayLogRunner(ILoggerFactory loggerFactory, ILogger logger)
    {
        _loggerFactory = loggerFactory;
        _logger = logger;
    }

    public void Run(LogOptions options)
    {
        _logger.LogInformation("Reading replay {ReplayPath}", options.ReplayPath);

        var stopwatch = Stopwatch.StartNew();
        using var file = File.OpenRead(options.ReplayPath);
        using var archive = new FBinaryArchive(file);

        var actorEventLogger = new ActorEventLogger(_loggerFactory.CreateLogger<ActorEventLogger>());
        var result = ValorantReplayReader.CreateDefault(
            _loggerFactory,
            actorEventLogger,
            ParseProfile.Default).Read(archive);
        var metadata = result.Metadata;

        Console.WriteLine($"Took: {stopwatch.ElapsedMilliseconds}ms");
        Console.WriteLine($"Read replay {metadata.ReplayInfo.FriendlyName}");
        Console.WriteLine($"Version {metadata.ReplayVersion.Branch}");
        Console.WriteLine($"Chunks {metadata.ReplayInfo.Chunks.Count}");
        Console.WriteLine($"Timestamp {metadata.ReplayInfo.Timestamp}");
        Console.WriteLine($"Duration {TimeSpan.FromMilliseconds(metadata.ReplayInfo.LengthInMs)}");
        Console.WriteLine($"File Size {file.Length / 1_000_000} MB");
        Console.WriteLine(
            $"Packet Stats: Bunch Count={result.PacketStats.BunchCount}\tPacket Count={result.PacketStats.PacketCount}\tMalformedPacketCount={result.PacketStats.MalformedPacketCount}\tPartialErrorCount={result.PacketStats.PartialErrorCount}\tTTL Bytes={result.PacketStats.TotalPacketBytes / 1_000_000} MB");
        actorEventLogger.LogSummary();
    }
}
