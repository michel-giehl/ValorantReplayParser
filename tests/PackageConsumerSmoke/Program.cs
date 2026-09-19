using Replay.Models.Errors;
using Replay.Models.Diagnostics;
using Replay.Models.Events;
using Replay.Valorant;

namespace PackageConsumerSmoke;

internal static class Program
{
    private static int Main(string[] args)
    {
        try
        {
            Run(args);
            Console.WriteLine("Package consumer smoke check passed.");
            return 0;
        }
        catch (Exception exception)
        {
            Console.Error.WriteLine(exception);
            return 1;
        }
    }

    private static void Run(string[] args)
    {
        if (args.Length != 1)
        {
            throw new ArgumentException("Pass the path to a supported .vrf replay as the only argument.");
        }

        var replayPath = Path.GetFullPath(args[0]);
        if (!File.Exists(replayPath))
        {
            throw new FileNotFoundException("The replay passed to the smoke consumer was not found.", replayPath);
        }

        var eventSink = new CapturingReplayEventSink();
        var reader = new ValorantReplayReader(eventSink: eventSink);

        ValorantReplayMetadata metadata;
        using (var metadataStream = File.OpenRead(replayPath))
        {
            metadata = reader.ReadMetadata(metadataStream);
            Require(metadataStream.CanRead, "ReadMetadata disposed its borrowed input stream.");
            Require(metadataStream.Position == metadataStream.Length,
                "ReadMetadata did not leave the buffered borrowed stream at its end.");
        }

        Require(metadata.FullParseSupportStatus == ValorantReplaySupportStatus.Supported,
            "The smoke replay is not supported by this parser package.");

        ValorantReplayReadResult result;
        using (var replayStream = File.OpenRead(replayPath))
        {
            result = reader.Read(replayStream);
            Require(replayStream.CanRead, "Read disposed its borrowed input stream.");
            Require(replayStream.Position == replayStream.Length,
                "Read did not leave the buffered borrowed stream at its end.");
        }

        Require(result.Metadata.FullParseSupportStatus == ValorantReplaySupportStatus.Supported,
            "The detached result lost its supported replay metadata.");
        Require(result.PacketStats.PacketCount > 0, "The parser did not read any replay packets.");
        Require(result.Status is ReplayReadStatus.Completed or ReplayReadStatus.CompletedWithWarnings,
            "The parser returned an invalid completion status.");

        var typedGameplayEventCount = eventSink.Events.Count(IsTypedGameplayEvent);
        Require(typedGameplayEventCount > 0, "The parser did not emit any typed gameplay events.");

        var exportGroups = result.ExportGroups;
        Require(exportGroups.Count > 0, "The result did not expose any detached export-group summaries.");
        foreach (var exportGroup in exportGroups)
        {
            _ = exportGroup.PathName.Length;
            foreach (var field in exportGroup.Fields)
            {
                _ = field.Name.Length;
                _ = field.Handle;
                _ = field.CompatibleChecksum;
            }
        }

        Require(result.PacketStats.PacketCount > 0 && exportGroups.Count > 0,
            "The detached result became unusable after the input stream was disposed.");

        using var invalidStream = new MemoryStream([0xEF, 0xBE, 0xAD, 0xDE], writable: false);
        try
        {
            reader.ReadMetadata(invalidStream);
            throw new InvalidOperationException("Invalid replay magic was accepted.");
        }
        catch (ReplayParseException)
        {
            // Invalid container data must use the documented parse exception contract.
        }
    }

    private static bool IsTypedGameplayEvent(ReplayEvent replayEvent) =>
        replayEvent.GetType().Namespace?.StartsWith("Replay.Valorant.", StringComparison.Ordinal) == true
        || replayEvent is ExportGroupReceived { WasDecoded: true, Payload: not null }
        || replayEvent is RpcReceived { WasDecoded: true, Payload: not null };

    private static void Require(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }

    private sealed class CapturingReplayEventSink : IReplayEventSink
    {
        public List<ReplayEvent> Events { get; } = [];

        public void Emit(ReplayEvent replayEvent) => Events.Add(replayEvent);
    }
}
