using System.Buffers;
using System.Runtime.InteropServices.JavaScript;
using System.Text.Json;
using Replay.Encoding.Archives;
using Replay.Models.Descriptors;
using Replay.Unreal.Readers;
using Replay.Valorant;

namespace Replay.BrowserWasm;

public static partial class BrowserReplayParser
{
    private const int SchemaVersion = 1;

    private static readonly ParseProfile ViewerProfile = new()
    {
        EnabledCategories = ExportCategory.None,
        CaptureDiagnosticFields = false,
    };

    [JSExport]
    public static string Parse(byte[] replayBytes)
    {
        ArgumentNullException.ThrowIfNull(replayBytes);

        var output = new ArrayBufferWriter<byte>();
        using var writer = new Utf8JsonWriter(output);
        writer.WriteStartObject();
        writer.WriteNumber("schema_version", SchemaVersion);
        writer.WriteNumber("source_size_bytes", replayBytes.Length);
        writer.WriteStartArray("events");

        var eventSink = new BrowserReplayEventSink(writer);
        using var archive = new FBinaryArchive(replayBytes);
        var context = ValorantReplayReader.CreateDefault(
            loggerFactory: null,
            eventSink,
            ViewerProfile).Read(archive);

        writer.WriteEndArray();
        WriteMetadata(writer, context);
        WritePacketStats(writer, context);
        WriteEventCounts(writer, eventSink.Counts);
        writer.WriteEndObject();
        writer.Flush();

        return System.Text.Encoding.UTF8.GetString(output.WrittenSpan);
    }

    private static void WriteMetadata(Utf8JsonWriter writer, ReplayReaderContext context)
    {
        var replayVersion = context.ReplayVersion;
        var ueVersion = context.UEVersion;

        writer.WriteStartObject("metadata");
        writer.WriteString("friendly_name", context.ReplayInfo.FriendlyName);
        writer.WriteString(
            "timestamp",
            context.ReplayInfo.Timestamp.ToString(System.Globalization.CultureInfo.InvariantCulture));
        writer.WriteNumber("duration_ms", context.ReplayInfo.LengthInMs);
        writer.WriteString("replay_build", replayVersion.Branch);
        writer.WriteString(
            "replay_version",
            $"{replayVersion.Major}.{replayVersion.Minor}.{replayVersion.Patch}");
        writer.WriteNumber("replay_changelist", replayVersion.Changelist);
        writer.WriteNumber(
            "game_network_protocol_version",
            context.ReplayHeader.GameNetworkProtocolVersion);
        writer.WriteNumber("ue4_version", ueVersion.UE4Version);
        writer.WriteNumber("ue5_version", ueVersion.UE5Version);
        writer.WriteEndObject();
    }

    private static void WritePacketStats(Utf8JsonWriter writer, ReplayReaderContext context)
    {
        var stats = context.PacketStats;

        writer.WriteStartObject("stats");
        writer.WriteNumber("packet_count", stats.PacketCount);
        writer.WriteNumber("packets_with_bunches", stats.PacketsWithBunches);
        writer.WriteNumber("bunch_count", stats.BunchCount);
        writer.WriteNumber("malformed_packet_count", stats.MalformedPacketCount);
        writer.WriteNumber("partial_error_count", stats.PartialErrorCount);
        writer.WriteNumber("total_packet_bytes", stats.TotalPacketBytes);
        writer.WriteEndObject();
    }

    private static void WriteEventCounts(
        Utf8JsonWriter writer,
        BrowserReplayEventCounts counts)
    {
        writer.WriteStartObject("counts");
        writer.WriteNumber("emitted", counts.Emitted);
        writer.WriteNumber("actor_spawned", counts.ActorSpawned);
        writer.WriteNumber("actor_closed", counts.ActorClosed);
        writer.WriteNumber("export_group_received", counts.ExportGroups);
        writer.WriteNumber("filtered_export_groups", counts.FilteredExportGroups);
        writer.WriteNumber("rpc_received", counts.Rpcs);
        writer.WriteNumber("valorant_shot_received", counts.Shots);
        writer.WriteNumber("movement", counts.Movement);
        writer.WriteEndObject();
    }
}
